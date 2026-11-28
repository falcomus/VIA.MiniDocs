using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class PostgresRoleAndRestoreTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  private static readonly string[] ExpectedMigrations =
  [
    "20261126134926_InitialD2Model",
    "20261126170000_EnforceWriteBoundaries",
    "20261126180000_FinalQualityHardening",
    "20261127090000_SessionSecurity",
    "20261127100000_TechnicalProvisioning",
    "20261128100000_WorkspaceMetadata",
    "20261128110000_TopicSiblingNames"
  ];

  [Theory]
  [InlineData("  ÄRGER", "existing sibling names conflict")]
  [InlineData("Alle Zugeordnet ", "existing root uses a reserved name")]
  public async Task TopicSiblingNamesUpgradeRejectsInvalidExistingDataWithoutModifyingIt(string invalid, string diagnostic)
  {
    await using var database = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(database);
    await using var migrator = fixture.CreateMigratorPeer(database);
    await migrator.GetService<IMigrator>().MigrateAsync(ExpectedMigrations[^2]);
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertActiveWorkspaceAsync(database, Guid.NewGuid(), workspace, "invalid-topic-migration");
    await database.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name)
        VALUES ({Guid.NewGuid()}, {workspace}, 'Ärger'), ({Guid.NewGuid()}, {workspace}, {invalid});
        """);
    var before = await MetadataRestoreSnapshotAsync(database, workspace);
    var failure = await Assert.ThrowsAsync<PostgresException>(() => migrator.Database.MigrateAsync());
    Assert.Contains(diagnostic, failure.MessageText);
    Assert.Equal(before, await MetadataRestoreSnapshotAsync(database, workspace));
    Assert.Equal(ExpectedMigrations[..^1], (await migrator.Database.GetAppliedMigrationsAsync()).ToArray());
    await PostgresTestDatabase.VerifyPermissionsAsync(database);
  }

  [Fact]
  public async Task RestrictedRolesMigrateOperateAndRestoreWithoutGrantingDdlToRuntime()
  {
    await using var source = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(source);
    await using (var migrator = fixture.CreateMigratorPeer(source))
    {
      await migrator.Database.MigrateAsync();
      Assert.Equal(ExpectedMigrations, (await migrator.Database.GetAppliedMigrationsAsync()).ToArray());
      await DeniedAsync(migrator, "CREATE SCHEMA forbidden_schema");
      await DeniedAsync(migrator, "CREATE ROLE forbidden_role");
    }
    await PostgresTestDatabase.VerifyPermissionsAsync(source);

    var workspace = Guid.NewGuid();
    var owner = Guid.NewGuid();
    var document = Guid.NewGuid();
    var operation = Guid.NewGuid();
    var unknownRequest = Guid.NewGuid();
    var retiredRequest = Guid.NewGuid();
    var account = Guid.NewGuid();
    var applicationId = Guid.NewGuid();
    var protectedBytes = new byte[] { 1, 7, 42 };
    var topic = await PostgresSeed.InsertUserAndWorkspace(source, owner, workspace, "role-restore");
    var provisioning = Guid.NewGuid();
    var worker = Guid.NewGuid();
    await using (var runtime = fixture.CreateRuntimePeer(source))
    {
      await AssertRuntimePrivilegesAsync(runtime);
      await using (var tx = await runtime.Database.BeginTransactionAsync())
      {
        Assert.True(await CommandAsync(runtime, $"""
            SELECT minidocs.command_prepare_provisioning(
              {workspace}, {account}, {provisioning}, 'restore-test', {protectedBytes}, {protectedBytes})
            """));
        var fence = Assert.IsType<OperationFence>(await ProvisioningPersistence.TryBeginProvisioningAsync(
            runtime, workspace, provisioning, 1, worker, TimeSpan.FromMinutes(1)));
        Assert.True(await CommandAsync(runtime, $"""
            SELECT minidocs.command_complete_provisioning(
              {workspace}, {provisioning}, {fence.Version}, {worker}, {applicationId})
            """));
        Assert.True(await CommandAsync(runtime, $"""
            SELECT minidocs.command_reserve_upload(
              {workspace}, {document}, {topic}, {operation}, {unknownRequest}, 'Backup test', 'meeting')
            """));
        Assert.NotNull(await OperationLeasePersistence.TryBeginUploadAsync(
            runtime, workspace, document, operation, 1, worker, TimeSpan.FromMinutes(1)));
        await tx.CommitAsync();
      }
      Assert.Equal(applicationId, (await runtime.ViaDocsAccounts.AsNoTracking().SingleAsync()).ApplicationId);
      await source.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.file_operations SET lease_until = clock_timestamp() - interval '1 second'
          WHERE id = {operation};
          INSERT INTO minidocs.upload_request_tombstones(workspace_id, request_id)
          VALUES ({workspace}, {retiredRequest});
          """);
      await using var recovery = await runtime.Database.BeginTransactionAsync();
      Assert.True(await OperationLeasePersistence.TryRecoverExpiredUploadAsync(
          runtime, workspace, document, operation, 2));
      await recovery.CommitAsync();
    }

    // Restore into a different, empty database in the very same isolated test container.
    // Object ownership and ACLs are intentionally preserved by pg_dump/pg_restore.
    await using var restored = await fixture.CreateDatabaseAsync();
    await fixture.RestoreCopyAsync(source, restored);
    // Database-level privileges are not in a dump without --create; apply DBA setup again.
    // Do NOT regrant table/function privileges: the archive must have preserved those.
    await restored.Database.ExecuteSqlRawAsync(ReadRoleScript("RestoreDatabaseConnect.sql"));
    await PostgresTestDatabase.VerifyPermissionsAsync(restored);

    await using (var runtime = fixture.CreateRuntimePeer(restored))
    {
      await AssertRuntimePrivilegesAsync(runtime);
      var restoredWorkspace = await runtime.Workspaces.AsNoTracking().SingleAsync();
      Assert.Equal(workspace, restoredWorkspace.Id);
      Assert.Equal(owner, restoredWorkspace.OwnerUserId);
      Assert.Equal(WorkspaceStatus.Active, restoredWorkspace.Status);
      var technicalIdentity = await runtime.ViaDocsAccounts.AsNoTracking().SingleAsync();
      Assert.Equal(applicationId, technicalIdentity.ApplicationId);
      Assert.Equal(protectedBytes, technicalIdentity.ProtectedSecret);
      Assert.Equal(document, (await runtime.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).Id);
      var unresolved = await runtime.FileOperations.ObserveAllWorkspaces().AsNoTracking().SingleAsync();
      Assert.Equal(operation, unresolved.Id);
      Assert.Equal(unknownRequest, unresolved.RequestId);
      Assert.Equal(FileOperationState.Unknown, unresolved.State);
      Assert.Equal(retiredRequest, (await runtime.UploadRequestTombstones.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).RequestId);

      // Deferred triggers must still reject invalid state after restoration.
      var orphan = await Assert.ThrowsAsync<PostgresException>(() =>
          PostgresSeed.InsertUserAsync(runtime, Guid.NewGuid(), "restore-orphan", "regular"));
      Assert.Equal("ck_user_workspace_cardinality", orphan.ConstraintName);
      await using var tx = await runtime.Database.BeginTransactionAsync();
      Assert.True(await CommandAsync(runtime, $"""
          SELECT minidocs.command_rename_document({workspace}, {document}, 1::bigint, 'Restored and writable')
          """));
      await tx.CommitAsync();
      Assert.Equal("Restored and writable", (await runtime.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync()).Title);
    }
    await using var restoredMigrator = fixture.CreateMigratorPeer(restored);
    Assert.Equal(ExpectedMigrations, (await restoredMigrator.Database.GetAppliedMigrationsAsync()).ToArray());
    Assert.Empty(await restoredMigrator.Database.GetPendingMigrationsAsync());
  }

  [Fact]
  [Trait("Package", "D6-FIX03-C")]
  public async Task PopulatedD4UpgradeAndD6RestorePreserveMetadataVersionsAndWriteBoundary()
  {
    await using var source = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(source);
    await using (var migrator = fixture.CreateMigratorPeer(source))
      await migrator.GetService<IMigrator>().MigrateAsync("20261127100000_TechnicalProvisioning");
    await PostgresTestDatabase.VerifyPermissionsAsync(source);
    var workspace = Guid.NewGuid();
    var root = Guid.NewGuid();
    var child = Guid.NewGuid();
    var tag = Guid.NewGuid();
    var document = Guid.NewGuid();
    var other = Guid.NewGuid();
    var system = await PostgresSeed.InsertActiveWorkspaceAsync(source, Guid.NewGuid(), workspace, "metadata-restore");
    await source.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name, version)
        VALUES ({root}, {workspace}, 'Parent', 3);
        INSERT INTO minidocs.topics(id, workspace_id, parent_id, name, version)
        VALUES ({child}, {workspace}, {root}, 'Child', 4);
        INSERT INTO minidocs.tags(id, workspace_id, name, name_key, description, color, version)
        VALUES ({tag}, {workspace}, 'Ärger', 'ärger', 'Before', '#123456', 5);
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, via_file_id, title, kind,
            document_date, availability, version)
        VALUES ({document}, {workspace}, {child}, {Guid.NewGuid()}, 'Before', 'note', DATE '2026-01-02', 'available', 7),
            ({other}, {workspace}, {system}, {Guid.NewGuid()}, 'Other', 'meeting', NULL, 'available', 2);
        INSERT INTO minidocs.document_tags(workspace_id, document_id, tag_id)
        VALUES ({workspace}, {document}, {tag});
        """);
    var before = await MetadataRestoreSnapshotAsync(source, workspace);

    // Rehearse the documented rollback path without downgrading an existing schema.
    await using var preMigrationRestore = await fixture.CreateDatabaseAsync();
    await fixture.RestoreCopyAsync(source, preMigrationRestore);
    await preMigrationRestore.Database.ExecuteSqlRawAsync(ReadRoleScript("RestoreDatabaseConnect.sql"));
    await PostgresTestDatabase.VerifyPermissionsAsync(preMigrationRestore);
    Assert.Equal(ExpectedMigrations[..^2], (await preMigrationRestore.Database.GetAppliedMigrationsAsync()).ToArray());
    Assert.Equal(ExpectedMigrations[^2..], (await preMigrationRestore.Database.GetPendingMigrationsAsync()).ToArray());
    await using (var runtime = fixture.CreateRuntimePeer(preMigrationRestore))
    {
      Assert.Equal(before, await MetadataRestoreSnapshotAsync(runtime, workspace));
      await using var tx = await runtime.Database.BeginTransactionAsync();
      Assert.True(await CommandAsync(runtime, $"SELECT minidocs.command_rename_document({workspace}, {document}, 7::bigint, 'Rollback writable')"));
      await tx.CommitAsync();
      Assert.Equal("Rollback writable", (await runtime.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync(d => d.Id == document)).Title);
    }

    await using (var migrator = fixture.CreateMigratorPeer(source))
    {
      await migrator.Database.MigrateAsync();
      Assert.Equal(ExpectedMigrations, (await migrator.Database.GetAppliedMigrationsAsync()).ToArray());
      Assert.Empty(await migrator.Database.GetPendingMigrationsAsync());
    }
    await PostgresTestDatabase.VerifyPermissionsAsync(source);
    Assert.Equal(before, await MetadataRestoreSnapshotAsync(source, workspace));

    await using (var runtime = fixture.CreateRuntimePeer(source))
    {
      await AssertRuntimePrivilegesAsync(runtime);
      var change = await LocalWriteTransaction.ExecuteAsync(runtime, async ct =>
      {
        Assert.True(await WorkspaceMetadataPersistence.WriteTopicAsync(runtime, workspace, child, 4, "Child D6", null, false, ct));
        Assert.True(await WorkspaceMetadataPersistence.WriteDocumentAsync(runtime, workspace, document, 7, "D6 title", "meeting", new DateOnly(2026, 11, 28), true, ct));
        Assert.True(await WorkspaceMetadataPersistence.WriteTagAsync(runtime, workspace, tag, 5, "Grün", "After", "#654321", ct));
        return true;
      }, value => value);
      Assert.Equal(LocalWriteStatus.Committed, change.Status);
    }
    var after = await MetadataRestoreSnapshotAsync(source, workspace);
    Assert.NotEqual(before, after);
    await using var restored = await fixture.CreateDatabaseAsync();
    await fixture.RestoreCopyAsync(source, restored);
    await restored.Database.ExecuteSqlRawAsync(ReadRoleScript("RestoreDatabaseConnect.sql"));
    await PostgresTestDatabase.VerifyPermissionsAsync(restored);
    await using (var runtime = fixture.CreateRuntimePeer(restored))
    {
      await AssertRuntimePrivilegesAsync(runtime);
      Assert.Equal(after, await MetadataRestoreSnapshotAsync(runtime, workspace));
      var restoredDocument = await runtime.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync(d => d.Id == document);
      Assert.Equal("D6 title", restoredDocument.Title);
      Assert.Equal(DocumentKind.Meeting, restoredDocument.Kind);
      Assert.Equal(new DateOnly(2026, 11, 28), restoredDocument.DocumentDate);
      Assert.Equal(child, restoredDocument.TopicId);
      Assert.Equal(9L, restoredDocument.Version);
      var restoredTopic = await runtime.Topics.ObserveAllWorkspaces().AsNoTracking().SingleAsync(t => t.Id == child);
      Assert.Equal(root, restoredTopic.ParentId);
      Assert.Equal("Child D6", restoredTopic.Name);
      Assert.Equal(5L, restoredTopic.Version);
      var restoredTag = await runtime.Tags.ObserveAllWorkspaces().AsNoTracking().SingleAsync(t => t.Id == tag);
      Assert.Equal("Grün", restoredTag.Name);
      Assert.Equal("grün", restoredTag.NameKey);
      Assert.Equal("After", restoredTag.Description);
      Assert.Equal("#654321", restoredTag.Color);
      Assert.Equal(6L, restoredTag.Version);
      Assert.Equal(2L, (await runtime.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync(d => d.Id == other)).Version);
      var link = Assert.Single(await runtime.DocumentTags.ObserveAllWorkspaces().AsNoTracking().ToListAsync());
      Assert.Equal(document, link.DocumentId);
      Assert.Equal(tag, link.TagId);
      var write = await LocalWriteTransaction.ExecuteAsync(runtime, async ct =>
      {
        Assert.False(await WorkspaceMetadataPersistence.WriteDocumentAsync(runtime, workspace, document, 7, "Stale", null, null, false, ct));
        Assert.True(await WorkspaceMetadataPersistence.WriteDocumentAsync(runtime, workspace, document, 9, "Restored D6", null, null, false, ct));
        Assert.True(await WorkspaceMetadataPersistence.WriteTopicAsync(runtime, workspace, child, 5, "Restored child", null, false, ct));
        Assert.True(await WorkspaceMetadataPersistence.WriteTagAsync(runtime, workspace, tag, 6, "Restored tag", "After restore", "#654321", ct));
        return true;
      }, value => value);
      Assert.Equal(LocalWriteStatus.Committed, write.Status);
      var saved = await runtime.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync(d => d.Id == document);
      Assert.Equal("Restored D6", saved.Title);
      Assert.Equal(11L, saved.Version);
      var savedTopic = await runtime.Topics.ObserveAllWorkspaces().AsNoTracking().SingleAsync(t => t.Id == child);
      Assert.Equal("Restored child", savedTopic.Name);
      Assert.Equal(6L, savedTopic.Version);
      var savedTag = await runtime.Tags.ObserveAllWorkspaces().AsNoTracking().SingleAsync(t => t.Id == tag);
      Assert.Equal("Restored tag", savedTag.Name);
      Assert.Equal(7L, savedTag.Version);
    }
    await using var restoredMigrator = fixture.CreateMigratorPeer(restored);
    Assert.Empty(await restoredMigrator.Database.GetPendingMigrationsAsync());
  }

  // Compare complete rows, including IDs, timestamps, version/ETag sources and
  // relationship rows, not just counts. Stable ordering survives pg_restore.
  private static async Task<string> MetadataRestoreSnapshotAsync(MiniDocsDbContext db, Guid workspace) =>
      (await db.Database.SqlQuery<string>($"""
          SELECT jsonb_build_object(
            'topics', (SELECT jsonb_agg(to_jsonb(t) ORDER BY t.id) FROM minidocs.topics t WHERE t.workspace_id = {workspace}),
            'tags', (SELECT jsonb_agg(to_jsonb(t) ORDER BY t.id) FROM minidocs.tags t WHERE t.workspace_id = {workspace}),
            'documents', (SELECT jsonb_agg(to_jsonb(d) ORDER BY d.id) FROM minidocs.documents d WHERE d.workspace_id = {workspace}),
            'links', (SELECT jsonb_agg(to_jsonb(l) ORDER BY l.document_id, l.tag_id) FROM minidocs.document_tags l WHERE l.workspace_id = {workspace})
          )::text AS "Value"
          """).ToListAsync()).Single();

  private static async Task AssertRuntimePrivilegesAsync(MiniDocsDbContext db)
  {
    Assert.Equal("minidocs_runtime", await db.Database.SqlQueryRaw<string>(
        """SELECT current_user::text AS "Value" """).SingleAsync());
    Assert.Equal(ExpectedMigrations, (await db.Database.GetAppliedMigrationsAsync()).ToArray());

    await DeniedAsync(db, "CREATE TABLE minidocs.forbidden(id integer)");
    await DeniedAsync(db, "ALTER TABLE minidocs.documents ADD COLUMN forbidden integer");
    await DeniedAsync(db, "TRUNCATE minidocs.documents CASCADE");
    await DeniedAsync(db, """DELETE FROM public."__EFMigrationsHistory" """);
    await DeniedAsync(db, "CREATE TEMP TABLE forbidden_temp(id integer)");
    await DeniedAsync(db, "SET session_replication_role = replica");
    await DeniedAsync(db, "SET ROLE minidocs_migrator");
    await DeniedAsync(db, "DELETE FROM minidocs.upload_request_tombstones");
  }

  private static async Task DeniedAsync(MiniDocsDbContext db, string sql)
  {
    var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
    Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
  }

  private static async Task<bool> CommandAsync(MiniDocsDbContext db, FormattableString command) =>
      (await db.Database.SqlQuery<bool>(command).ToListAsync()).Single();

  internal static string ReadRoleScript(string name, [CallerFilePath] string sourceFile = "")
  {
    var directory = Path.GetDirectoryName(sourceFile)
        ?? throw new InvalidOperationException("The test source directory is required.");
    return File.ReadAllText(Path.GetFullPath(Path.Combine(directory, "..", "..", "src",
        "VIA.MiniDocs.Infrastructure", "Persistence", name)));
  }
}
