using Microsoft.EntityFrameworkCore;
using Npgsql;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class PostgresTrashTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  [Fact]
  public async Task TrashAndRestoreKeepOriginalTopicTagsAndTranscript()
  {
    await using var db = await CreateDatabaseAsync();
    var seed = await SeedAsync(db);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TrashPersistence.TryTrashAsync(db, seed.Workspace, seed.Document, 1));
      await tx.CommitAsync();
    }
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.False(await TrashPersistence.TryRestoreAsync(db, seed.Workspace, seed.Document, 1));
      Assert.True(await TrashPersistence.TryRestoreAsync(db, seed.Workspace, seed.Document, 2));
      await tx.CommitAsync();
    }
    var document = await db.Documents.AsNoTracking().SingleAsync();
    Assert.Null(document.DeletedAt);
    Assert.Equal(seed.Topic, document.TopicId);
    Assert.Equal(seed.Remote, document.ViaFileId);
    Assert.Equal(3L, document.Version);
    Assert.Equal(1, await db.DocumentTags.CountAsync());
    Assert.Equal("Retained text", (await db.TranscriptPages.AsNoTracking().SingleAsync()).Text);
    Assert.Equal(1, await db.FileOperations.CountAsync());
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task RestoreAndPurgeCannotBothWin(bool purgeFirst)
  {
    await using var db = await CreateDatabaseAsync();
    var seed = await SeedAsync(db);
    await TrashAsync(db, seed);
    await using var peer = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var tx1 = await db.Database.BeginTransactionAsync();
    await using var tx2 = await peer.Database.BeginTransactionAsync();
    var pid = await peer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
    var operation = Guid.NewGuid();
    Task<bool> contender;
    if (purgeFirst)
    {
      Assert.NotNull(await TrashPersistence.TryBeginPurgeAsync(db, seed.Workspace, seed.Document, 2,
          operation, Guid.NewGuid(), TimeSpan.FromMinutes(1)));
      contender = TrashPersistence.TryRestoreAsync(peer, seed.Workspace, seed.Document, 2);
    }
    else
    {
      Assert.True(await TrashPersistence.TryRestoreAsync(db, seed.Workspace, seed.Document, 2));
      contender = TryStartPurgeAsync(peer, seed, operation);
    }
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
    Assert.False(await contender);
    await tx2.RollbackAsync();
    var saved = await observer.Documents.AsNoTracking().SingleAsync();
    Assert.Equal(purgeFirst ? DocumentAvailability.DeletePending : DocumentAvailability.Available,
        saved.Availability);
    Assert.Equal(purgeFirst, saved.DeletedAt.HasValue);
    Assert.Equal(3L, saved.Version);
    Assert.Equal(purgeFirst ? 1 : 0, await observer.FileOperations.CountAsync(
        op => op.Kind == FileOperationKind.Delete));
  }

  [Fact]
  public async Task UnknownDeletePreservesReferenceAndFencesLateResults()
  {
    await using var db = await CreateDatabaseAsync();
    var seed = await SeedAsync(db);
    await TrashAsync(db, seed);
    var operation = Guid.NewGuid();
    var first = await BeginPurgeAsync(db, seed, operation);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TrashPersistence.TryMarkDeleteUnknownAsync(db, seed.Workspace, seed.Document,
          operation, first.Fence));
      Assert.False(await TrashPersistence.TryRestoreAsync(db, seed.Workspace, seed.Document, 3));
      Assert.False(await TrashPersistence.TryCompleteConfirmedPurgeAsync(db, seed.Workspace,
          seed.Document, operation, first));
      Assert.Null(await TrashPersistence.TryBeginPurgeAsync(db, seed.Workspace, seed.Document, 3,
          Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(1)));
      await tx.CommitAsync();
    }
    var pending = await db.Documents.AsNoTracking().SingleAsync();
    Assert.Equal(seed.Remote, pending.ViaFileId);
    Assert.Equal(DocumentAvailability.DeletePending, pending.Availability);
    Assert.Equal(FileOperationState.DeleteUnknown, (await db.FileOperations.AsNoTracking()
        .SingleAsync(op => op.Id == operation)).State);
    Assert.Equal(0, await db.FileOperationReceipts.CountAsync());
    Assert.Equal(1, await db.TranscriptPages.CountAsync());

    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      var verification = await TrashPersistence.TryBeginDeleteVerificationAsync(db, seed.Workspace,
          seed.Document, operation, first.Fence.Version + 1, Guid.NewGuid(), TimeSpan.FromMinutes(1));
      Assert.NotNull(verification);
      Assert.False(await TrashPersistence.TryCompleteConfirmedPurgeAsync(db, seed.Workspace, seed.Document,
          operation, verification.GetValueOrDefault() with { RemoteFileId = Guid.NewGuid() }));
      // Simulated confirmed absence, not an actual VIA.Docs call.
      Assert.True(await TrashPersistence.TryCompleteConfirmedPurgeAsync(db, seed.Workspace, seed.Document,
          operation, verification.GetValueOrDefault()));
      await tx.CommitAsync();
    }
    Assert.Equal(0, await db.Documents.CountAsync());
    Assert.Equal(1, await db.FileOperationReceipts.CountAsync());
  }

  [Fact]
  public async Task ExpiredDeleteCanBeReconciledButNotSentAgain()
  {
    await using var db = await CreateDatabaseAsync();
    var seed = await SeedAsync(db);
    await TrashAsync(db, seed);
    var operation = Guid.NewGuid();
    var first = await BeginPurgeAsync(db, seed, operation);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operations SET lease_until = clock_timestamp() - interval '1 second'
        WHERE id = {operation}
        """);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.False(await TrashPersistence.TryCompleteConfirmedPurgeAsync(db, seed.Workspace,
          seed.Document, operation, first));
      Assert.True(await TrashPersistence.TryRecoverExpiredDeleteAsync(db, seed.Workspace, seed.Document,
          operation, first.Fence.Version));
      Assert.Null(await TrashPersistence.TryBeginPurgeAsync(db, seed.Workspace, seed.Document, 3,
          Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(1)));
      Assert.NotNull(await TrashPersistence.TryBeginDeleteVerificationAsync(db, seed.Workspace, seed.Document,
          operation, first.Fence.Version + 1, Guid.NewGuid(), TimeSpan.FromMinutes(1)));
      await tx.CommitAsync();
    }
    var pending = await db.FileOperations.AsNoTracking().SingleAsync(op => op.Id == operation);
    Assert.Equal(FileOperationState.DeleteUnknown, pending.State);
    Assert.Equal(1, pending.AttemptNo);
    Assert.Equal(0, await db.FileOperationReceipts.CountAsync());
  }

  [Fact]
  public async Task ConfirmedPurgeIsAtomicAndRetainsOnlyMinimalReceiptAndRequestTombstone()
  {
    await using var db = await CreateDatabaseAsync();
    var seed = await SeedAsync(db);
    await TrashAsync(db, seed);
    var operation = Guid.NewGuid();
    var reservation = await BeginPurgeAsync(db, seed, operation);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TrashPersistence.TryCompleteConfirmedPurgeAsync(db, seed.Workspace,
          seed.Document, operation, reservation));
      await tx.RollbackAsync();
    }
    Assert.Equal(1, await db.Documents.CountAsync());
    Assert.Equal(2, await db.FileOperations.CountAsync());
    Assert.Equal(1, await db.TranscriptPages.CountAsync());
    Assert.Equal(1, await db.DocumentTags.CountAsync());
    Assert.Equal(0, await db.UploadRequestTombstones.CountAsync());
    Assert.Equal(0, await db.FileOperationReceipts.CountAsync());
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TrashPersistence.TryCompleteConfirmedPurgeAsync(db, seed.Workspace,
          seed.Document, operation, reservation));
      await tx.CommitAsync();
    }
    Assert.Equal(0, await db.Documents.CountAsync());
    Assert.Equal(0, await db.FileOperations.CountAsync());
    Assert.Equal(0, await db.TranscriptPages.CountAsync());
    Assert.Equal(0, await db.DocumentTags.CountAsync());
    Assert.Equal(1, await db.Tags.CountAsync());
    Assert.Equal(1, await db.Topics.CountAsync());
    var tombstone = await db.UploadRequestTombstones.AsNoTracking().SingleAsync();
    Assert.Equal(seed.Workspace, tombstone.WorkspaceId);
    Assert.Equal(seed.Request, tombstone.RequestId);
    var receipt = await db.FileOperationReceipts.AsNoTracking().SingleAsync();
    Assert.Equal(seed.Workspace, receipt.WorkspaceId);
    Assert.Equal(operation, receipt.OperationId);
    Assert.Equal(FileOperationOutcome.RemoteDeleted, receipt.Outcome);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.False(await TrashPersistence.TryCompleteConfirmedPurgeAsync(db, seed.Workspace,
          seed.Document, operation, reservation));
      Assert.Equal(0, await OperationRetentionPersistence.RemoveExpiredReceiptsAsync(db, seed.Workspace, 10));
      await tx.CommitAsync();
    }
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operation_receipts SET completed_at = clock_timestamp() - interval '31 days'
        WHERE operation_id = {operation}
        """);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.Equal(1, await OperationRetentionPersistence.RemoveExpiredReceiptsAsync(db, seed.Workspace, 10));
      await tx.CommitAsync();
    }
    Assert.Equal(0, await db.FileOperationReceipts.CountAsync());
    Assert.Equal(1, await db.UploadRequestTombstones.CountAsync());
  }

  [Fact]
  public async Task RetentionIsBoundedWorkspaceScopedAndNeverRemovesUnknownOrRecentOperations()
  {
    await using var db = await CreateDatabaseAsync();
    var first = await SeedAsync(db);
    var second = await SeedAsync(db);
    var third = await SeedAsync(db);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operations SET completed_at = clock_timestamp() - interval '31 days'
        WHERE request_id IN ({first.Request}, {second.Request});
        """);
    var extra = Guid.NewGuid();
    var extraRequest = Guid.NewGuid();
    var extraRemote = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability, via_file_id)
        VALUES ({extra}, {first.Workspace}, {first.Topic}, 'Older PDF', 'meeting', 'available', {extraRemote});
        INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, request_id,
            attempt_no, remote_file_id, completed_at, verified_size, verified_sha256)
        VALUES ({Guid.NewGuid()}, {first.Workspace}, {extra}, 'upload', 'completed', {extraRequest},
            1, {extraRemote}, clock_timestamp() - interval '32 days', 1, decode(repeat('00', 32), 'hex'));
        """);
    var pending = Guid.NewGuid();
    var unknownRequest = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
        VALUES ({pending}, {first.Workspace}, {first.Topic}, 'Pending', 'meeting', 'pending_upload');
        INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, request_id, attempt_no)
        VALUES ({Guid.NewGuid()}, {first.Workspace}, {pending}, 'upload', 'unknown', {unknownRequest}, 1);
        """);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.Equal(1, await OperationRetentionPersistence.RetireCompletedUploadsAsync(db, first.Workspace, 1));
      await tx.RollbackAsync();
    }
    Assert.Equal(0, await db.UploadRequestTombstones.CountAsync());
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.Equal(1, await OperationRetentionPersistence.RetireCompletedUploadsAsync(db, first.Workspace, 1));
      Assert.Equal(1, await db.UploadRequestTombstones.CountAsync());
      Assert.Equal(1, await OperationRetentionPersistence.RetireCompletedUploadsAsync(db, first.Workspace, 1));
      Assert.Equal(0, await OperationRetentionPersistence.RetireCompletedUploadsAsync(db, first.Workspace, 1));
      Assert.Equal(0, await OperationRetentionPersistence.RetireCompletedUploadsAsync(db, third.Workspace, 1));
      await tx.CommitAsync();
    }
    Assert.Equal(5, await db.Documents.CountAsync());
    Assert.Equal(3, await db.FileOperations.CountAsync());
    Assert.Equal(FileOperationState.Unknown, (await db.FileOperations.AsNoTracking()
        .SingleAsync(op => op.RequestId == unknownRequest)).State);
    var markers = await db.UploadRequestTombstones.AsNoTracking().ToListAsync();
    Assert.Equal(2, markers.Count);
    Assert.All(markers, marker => Assert.Equal(first.Workspace, marker.WorkspaceId));
    Assert.Contains(markers, marker => marker.RequestId == first.Request);
    Assert.Contains(markers, marker => marker.RequestId == extraRequest);
  }

  [Fact]
  public async Task TrashOperationsRequireCorrectWorkspaceAndExplicitTransaction()
  {
    await using var db = await CreateDatabaseAsync();
    var seed = await SeedAsync(db);
    var foreign = await SeedAsync(db);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        TrashPersistence.TryTrashAsync(db, seed.Workspace, seed.Document, 1));
    await TrashAsync(db, seed);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.False(await TrashPersistence.TryRestoreAsync(db, foreign.Workspace, seed.Document, 2));
      Assert.Null(await TrashPersistence.TryBeginPurgeAsync(db, foreign.Workspace, seed.Document, 2,
          Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(1)));
      await tx.RollbackAsync();
    }
    Assert.Equal(0, await db.FileOperations.CountAsync(op => op.Kind == FileOperationKind.Delete));
    Assert.NotNull((await db.Documents.AsNoTracking().SingleAsync(d => d.Id == seed.Document)).DeletedAt);
  }

  [Fact]
  public async Task TrashedDocumentStillOccupiesItsOriginalTopic()
  {
    await using var db = await CreateDatabaseAsync();
    var seed = await SeedAsync(db);
    var topic = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name) VALUES ({topic}, {seed.Workspace}, 'Original topic');
        UPDATE minidocs.documents SET topic_id = {topic} WHERE id = {seed.Document};
        """);
    await TrashAsync(db, seed);
    var error = await Assert.ThrowsAsync<PostgresException>(() =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM minidocs.topics WHERE workspace_id = {seed.Workspace} AND id = {topic}
            """));
    // PostgreSQL 18 reports RESTRICT foreign-key actions as restrict_violation.
    Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
    Assert.Equal("FK_documents_topics_workspace_id_topic_id", error.ConstraintName);
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      Assert.True(await TrashPersistence.TryRestoreAsync(db, seed.Workspace, seed.Document, 2));
      await tx.CommitAsync();
    }
    Assert.Equal(topic, (await db.Documents.AsNoTracking().SingleAsync()).TopicId);
  }

  private static async Task<bool> TryStartPurgeAsync(MiniDocsDbContext db, Seed seed, Guid operation) =>
      await TrashPersistence.TryBeginPurgeAsync(db, seed.Workspace, seed.Document, 2,
          operation, Guid.NewGuid(), TimeSpan.FromMinutes(1)) is not null;

  private static async Task TrashAsync(MiniDocsDbContext db, Seed seed)
  {
    await using var tx = await db.Database.BeginTransactionAsync();
    Assert.True(await TrashPersistence.TryTrashAsync(db, seed.Workspace, seed.Document, 1));
    await tx.CommitAsync();
  }

  private static async Task<PurgeReservation> BeginPurgeAsync(MiniDocsDbContext db, Seed seed, Guid operation)
  {
    await using var tx = await db.Database.BeginTransactionAsync();
    var reservation = await TrashPersistence.TryBeginPurgeAsync(db, seed.Workspace, seed.Document,
        2, operation, Guid.NewGuid(), TimeSpan.FromMinutes(1));
    Assert.NotNull(reservation);
    Assert.Equal(seed.Remote, reservation.GetValueOrDefault().RemoteFileId);
    await tx.CommitAsync();
    return reservation.GetValueOrDefault();
  }

  private async Task<MiniDocsDbContext> CreateDatabaseAsync()
  {
    var db = await fixture.CreateDatabaseAsync();
    try { await db.Database.MigrateAsync(); return db; }
    catch { await db.DisposeAsync(); throw; }
  }

  private sealed record Seed(Guid Workspace, Guid Document, Guid Topic, Guid Remote, Guid Request);

  private static async Task<Seed> SeedAsync(MiniDocsDbContext db)
  {
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertActiveWorkspaceAsync(db, Guid.NewGuid(), workspace, $"trash-{workspace:N}");
    var document = Guid.NewGuid();
    var remote = Guid.NewGuid();
    var request = Guid.NewGuid();
    var tag = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability,
            via_file_id, transcript_present, transcript_revision, page_count, page_count_source)
        VALUES ({document}, {workspace}, {topic}, 'Test PDF', 'meeting', 'available', {remote}, true, 1, 1, 'pdf_import');
        INSERT INTO minidocs.transcript_pages(workspace_id, document_id, page_no, text, edited)
        VALUES ({workspace}, {document}, 1, 'Retained text', false);
        INSERT INTO minidocs.tags(id, workspace_id, name, name_key, description, color)
        VALUES ({tag}, {workspace}, 'Tag', 'tag', '', '#123456');
        INSERT INTO minidocs.document_tags(workspace_id, document_id, tag_id)
        VALUES ({workspace}, {document}, {tag});
        INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, attempt_no,
            request_id, remote_file_id, completed_at, verified_size, verified_sha256)
        VALUES ({Guid.NewGuid()}, {workspace}, {document}, 'upload', 'completed', 1, {request},
            {remote}, clock_timestamp(), 3, {System.Security.Cryptography.SHA256.HashData(new byte[] {1,2,3})});
        """);
    return new Seed(workspace, document, topic, remote, request);
  }

}
