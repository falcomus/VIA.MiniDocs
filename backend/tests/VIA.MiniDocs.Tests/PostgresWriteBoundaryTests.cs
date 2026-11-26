using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class PostgresWriteBoundaryTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  [Fact]
  public async Task RuntimeHasOnlyExplicitCommandsAndNoProtectedDml()
  {
    await using var db = await OpenDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(db);

    await PostgresTestDatabase.VerifyPermissionsAsync(db);

    await DeniedAsync(runtime, "DELETE FROM minidocs.documents");
    await DeniedAsync(runtime, "DELETE FROM minidocs.file_operations");
    await DeniedAsync(runtime, "DELETE FROM minidocs.file_operation_receipts");
    await DeniedAsync(runtime, "UPDATE minidocs.file_operations SET state = 'prepared'");
    await DeniedAsync(runtime, "UPDATE minidocs.file_operations SET state = 'failed'");
    await DeniedAsync(runtime, "UPDATE minidocs.workspaces SET status = 'active'");
    await DeniedAsync(runtime, "UPDATE minidocs.user_invitations SET redeemed_at = NULL");
    await DeniedAsync(runtime, "SELECT token_hash FROM minidocs.user_invitations");
    await DeniedAsync(runtime, "UPDATE minidocs.identity_users SET is_disabled = true");
    await DeniedAsync(runtime, "SET ROLE minidocs_command_owner");
    await DeniedAsync(runtime, "SELECT minidocs.lock_workspace(NULL::uuid)");

    var seed = await SeedDocumentAsync(db);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE SCHEMA fixture_shadow;
        CREATE TABLE fixture_shadow.documents(id uuid);
        GRANT USAGE ON SCHEMA fixture_shadow TO minidocs_runtime
        """);
    await using var tx = await runtime.Database.BeginTransactionAsync();
    await runtime.Database.ExecuteSqlRawAsync("SET LOCAL search_path = fixture_shadow, pg_catalog");
    Assert.True(await DocumentPersistence.TryRenameAsync(runtime, seed.Workspace, seed.Document, 1, "Qualified"));
    await tx.CommitAsync();
    Assert.Equal("Qualified", (await db.Documents.AsNoTracking().SingleAsync()).Title);
  }

  [Fact]
  public async Task TrackedUpdateCannotBypassVersionAndCommandRejectsStaleVersion()
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    await using var runtime = fixture.CreateRuntimePeer(db);
    var document = await runtime.Documents.SingleAsync();
    document.Title = "Unversioned EF update";
    var error = await Assert.ThrowsAsync<DbUpdateException>(() => runtime.SaveChangesAsync());
    Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    runtime.ChangeTracker.Clear();

    var before = await runtime.Documents.AsNoTracking().SingleAsync();
    var changed = await LocalWriteTransaction.ExecuteAsync(runtime, ct =>
        DocumentPersistence.TryRenameAsync(runtime, seed.Workspace, seed.Document, 1, "Versioned", ct), value => value);
    Assert.Equal(LocalWriteStatus.Committed, changed.Status);
    var stale = await LocalWriteTransaction.ExecuteAsync(runtime, ct =>
        DocumentPersistence.TryRenameAsync(runtime, seed.Workspace, seed.Document, 1, "Stale", ct), value => value);
    Assert.Equal(LocalWriteStatus.Rejected, stale.Status);
    var after = await runtime.Documents.AsNoTracking().SingleAsync();
    Assert.Equal("Versioned", after.Title);
    Assert.Equal(2, after.Version);
    Assert.Equal(before.CreatedAt, after.CreatedAt);
    Assert.True(after.UpdatedAt >= before.UpdatedAt);
  }

  [Theory]
  [InlineData("INSERT INTO minidocs.transcript_pages SELECT * FROM minidocs.transcript_pages WHERE false")]
  [InlineData("UPDATE minidocs.transcript_pages SET text = 'unversioned'")]
  [InlineData("DELETE FROM minidocs.transcript_pages")]
  public async Task AlternativePageWritesAreDeniedWhileReplaceAllHoldsTheWorkspace(string sql)
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    await using var writer = fixture.CreateRuntimePeer(db);
    await using var other = fixture.CreateRuntimePeer(db);
    await using var tx = await writer.Database.BeginTransactionAsync();
    Assert.True(await TranscriptPersistence.TryReplaceAsync(writer, seed.Workspace, seed.Document, 0,
        true, false, [new(1, "current", false)]));
    await DeniedAsync(other, sql).WaitAsync(TimeSpan.FromSeconds(5));
    await tx.CommitAsync();

    await using var stale = await other.Database.BeginTransactionAsync();
    Assert.False(await TranscriptPersistence.TryReplaceAsync(other, seed.Workspace, seed.Document, 0,
        true, false, [new(1, "stale", false)]));
    await stale.RollbackAsync();
    Assert.Equal("current", (await other.TranscriptPages.AsNoTracking().SingleAsync()).Text);
    Assert.Equal(1, (await other.Documents.AsNoTracking().SingleAsync()).TranscriptRevision);
  }

  [Theory]
  [InlineData("unknown", "prepared")]
  [InlineData("unknown", "failed")]
  [InlineData("completed", "prepared")]
  public async Task InvalidUploadTransitionsAreRejectedEvenByTheLegacyTablePath(string from, string to)
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    var operation = await SeedUploadAsync(db, seed, from);
    var failure = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operations
        SET state = {to}, attempt_no = 0, completed_at = NULL, remote_file_id = NULL,
          verified_size = NULL, verified_sha256 = NULL
        WHERE id = {operation}
        """));
    Assert.Equal("ck_file_state_transition", failure.ConstraintName);
  }

  [Theory]
  [InlineData("in_flight", null, null, null)]
  [InlineData("remote_known", null, null, null)]
  [InlineData("completed", "11111111-1111-1111-1111-111111111111", null, null)]
  [InlineData("completed", "11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", 5)]
  public async Task InconsistentLifecycleRowsFailAtTheDatabase(string state, string? remoteId, string? owner, int? size)
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    var failure = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, request_id, attempt_no,
          remote_file_id, lease_owner, lease_until, completed_at, verified_size, verified_sha256)
        VALUES ({Guid.NewGuid()}, {seed.Workspace}, {seed.Document}, 'upload', {state}, {Guid.NewGuid()}, 1,
          {remoteId}::uuid, {owner}::uuid,
          CASE WHEN {owner}::text IS NOT NULL THEN clock_timestamp() + interval '1 minute' END,
          CASE WHEN {state} = 'completed' THEN clock_timestamp() END,
          {size}::bigint, CASE WHEN {size}::integer IS NOT NULL THEN decode(repeat('00',32),'hex') END)
        """));
    Assert.Equal("ck_file_lifecycle", failure.ConstraintName);
  }

  [Fact]
  public async Task RealIdentityStoreCanRegisterAndUpdateWithoutAdministrativeDml()
  {
    await using var db = await OpenDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(db);
    var admin = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(db, admin, "identity-test-admin", "admin_only");
    var hash = System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray());
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.user_invitations(id, token_hash, created_by, expires_at)
        VALUES ({Guid.NewGuid()}, {hash}, {admin}, clock_timestamp() + interval '1 hour')
        """);
    var user = new MiniDocsUser { Id = Guid.NewGuid(), UserName = "new-user", NormalizedUserName = "NEW-USER" };
    using var store = new UserStore<MiniDocsUser, IdentityRole<Guid>, MiniDocsDbContext, Guid>(runtime);
    var workspace = Guid.NewGuid();
    var registration = await LocalWriteTransaction.ExecuteAsync(runtime, async ct =>
    {
      Assert.True(await InvitationPersistence.TryLockValidAsync(runtime, hash, ct));
      Assert.True((await store.CreateAsync(user, ct)).Succeeded);
      return await InvitationPersistence.TryRegisterWorkspaceAsync(runtime, hash, user.Id, workspace, Guid.NewGuid(), ct);
    }, value => value);
    Assert.Equal(LocalWriteStatus.Committed, registration.Status);
    user.Email = "register@example.invalid";
    Assert.True((await store.UpdateAsync(user)).Succeeded);
    Assert.Equal("register@example.invalid", (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).Email);
    Assert.Equal(workspace, (await db.Workspaces.AsNoTracking().SingleAsync()).Id);
  }

  [Fact]
  public async Task IndependentWorkspaceCanWriteWhileAnotherIsLocked()
  {
    await using var db = await OpenDatabaseAsync();
    var first = await SeedDocumentAsync(db);
    var second = await SeedDocumentAsync(db);
    await using var left = fixture.CreateRuntimePeer(db);
    await using var right = fixture.CreateRuntimePeer(db);
    await using var hold = await left.Database.BeginTransactionAsync();
    Assert.True(await DocumentPersistence.TryRenameAsync(left, first.Workspace, first.Document, 1, "Locked"));
    await using var parallel = await right.Database.BeginTransactionAsync();
    Assert.True(await DocumentPersistence.TryRenameAsync(right, second.Workspace, second.Document, 1, "Independent")
        .WaitAsync(TimeSpan.FromSeconds(5)));
    await parallel.CommitAsync();
    await hold.RollbackAsync();
    Assert.Equal("Independent", (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == second.Document)).Title);
    Assert.Equal("PDF", (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == first.Document)).Title);
  }

  [Fact]
  public async Task SerializationFailureReturnsConflictAndRollsBackEarlierWork()
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    var independent = await SeedDocumentAsync(db);
    await using var old = fixture.CreateRuntimePeer(db);
    await using var fresh = fixture.CreateRuntimePeer(db);

    var result = await LocalWriteTransaction.ExecuteAsync(old, async ct =>
    {
      Assert.True(await DocumentPersistence.TryRenameAsync(old, independent.Workspace, independent.Document,
          1, "Must roll back", ct));
      var commit = await LocalWriteTransaction.ExecuteAsync(fresh,
          token => DocumentPersistence.TryRenameAsync(fresh, seed.Workspace, seed.Document, 1, "Committed", token),
          value => value, cancellationToken: ct);
      Assert.Equal(LocalWriteStatus.Committed, commit.Status);
      return await DocumentPersistence.TryRenameAsync(old, seed.Workspace, seed.Document, 1, "Stale", ct);
    }, value => value, IsolationLevel.RepeatableRead);

    Assert.Equal(LocalWriteStatus.ConcurrencyConflict, result.Status);
    Assert.Equal("PDF", (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == independent.Document)).Title);
    Assert.Equal("Committed", (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == seed.Document)).Title);
  }

  [Fact]
  public async Task UpgradeClosesLegacyTableAndColumnGrants()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(db);
    await using var migratorDb = fixture.CreateMigratorPeer(db);
    var migrator = migratorDb.GetService<IMigrator>();
    await migrator.MigrateAsync("20261126134926_InitialD2Model");
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "legacy");
    await db.Database.ExecuteSqlRawAsync("""
        GRANT UPDATE ON minidocs.workspaces TO minidocs_runtime;
        GRANT UPDATE(title) ON minidocs.documents TO minidocs_runtime;
        GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA minidocs TO minidocs_runtime
        """);

    // This state committed on the original schema. The upgrade must reject it
    // atomically instead of manufacturing a historical provisioning completion.
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.via_docs_accounts(id, workspace_id, application_name,
            application_secret_protected, application_id, verified_at)
        VALUES ({Guid.NewGuid()}, {workspace}, 'legacy-account', decode('01','hex'), {Guid.NewGuid()}, clock_timestamp());
        UPDATE minidocs.workspaces SET status = 'active' WHERE id = {workspace}
        """);
    var invalid = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
    Assert.Equal("ck_workspace_provisioning_complete", invalid.ConstraintName);
    Assert.Single(await db.Database.GetAppliedMigrationsAsync());
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.workspaces SET status = 'pending' WHERE id = {workspace};
        DELETE FROM minidocs.via_docs_accounts WHERE workspace_id = {workspace}
        """);
    await migrator.MigrateAsync("20261126170000_EnforceWriteBoundaries");
    await migrator.MigrateAsync();
    await PostgresTestDatabase.VerifyPermissionsAsync(db);

    await using var runtime = fixture.CreateRuntimePeer(db);
    Assert.Equal(workspace, (await runtime.Workspaces.AsNoTracking().SingleAsync()).Id);
    await DeniedAsync(runtime, "UPDATE minidocs.documents SET title = 'Legacy column grant'");
    await DeniedAsync(runtime, "UPDATE minidocs.workspaces SET status = 'active'");
    await DeniedAsync(runtime, "SELECT minidocs.serialize_workspace(NULL::uuid)");
  }


  [Fact]
  public async Task DeadlockReturnsConflictAndRollsBackEarlierWork()
  {
    await using var db = await OpenDatabaseAsync();
    var seedA = await SeedDocumentAsync(db);
    var seedB = await SeedDocumentAsync(db);
    await using var contender = fixture.CreatePeer(db);
    await using var other = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var blocker = await other.Database.BeginTransactionAsync();
    // Let the contender be the first session to detect the deliberately inverted order.
    await other.Database.ExecuteSqlRawAsync("SET LOCAL deadlock_timeout = '5s'");
    Assert.True(await DocumentPersistence.TryRenameAsync(other, seedB.Workspace, seedB.Document, 1, "Other wins"));

    var pid = 0;
    var result = LocalWriteTransaction.ExecuteAsync(contender, async ct =>
    {
      await contender.Database.ExecuteSqlRawAsync("SET LOCAL deadlock_timeout = '500ms'", ct);
      Assert.True(await DocumentPersistence.TryRenameAsync(contender, seedA.Workspace, seedA.Document, 1, "Must roll back", ct));
      pid = await contender.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync(ct);
      return await DocumentPersistence.TryRenameAsync(contender, seedB.Workspace, seedB.Document, 1, "Deadlocked", ct);
    }, value => value);
    // Wait until PostgreSQL actually reports the contender waiting on seed B.
    await PostgresWait.ForBlockedAsync(observer, () => Volatile.Read(ref pid), result);
    Assert.True(await DocumentPersistence.TryRenameAsync(other, seedA.Workspace, seedA.Document, 1, "Committed"));
    await blocker.CommitAsync();

    Assert.Equal(LocalWriteStatus.ConcurrencyConflict, (await result).Status);
    Assert.Equal("Committed", (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == seedA.Document)).Title);
    Assert.Equal("Other wins", (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == seedB.Document)).Title);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ReservationCannotRaceRetirementInEitherOrder(bool retireFirst)
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    var operation = await SeedUploadAsync(db, seed, "completed");
    var request = (await db.FileOperations.AsNoTracking().SingleAsync()).RequestId!.Value;
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operations SET completed_at = clock_timestamp() - interval '31 days'
        WHERE id = {operation}
        """);
    await using var first = fixture.CreateRuntimePeer(db);
    await using var second = fixture.CreateRuntimePeer(db);
    await using var tx1 = await first.Database.BeginTransactionAsync();
    await using var tx2 = await second.Database.BeginTransactionAsync();
    var pid = await second.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync();
    Task<bool> Reserve(MiniDocsDbContext context) => OperationLeasePersistence.TryReserveUploadAsync(
        context, seed.Workspace, Guid.NewGuid(), seed.Topic, Guid.NewGuid(), request, "Duplicate", DocumentKind.Meeting);
    Task<int> Retire(MiniDocsDbContext context) => OperationRetentionPersistence.RetireCompletedUploadsAsync(context, seed.Workspace, 1);

    if (retireFirst)
      Assert.Equal(1, await Retire(first));
    else
      Assert.False(await Reserve(first));
    Task rival = retireFirst ? Reserve(second) : Retire(second);
    await PostgresWait.ForBlockedAsync(db, () => pid, rival);
    await tx1.CommitAsync();
    if (retireFirst)
      Assert.False(await (Task<bool>)rival);
    else
      Assert.Equal(1, await (Task<int>)rival);
    await tx2.CommitAsync();

    Assert.Equal(request, (await db.UploadRequestTombstones.AsNoTracking().SingleAsync()).RequestId);
    Assert.Empty(await db.FileOperations.AsNoTracking().ToListAsync());
    Assert.Single(await db.Documents.AsNoTracking().ToListAsync());
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RealDatabaseFailureMidCommandRollsBackAllWrites(bool purge)
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    await using var runtime = fixture.CreateRuntimePeer(db);
    var worker = Guid.NewGuid();
    var operation = Guid.NewGuid();
    var remote = Guid.NewGuid();
    var digest = new byte[32];
    OperationFence fence = default;
    PurgeReservation reservation = default;

    if (purge)
    {
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.documents SET via_file_id = {remote}, availability = 'available' WHERE id = {seed.Document}
          """);
      await SeedUploadAsync(db, seed, "completed");
      await using var prepare = await runtime.Database.BeginTransactionAsync();
      Assert.True(await TrashPersistence.TryTrashAsync(runtime, seed.Workspace, seed.Document, 1));
      reservation = Assert.IsType<PurgeReservation>(await TrashPersistence.TryBeginPurgeAsync(
          runtime, seed.Workspace, seed.Document, 2, operation, worker, TimeSpan.FromMinutes(1)));
      await prepare.CommitAsync();
    }
    else
    {
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, request_id,
            attempt_no, remote_file_id, lease_owner, lease_until, verified_size, verified_sha256)
          VALUES ({operation}, {seed.Workspace}, {seed.Document}, 'upload', 'remote_known', {Guid.NewGuid()},
            1, {remote}, {worker}, clock_timestamp() + interval '1 minute', 3, {digest})
          """);
      fence = new(worker, 1);
    }

    await db.Database.ExecuteSqlRawAsync("""
        CREATE FUNCTION minidocs.test_reject_document_mutation() RETURNS trigger
        LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
        BEGIN RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Controlled fixture fault'; END;
        $$;
        CREATE TRIGGER test_reject_document_mutation BEFORE UPDATE OR DELETE ON minidocs.documents
        FOR EACH ROW EXECUTE FUNCTION minidocs.test_reject_document_mutation()
        """);
    var result = await Assert.ThrowsAsync<PostgresException>(() => LocalWriteTransaction.ExecuteAsync(runtime,
        ct => purge
            ? TrashPersistence.TryCompleteConfirmedPurgeAsync(runtime, seed.Workspace, seed.Document, operation, reservation, ct)
            : OperationLeasePersistence.TryCompleteUploadAsync(runtime, seed.Workspace, seed.Document, operation, 1, fence, remote, 3, digest, ct),
        value => value));
    Assert.Equal("P0001", result.SqlState);

    var saved = await db.FileOperations.AsNoTracking().SingleAsync(op => op.Id == operation);
    var document = await db.Documents.AsNoTracking().SingleAsync();
    Assert.Equal(purge ? FileOperationState.DeleteInFlight : FileOperationState.RemoteKnown, saved.State);
    Assert.Equal(purge ? DocumentAvailability.DeletePending : DocumentAvailability.PendingUpload, document.Availability);
    Assert.Empty(await db.FileOperationReceipts.AsNoTracking().ToListAsync());
    Assert.Empty(await db.UploadRequestTombstones.AsNoTracking().ToListAsync());
  }


  [Fact]
  public async Task TagAssignmentsAndDeletionInvalidateOnlyAffectedDocumentVersions()
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    var unrelated = Guid.NewGuid();
    var tag = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
        VALUES ({unrelated}, {seed.Workspace}, {seed.Topic}, 'Unrelated', 'meeting', 'pending_upload');
        INSERT INTO minidocs.tags(id, workspace_id, name, name_key, description, color)
        VALUES ({tag}, {seed.Workspace}, 'Example', 'example', '', '#123456');
        """);
    await using var runtime = fixture.CreateRuntimePeer(db);
    await using var tx = await runtime.Database.BeginTransactionAsync();
    Assert.True(await DocumentPersistence.TrySetTagAsync(runtime, seed.Workspace, seed.Document, 1, tag, true));
    Assert.False(await DocumentPersistence.TrySetTagAsync(runtime, seed.Workspace, seed.Document, 1, tag, true));
    Assert.True(await DocumentPersistence.TrySetTagAsync(runtime, seed.Workspace, seed.Document, 2, tag, true));
    Assert.True(await DocumentPersistence.TryDeleteTagAsync(runtime, seed.Workspace, tag, 1));
    await tx.CommitAsync();

    Assert.Empty(await db.DocumentTags.AsNoTracking().ToListAsync());
    Assert.Equal(3, (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == seed.Document)).Version);
    Assert.Equal(1, (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == unrelated)).Version);
  }

  [Fact]
  public async Task PendingAssignmentChecksBothRevisionsAndPageCount()
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    var pending = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.pending_transcripts(id, workspace_id, file_name, imported_at, status)
        VALUES ({pending}, {seed.Workspace}, 'pending.pdf', clock_timestamp(), 'pending');
        INSERT INTO minidocs.pending_transcript_pages(workspace_id, pending_id, page_no, text)
        VALUES ({seed.Workspace}, {pending}, 2, 'Imported page');
        """);
    await using var runtime = fixture.CreateRuntimePeer(db);
    await using var tx = await runtime.Database.BeginTransactionAsync();
    Assert.True(await DocumentPersistence.TryChangePageCountAsync(runtime, seed.Workspace, seed.Document,
        1, 2, PageCountSource.Viewer));
    Assert.False(await TranscriptPersistence.TryAssignPendingAsync(runtime, seed.Workspace, seed.Document,
        1, pending, 1, false));
    Assert.False(await TranscriptPersistence.TryAssignPendingAsync(runtime, seed.Workspace, seed.Document,
        0, pending, 2, false));
    Assert.True(await TranscriptPersistence.TryAssignPendingAsync(runtime, seed.Workspace, seed.Document,
        0, pending, 1, false));
    Assert.False(await TranscriptPersistence.TryAssignPendingAsync(runtime, seed.Workspace, seed.Document,
        1, pending, 2, true));
    Assert.False(await DocumentPersistence.TryChangePageCountAsync(runtime, seed.Workspace, seed.Document,
        3, 1, PageCountSource.Viewer));
    await tx.CommitAsync();

    var page = await db.TranscriptPages.AsNoTracking().SingleAsync();
    Assert.Equal(2, page.PageNo);
    Assert.Equal("Imported page", page.Text);
    Assert.Equal(PendingTranscriptStatus.Matched, (await db.PendingTranscripts.AsNoTracking().SingleAsync()).Status);
  }

  [Fact]
  public async Task RuntimeTopicCommandsRejectCyclesAndCannotDeleteNewAssignment()
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name)
        VALUES ({first}, {seed.Workspace}, 'First'), ({second}, {seed.Workspace}, 'Second');
        UPDATE minidocs.documents SET availability = 'available', via_file_id = {Guid.NewGuid()} WHERE id = {seed.Document};
        """);
    await using var runtime = fixture.CreateRuntimePeer(db);
    await using var tx = await runtime.Database.BeginTransactionAsync();
    Assert.True(await TopicPersistence.TryMoveAsync(runtime, seed.Workspace, first, 1, second));
    Assert.False(await TopicPersistence.TryMoveAsync(runtime, seed.Workspace, second, 1, first));
    Assert.True(await DocumentPersistence.TryMoveAsync(runtime, seed.Workspace, seed.Document, 1, first));
    Assert.False(await TopicPersistence.TryDeleteAsync(runtime, seed.Workspace, first, 2));
    Assert.False(await TopicPersistence.TryDeleteAsync(runtime, seed.Workspace, seed.Topic, 1));
    await tx.CommitAsync();
    Assert.Equal(first, (await db.Documents.AsNoTracking().SingleAsync()).TopicId);
  }

  [Fact]
  public async Task DeletingCompletedDeleteWithoutReceiptViolatesConstraint()
  {
    await using var db = await OpenDatabaseAsync();
    var seed = await SeedDocumentAsync(db);
    var operation = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state,
            attempt_no, remote_file_id, completed_at)
        VALUES ({operation}, {seed.Workspace}, {seed.Document}, 'delete', 'completed', 1, {Guid.NewGuid()}, clock_timestamp())
        """);
    var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.file_operations WHERE id = {operation}
        """));
    Assert.Equal("ck_completed_delete_receipt", error.ConstraintName);
  }


  private async Task<MiniDocsDbContext> OpenDatabaseAsync()
  {
    var db = await fixture.CreateDatabaseAsync();
    try
    {
      await fixture.PrepareRuntimeAsync(db);
      await using var migrator = fixture.CreateMigratorPeer(db);
      await migrator.Database.MigrateAsync();
      await PostgresTestDatabase.VerifyPermissionsAsync(db);
      return db;
    }
    catch
    {
      await db.DisposeAsync();
      throw;
    }
  }

  private static async Task<Seed> SeedDocumentAsync(MiniDocsDbContext db)
  {
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertActiveWorkspaceAsync(db, Guid.NewGuid(), workspace, $"boundary-{workspace:N}");
    var document = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
        VALUES ({document}, {workspace}, {topic}, 'PDF', 'meeting', 'pending_upload')
        """);
    return new(workspace, topic, document);
  }

  private static async Task<Guid> SeedUploadAsync(MiniDocsDbContext db, Seed seed, string state)
  {
    var operation = Guid.NewGuid();
    // A privileged transition fixture still needs a consistent terminal upload.
    if (state == "completed")
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.documents
          SET via_file_id = coalesce(via_file_id, gen_random_uuid()), availability = 'available'
          WHERE id = {seed.Document} AND workspace_id = {seed.Workspace}
          """);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, request_id, attempt_no,
          remote_file_id, completed_at, verified_size, verified_sha256)
        VALUES ({operation}, {seed.Workspace}, {seed.Document}, 'upload', {state}, {Guid.NewGuid()}, 1,
          CASE WHEN {state} = 'completed' THEN
            (SELECT via_file_id FROM minidocs.documents WHERE id = {seed.Document}) END,
          CASE WHEN {state} = 'completed' THEN clock_timestamp() END,
          CASE WHEN {state} = 'completed' THEN 1 END,
          CASE WHEN {state} = 'completed' THEN decode(repeat('00',32),'hex') END)
        """);
    return operation;
  }

  private static async Task DeniedAsync(MiniDocsDbContext db, string sql)
  {
    var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
    Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
  }

  private sealed record Seed(Guid Workspace, Guid Topic, Guid Document);
}
