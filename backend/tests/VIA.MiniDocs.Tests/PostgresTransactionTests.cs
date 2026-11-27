using Microsoft.EntityFrameworkCore;
using Npgsql;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class PostgresTransactionTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InvitationClaimUsesCurrentStateAndDatabaseTimeAfterWaiting(bool expiresWhileWaiting)
  {
    await using var db = await CreateDatabaseAsync();
    var admin = Guid.NewGuid();
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(db, admin, "admin", "admin_only");
    await PostgresSeed.InsertUserAndWorkspace(db, first, Guid.NewGuid(), "first");
    await PostgresSeed.InsertUserAndWorkspace(db, second, Guid.NewGuid(), "second");
    var hash = System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray());
    var invitation = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.user_invitations(id, token_hash, created_by, expires_at)
        VALUES ({invitation}, {hash}, {admin}, clock_timestamp() + interval '1 minute')
        """);

    await using var peer = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var tx1 = await db.Database.BeginTransactionAsync();
    await using var tx2 = await peer.Database.BeginTransactionAsync();
    var pid = await peer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
    if (expiresWhileWaiting)
    {
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.user_invitations SET expires_at = clock_timestamp() + interval '1 second'
          WHERE id = {invitation}
          """);
    }
    else
    {
      Assert.True(await InvitationPersistence.TryRedeemAsync(db, hash, first));
    }

    var claim = InvitationPersistence.TryRedeemAsync(peer, hash, second);
    try
    {
      await PostgresWait.ForBlockedAsync(observer, pid, claim);
      if (expiresWhileWaiting)
      {
        // Wait using database time rather than making an assumption about the client clock.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_sleep(GREATEST(0, EXTRACT(EPOCH FROM
                ((SELECT expires_at FROM minidocs.user_invitations WHERE id = {invitation})
                - clock_timestamp())))::double precision + 0.05)
            """);
      }
      await tx1.CommitAsync();
    }
    catch
    {
      await tx1.RollbackAsync();
      try { await claim; } catch (PostgresException) { }
      throw;
    }
    Assert.False(await claim);
    await tx2.RollbackAsync();
    var result = await observer.UserInvitations.AsNoTracking().SingleAsync();
    Assert.Equal(expiresWhileWaiting ? (Guid?)null : first, result.RedeemedUserId);
  }

  [Fact]
  public async Task StaleTranscriptRevisionCannotEraseConcurrentlyInsertedPage()
  {
    await using var db = await CreateDatabaseAsync();
    var (workspace, document) = await SeedDocumentAsync(db);
    await using var peer = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var tx1 = await db.Database.BeginTransactionAsync();
    await using var tx2 = await peer.Database.BeginTransactionAsync();
    var pid = await peer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
    Assert.True(await TranscriptPersistence.TryReplaceAsync(db, workspace, document, 0,
        true, true, [new(1, "First page", true), new(2, "Inserted page", false)]));
    var replace = TranscriptPersistence.TryReplaceAsync(peer, workspace, document, 0,
        true, true, [new(1, "Stale edit", true)]);
    try
    {
      await PostgresWait.ForBlockedAsync(observer, pid, replace);
      await tx1.CommitAsync();
    }
    catch
    {
      await tx1.RollbackAsync();
      try { await replace; } catch (PostgresException) { }
      throw;
    }
    Assert.False(await replace);
    await tx2.RollbackAsync();
    Assert.Equal(2, await observer.TranscriptPages.ObserveAllWorkspaces().CountAsync());
    Assert.Equal(1L, (await observer.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).TranscriptRevision);
    Assert.Equal("Inserted page", (await observer.TranscriptPages.ObserveAllWorkspaces().AsNoTracking()
        .SingleAsync(page => page.PageNo == 2)).Text);
  }

  [Fact]
  public async Task TranscriptReplacementPreservesPageVersionsAndRollsBackAtomically()
  {
    await using var db = await CreateDatabaseAsync();
    var (workspace, document) = await SeedDocumentAsync(db);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TranscriptPersistence.TryReplaceAsync(db, workspace, document, 0,
          true, false, [new(1, "Old", false), new(2, "Stable", false)]));
      await tx.CommitAsync();
    }
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TranscriptPersistence.TryReplaceAsync(db, workspace, document, 1,
          true, true, [new(1, "New", true), new(2, "Stable", false)]));
      await tx.CommitAsync();
    }
    var pages = await db.TranscriptPages.ObserveAllWorkspaces().AsNoTracking().OrderBy(page => page.PageNo).ToListAsync();
    Assert.Equal(2L, pages[0].Version);
    Assert.Equal(1L, pages[1].Version);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TranscriptPersistence.TryReplaceAsync(db, workspace, document, 2, false, false, []));
      await tx.RollbackAsync();
    }
    Assert.Equal(2, await db.TranscriptPages.ObserveAllWorkspaces().CountAsync());
    Assert.Equal(2L, (await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).TranscriptRevision);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      await Assert.ThrowsAsync<ArgumentException>(() => TranscriptPersistence.TryReplaceAsync(
          db, workspace, document, 2, true, false, [new(3, "Outside range", false)]));
      await tx.RollbackAsync();
    }
    Assert.Equal(2, await db.TranscriptPages.ObserveAllWorkspaces().CountAsync());
  }

  [Fact]
  public async Task PageEditRequiresBothVersions()
  {
    await using var db = await CreateDatabaseAsync();
    var (workspace, document) = await SeedDocumentAsync(db);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TranscriptPersistence.TryReplaceAsync(db, workspace, document, 0,
          true, false, [new(1, "Original", false)]));
      await tx.CommitAsync();
    }
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.False(await TranscriptPersistence.TryEditPageAsync(db, workspace, document, 1, 1, 2, "Stale page"));
      Assert.False(await TranscriptPersistence.TryEditPageAsync(db, workspace, document, 0, 1, 1, "Stale aggregate"));
      Assert.True(await TranscriptPersistence.TryEditPageAsync(db, workspace, document, 1, 1, 1, "Edited"));
      await tx.CommitAsync();
    }
    var page = await db.TranscriptPages.ObserveAllWorkspaces().AsNoTracking().SingleAsync();
    Assert.Equal("Edited", page.Text);
    Assert.Equal(2L, page.Version);
    Assert.True(page.Edited);
    var saved = await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync();
    Assert.True(saved.TranscriptEdited);
    Assert.Equal(2L, saved.TranscriptRevision);
  }

  [Fact]
  public async Task ProvisioningLeaseSerializesStartsAndFencesExpiredResponses()
  {
    await using var db = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "provision");
    var operation = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.via_docs_accounts(id, workspace_id, application_name, application_secret_protected)
        VALUES ({Guid.NewGuid()}, {workspace}, 'prepared-app', decode('01','hex'));
        INSERT INTO minidocs.provisioning_operations(id, workspace_id, status, invitation_protected, attempt_no)
        VALUES ({operation}, {workspace}, 'ready', decode('02','hex'), 0)
        """);
    await using var peer = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var tx1 = await db.Database.BeginTransactionAsync();
    await using var tx2 = await peer.Database.BeginTransactionAsync();
    var pid = await peer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
    var first = await ProvisioningPersistence.TryBeginProvisioningAsync(
        db, workspace, operation, 1, Guid.NewGuid(), TimeSpan.FromMinutes(1));
    Assert.NotNull(first);
    var contender = ProvisioningPersistence.TryBeginProvisioningAsync(
        peer, workspace, operation, 1, Guid.NewGuid(), TimeSpan.FromMinutes(1));
    try
    {
      await PostgresWait.ForBlockedAsync(observer, pid, contender);
      await tx1.CommitAsync();
    }
    catch
    {
      await tx1.RollbackAsync();
      try { await contender; } catch (PostgresException) { }
      throw;
    }
    Assert.Null(await contender);
    await tx2.RollbackAsync();

    var fence = first.GetValueOrDefault();
    // Simulate an abandoned worker without relying on wall-clock sleeps.
    await observer.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.provisioning_operations SET lease_until = clock_timestamp() - interval '1 second'
        WHERE id = {operation}
        """);
    await using (var tx = await observer.Database.BeginTransactionAsync())
    {
      Assert.Null(await ProvisioningPersistence.TryRecordProvisioningCandidateAsync(
          observer, workspace, operation, fence, Guid.NewGuid()));
      Assert.True(await ProvisioningPersistence.TryRecoverExpiredProvisioningAsync(
          observer, workspace, operation, fence.Version));
      Assert.Null(await ProvisioningPersistence.TryBeginProvisioningAsync(
          observer, workspace, operation, fence.Version + 1, Guid.NewGuid(), TimeSpan.FromMinutes(1)));
      await tx.CommitAsync();
    }
    var final = await observer.ProvisioningOperations.AsNoTracking().SingleAsync();
    Assert.Equal(ProvisioningStatus.RecoveryRequired, final.Status);
    Assert.Equal(1, final.AttemptNo);
    Assert.Null(final.CandidateApplicationId);
    Assert.Null(final.LeaseOwner);
  }

  [Fact]
  public async Task UploadFencesOldWorkersAndCompletesOnlyWithMatchingDigest()
  {
    await using var db = await CreateDatabaseAsync();
    var (workspace, document) = await SeedDocumentAsync(db);
    var operation = await SeedUploadAsync(db, workspace, document);
    var remote = Guid.NewGuid();
    var hash = System.Security.Cryptography.SHA256.HashData(new byte[] { 1, 2, 3 });
    OperationFence first;
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      var fence = await OperationLeasePersistence.TryBeginUploadAsync(
          db, workspace, document, operation, 1, Guid.NewGuid(), TimeSpan.FromMinutes(1));
      Assert.NotNull(fence);
      first = fence.GetValueOrDefault();
      await tx.CommitAsync();
    }
    OperationFence second;
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.Null(await OperationLeasePersistence.TryRecordUploadRemoteAsync(
          db, workspace, document, operation, new OperationFence(Guid.NewGuid(), first.Version), remote));
      var fence = await OperationLeasePersistence.TryRecordUploadRemoteAsync(
          db, workspace, document, operation, first, remote);
      Assert.NotNull(fence);
      second = fence.GetValueOrDefault();
      await tx.CommitAsync();
    }
    Assert.Null((await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).ViaFileId);
    var known = await db.FileOperations.ObserveAllWorkspaces().AsNoTracking().SingleAsync();
    Assert.Equal(remote, known.RemoteFileId);
    Assert.Null(known.VerifiedSha256);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operations SET lease_until = clock_timestamp() - interval '1 second'
        WHERE id = {operation}
        """);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.Null(await OperationLeasePersistence.TryRecordUploadDigestAsync(
          db, workspace, document, operation, second, 3, hash));
      var reclaimed = await OperationLeasePersistence.TryReclaimKnownUploadAsync(
          db, workspace, document, operation, second.Version, Guid.NewGuid(), TimeSpan.FromMinutes(1));
      Assert.NotNull(reclaimed);
      var measured = await OperationLeasePersistence.TryRecordUploadDigestAsync(
          db, workspace, document, operation, reclaimed.GetValueOrDefault(), 3, hash);
      Assert.NotNull(measured);
      second = measured.GetValueOrDefault();
      await tx.CommitAsync();
    }
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.False(await OperationLeasePersistence.TryCompleteUploadAsync(
          db, workspace, document, operation, 1, first, remote, 3, hash));
      Assert.False(await OperationLeasePersistence.TryCompleteUploadAsync(
          db, workspace, document, operation, 1, second, remote, 4, hash));
      Assert.False(await OperationLeasePersistence.TryCompleteUploadAsync(
          db, workspace, document, operation, 1, second, remote, 3, new byte[32]));
      Assert.False(await OperationLeasePersistence.TryCompleteUploadAsync(
          db, workspace, document, operation, 2, second, remote, 3, hash));
      Assert.True(await OperationLeasePersistence.TryCompleteUploadAsync(
          db, workspace, document, operation, 1, second, remote, 3, hash));
      await tx.CommitAsync();
    }
    var saved = await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync();
    Assert.Equal(remote, saved.ViaFileId);
    Assert.Equal(DocumentAvailability.Available, saved.Availability);
    var file = await db.FileOperations.ObserveAllWorkspaces().AsNoTracking().SingleAsync();
    Assert.Equal(FileOperationState.Completed, file.State);
    Assert.NotNull(file.CompletedAt);
    Assert.Null(file.LeaseOwner);
    Assert.Equal(1, file.AttemptNo);
  }

  [Fact]
  public async Task ConcurrentUploadStartsReserveOnlyOneExternalAttempt()
  {
    await using var db = await CreateDatabaseAsync();
    var (workspace, document) = await SeedDocumentAsync(db);
    var operation = await SeedUploadAsync(db, workspace, document);
    await using var peer = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var tx1 = await db.Database.BeginTransactionAsync();
    await using var tx2 = await peer.Database.BeginTransactionAsync();
    var pid = await peer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
    Assert.NotNull(await OperationLeasePersistence.TryBeginUploadAsync(
        db, workspace, document, operation, 1, Guid.NewGuid(), TimeSpan.FromMinutes(1)));
    var contender = OperationLeasePersistence.TryBeginUploadAsync(
        peer, workspace, document, operation, 1, Guid.NewGuid(), TimeSpan.FromMinutes(1));
    try
    {
      await PostgresWait.ForBlockedAsync(observer, pid, contender);
      await tx1.CommitAsync();
    }
    catch
    {
      await tx1.RollbackAsync();
      try { await contender; } catch (PostgresException) { }
      throw;
    }
    Assert.Null(await contender);
    await tx2.RollbackAsync();
    var saved = await observer.FileOperations.ObserveAllWorkspaces().AsNoTracking().SingleAsync();
    Assert.Equal(FileOperationState.InFlight, saved.State);
    Assert.Equal(1, saved.AttemptNo);
    Assert.Equal(2L, saved.Version);
  }

  [Fact]
  public async Task ExpiredUploadRemainsUnknownAndCannotBeSentAgain()
  {
    await using var db = await CreateDatabaseAsync();
    var (workspace, document) = await SeedDocumentAsync(db);
    var operation = await SeedUploadAsync(db, workspace, document);
    OperationFence first;
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      var fence = await OperationLeasePersistence.TryBeginUploadAsync(
          db, workspace, document, operation, 1, Guid.NewGuid(), TimeSpan.FromMinutes(1));
      Assert.NotNull(fence);
      first = fence.GetValueOrDefault();
      await tx.CommitAsync();
    }
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operations SET lease_until = clock_timestamp() - interval '1 second'
        WHERE id = {operation}
        """);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await OperationLeasePersistence.TryRecoverExpiredUploadAsync(
          db, workspace, document, operation, first.Version));
      Assert.Null(await OperationLeasePersistence.TryBeginUploadAsync(
          db, workspace, document, operation, first.Version + 1, Guid.NewGuid(), TimeSpan.FromMinutes(1)));
      Assert.Null(await OperationLeasePersistence.TryRecordUploadRemoteAsync(
          db, workspace, document, operation, first, Guid.NewGuid()));
      await tx.CommitAsync();
    }
    Assert.Equal(FileOperationState.Unknown, (await db.FileOperations.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).State);
    Assert.Equal(DocumentAvailability.PendingUpload, (await db.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).Availability);
  }

  private async Task<MiniDocsDbContext> CreateDatabaseAsync()
  {
    var db = await fixture.CreateDatabaseAsync();
    try { await db.Database.MigrateAsync(); return db; }
    catch { await db.DisposeAsync(); throw; }
  }

  private static async Task<(Guid Workspace, Guid Document)> SeedDocumentAsync(MiniDocsDbContext db)
  {
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertActiveWorkspaceAsync(db, Guid.NewGuid(), workspace, "document");
    var document = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability, page_count, page_count_source)
        VALUES ({document}, {workspace}, {topic}, 'Test PDF', 'meeting', 'pending_upload', 2, 'pdf_import')
        """);
    return (workspace, document);
  }

  private static async Task<Guid> SeedUploadAsync(MiniDocsDbContext db, Guid workspace, Guid document)
  {
    var operation = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, attempt_no, request_id)
        VALUES ({operation}, {workspace}, {document}, 'upload', 'prepared', 0, {Guid.NewGuid()})
        """);
    return operation;
  }

}
