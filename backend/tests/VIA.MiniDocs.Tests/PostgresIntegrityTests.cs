using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class PostgresIntegrityTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  [Fact]
  public async Task RegularUserCannotCommitWithoutWorkspace()
  {
    await using var db = await CreateMigratedDatabaseAsync();
    await using var transaction = await db.Database.BeginTransactionAsync();
    await PostgresSeed.InsertUserAsync(db, Guid.NewGuid(), "orphan", "regular");
    await RejectAsync(() => transaction.CommitAsync(), "ck_user_workspace_cardinality");
  }

  [Fact]
  public async Task AdminOnlyUserCannotOwnWorkspace()
  {
    await using var db = await CreateMigratedDatabaseAsync();
    var user = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(db, user, "admin", "admin_only");
    await using var transaction = await db.Database.BeginTransactionAsync();
    await PostgresSeed.InsertWorkspaceAsync(db, user, Guid.NewGuid());
    await RejectAsync(() => transaction.CommitAsync(), "ck_user_workspace_cardinality");
  }

  [Fact]
  public async Task WorkspaceRequiresSystemTopicAtCommit()
  {
    await using var db = await CreateMigratedDatabaseAsync();
    await using var transaction = await db.Database.BeginTransactionAsync();
    var user = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(db, user, "no-system", "regular");
    await PostgresSeed.InsertWorkspaceAsync(db, user, Guid.NewGuid());
    await RejectAsync(() => transaction.CommitAsync(), "ck_workspace_system_topic");
  }

  [Fact]
  public async Task OwnerAndSystemTopicAreProtected()
  {
    await using var db = await CreateMigratedDatabaseAsync();
    var owner = Guid.NewGuid();
    var workspace = Guid.NewGuid();
    var systemTopic = await PostgresSeed.InsertUserAndWorkspace(db, owner, workspace, "protected");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.identity_users SET account_kind = 'admin_only' WHERE id = {owner}
        """), "ck_user_workspace_cardinality");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.identity_users WHERE id = {owner}
        """), "ck_user_deletion_protected");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.workspaces WHERE id = {workspace}
        """), "ck_workspace_deletion_protected");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.topics SET name = 'Renamed' WHERE id = {systemTopic}
        """), "ck_topic_system_protected");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.topics WHERE id = {systemTopic}
        """), "ck_topic_system_protected");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, parent_id, name)
        VALUES ({Guid.NewGuid()}, {workspace}, {systemTopic}, 'Invalid child')
        """), "ck_topic_no_system_children");
  }

  [Fact]
  public async Task ActiveWorkspaceRequiresAndRetainsVerifiedTechnicalIdentity()
  {
    await using var db = await CreateMigratedDatabaseAsync();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "active");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.workspaces SET status = 'active' WHERE id = {workspace}
        """), "ck_workspace_verified_account");
    var account = Guid.NewGuid();
    var application = Guid.NewGuid();
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.via_docs_accounts(id, workspace_id, application_name,
            application_secret_protected, application_id, verified_at)
        VALUES ({account}, {workspace}, 'test', decode('01','hex'), {application}, clock_timestamp());
        UPDATE minidocs.workspaces SET status = 'active' WHERE id = {workspace}
        """), "ck_workspace_provisioning_complete");
    await using (var transaction = await db.Database.BeginTransactionAsync())
    {
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.workspaces SET status = 'active' WHERE id = {workspace}
          """);
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO minidocs.via_docs_accounts(id, workspace_id, application_name,
              application_secret_protected, application_id, verified_at)
          VALUES ({account}, {workspace}, 'test', decode('01','hex'), {application}, clock_timestamp());
          INSERT INTO minidocs.provisioning_operations(id, workspace_id, status, attempt_no, candidate_application_id)
          VALUES ({Guid.NewGuid()}, {workspace}, 'active', 1, {application})
          """);
      await transaction.CommitAsync();
    }
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.via_docs_accounts SET application_id = NULL, verified_at = NULL WHERE id = {account}
        """), "ck_prepared_technical_identity");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.via_docs_accounts WHERE id = {account}
        """), "ck_workspace_verified_account");
  }

  [Fact]
  public async Task MultiRowTopicMoveCannotCreateCycle()
  {
    await using var db = await CreateMigratedDatabaseAsync();
    var (workspace, first, second) = await SeedTopicsAsync(db);
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.topics SET parent_id = CASE WHEN id = {first} THEN {second} ELSE {first} END
        WHERE workspace_id = {workspace} AND id IN ({first}, {second})
        """), "ck_topic_tree_acyclic");
    Assert.Equal(0, await db.Topics.ObserveAllWorkspaces().CountAsync(topic => topic.WorkspaceId == workspace && topic.ParentId != null));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ConcurrentTopicMovesCannotCommitCycle(bool repeatableRead)
  {
    await using var db = await CreateMigratedDatabaseAsync();
    var (_, first, second) = await SeedTopicsAsync(db);
    await using var peer = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var tx1 = await db.Database.BeginTransactionAsync();
    await using var tx2 = await peer.Database.BeginTransactionAsync(
        repeatableRead ? IsolationLevel.RepeatableRead : IsolationLevel.ReadCommitted);
    var pid = await peer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.topics SET parent_id = {second} WHERE id = {first}
        """);
    var move = peer.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.topics SET parent_id = {first} WHERE id = {second}
        """);
    try
    {
      await PostgresWait.ForBlockedAsync(observer, pid, move);
      await tx1.CommitAsync();
    }
    catch
    {
      await tx1.RollbackAsync();
      try { await move; } catch (PostgresException) { }
      throw;
    }
    if (repeatableRead)
    {
      var error = await Assert.ThrowsAsync<PostgresException>(() => move);
      Assert.Equal(PostgresErrorCodes.SerializationFailure, error.SqlState);
    }
    else
    {
      await move;
      await RejectAsync(() => tx2.CommitAsync(), "ck_topic_tree_acyclic");
    }
    Assert.Equal(1, await observer.Topics.ObserveAllWorkspaces().CountAsync(topic => topic.ParentId != null));
  }

  [Fact]
  public async Task UploadRequestCannotBeReservedAndRetiredAtSameTime()
  {
    await using var db = await CreateMigratedDatabaseAsync();
    var (workspace, document) = await SeedDocumentAsync(db);
    var operation = Guid.NewGuid();
    var request = Guid.NewGuid();
    await InsertUploadAsync(db, workspace, document, operation, request);
    await RejectAsync(() => InsertTombstoneAsync(db, workspace, request), "ck_upload_request_exclusive");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.file_operations WHERE id = {operation}
        """), "ck_file_operation_unresolved");
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.file_operations SET state = 'failed' WHERE id = {operation}
        """);
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.file_operations WHERE id = {operation}
        """), "ck_upload_request_retained");
    await using (var transaction = await db.Database.BeginTransactionAsync())
    {
      await InsertTombstoneAsync(db, workspace, request);
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          DELETE FROM minidocs.file_operations WHERE id = {operation}
          """);
      await transaction.CommitAsync();
    }
    await RejectAsync(() => InsertUploadAsync(db, workspace, document, Guid.NewGuid(), request), "ck_upload_request_exclusive");
    await RejectAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.upload_request_tombstones WHERE workspace_id = {workspace} AND request_id = {request}
        """), "ck_upload_tombstone_permanent");
  }

  [Fact]
  public async Task ConcurrentUploadReservationCannotReuseRetiredRequest()
  {
    await using var db = await CreateMigratedDatabaseAsync();
    var (workspace, document) = await SeedDocumentAsync(db);
    var request = Guid.NewGuid();
    await using var peer = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var tx1 = await db.Database.BeginTransactionAsync();
    await using var tx2 = await peer.Database.BeginTransactionAsync();
    var pid = await peer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
    await InsertTombstoneAsync(db, workspace, request);
    var reserve = InsertUploadAsync(peer, workspace, document, Guid.NewGuid(), request);
    try
    {
      await PostgresWait.ForBlockedAsync(observer, pid, reserve);
      await tx1.CommitAsync();
    }
    catch
    {
      await tx1.RollbackAsync();
      try { await reserve; } catch (PostgresException) { }
      throw;
    }
    await reserve;
    await RejectAsync(() => tx2.CommitAsync(), "ck_upload_request_exclusive");
    Assert.Equal(1, await observer.UploadRequestTombstones.ObserveAllWorkspaces().CountAsync());
    Assert.Equal(0, await observer.FileOperations.ObserveAllWorkspaces().CountAsync());
  }

  [Fact]
  public async Task InitialMigrationCanBeReversedAndReappliedWithItsIntegrityFunctions()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261126134926_InitialD2Model");
    await migrator.MigrateAsync("0");
    var functions = await db.Database.SqlQueryRaw<int>("""
        SELECT count(*)::integer AS "Value" FROM pg_catalog.pg_proc p
        JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'minidocs' AND p.proname IN ('serialize_workspace', 'check_topic_tree', 'check_upload_reservation')
        """).SingleAsync();
    Assert.Equal(0, functions);
    await migrator.MigrateAsync();
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), Guid.NewGuid(), "reapplied");
    await RejectAsync(() => PostgresSeed.InsertUserAsync(db, Guid.NewGuid(), "orphan-after-reapply", "regular"),
        "ck_user_workspace_cardinality");
  }

  private async Task<MiniDocsDbContext> CreateMigratedDatabaseAsync()
  {
    var db = await fixture.CreateDatabaseAsync();
    try
    {
      await db.Database.MigrateAsync();
      return db;
    }
    catch
    {
      await db.DisposeAsync();
      throw;
    }
  }

  private static async Task<(Guid Workspace, Guid First, Guid Second)> SeedTopicsAsync(MiniDocsDbContext db)
  {
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "topics");
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name)
        VALUES ({first}, {workspace}, 'First'), ({second}, {workspace}, 'Second')
        """);
    return (workspace, first, second);
  }

  private static async Task<(Guid Workspace, Guid Document)> SeedDocumentAsync(MiniDocsDbContext db)
  {
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "files");
    var document = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
        VALUES ({document}, {workspace}, {topic}, 'Test PDF', 'meeting', 'pending_upload')
        """);
    return (workspace, document);
  }

  private static Task<int> InsertUploadAsync(MiniDocsDbContext db, Guid workspace, Guid document, Guid operation, Guid request) =>
      db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, attempt_no, request_id)
          VALUES ({operation}, {workspace}, {document}, 'upload', 'prepared', 0, {request})
          """);

  private static Task<int> InsertTombstoneAsync(MiniDocsDbContext db, Guid workspace, Guid request) =>
      db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO minidocs.upload_request_tombstones(workspace_id, request_id) VALUES ({workspace}, {request})
          """);

  private static async Task RejectAsync(Func<Task> action, string constraint)
  {
    var error = await Assert.ThrowsAsync<PostgresException>(action);
    Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    Assert.Equal(constraint, error.ConstraintName);
  }

}
