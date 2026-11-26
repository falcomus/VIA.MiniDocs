using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class PostgresRoleAndRestoreTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  [Fact]
  public async Task RestrictedRolesMigrateOperateAndRestoreWithoutGrantingDdlToRuntime()
  {
    await using var source = await fixture.CreateDatabaseAsync();
    // Generated test-only credentials in the fixture-owned container; never use external credentials.
    var migrationPassword = Guid.NewGuid().ToString("N");
    var runtimePassword = Guid.NewGuid().ToString("N");
    await source.Database.OpenConnectionAsync();
    try
    {
      // PostgreSQL utility statements do not accept bind parameters for passwords.
      // Only fixed role names and locally generated hexadecimal credentials are used.
      await using var createRoles = source.Database.GetDbConnection().CreateCommand();
      createRoles.CommandText =
          $"CREATE ROLE minidocs_migrator LOGIN NOINHERIT PASSWORD '{migrationPassword}'; " +
          $"CREATE ROLE minidocs_runtime LOGIN NOINHERIT PASSWORD '{runtimePassword}';";
      await createRoles.ExecuteNonQueryAsync();
    }
    finally
    {
      await source.Database.CloseConnectionAsync();
    }

    await source.Database.ExecuteSqlRawAsync(ReadRoleScript("PrepareDatabaseRoles.sql"));
    await using (var migrator = fixture.CreateRoleContext(source, "minidocs_migrator", migrationPassword))
    {
      await migrator.Database.MigrateAsync();
      Assert.Equal(3, (await migrator.Database.GetAppliedMigrationsAsync()).Count());
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
    await using (var runtime = fixture.CreateRoleContext(source, "minidocs_runtime", runtimePassword))
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

    await using (var runtime = fixture.CreateRoleContext(restored, "minidocs_runtime", runtimePassword))
    {
      await AssertRuntimePrivilegesAsync(runtime);
      var restoredWorkspace = await runtime.Workspaces.AsNoTracking().SingleAsync();
      Assert.Equal(workspace, restoredWorkspace.Id);
      Assert.Equal(owner, restoredWorkspace.OwnerUserId);
      Assert.Equal(WorkspaceStatus.Active, restoredWorkspace.Status);
      var technicalIdentity = await runtime.ViaDocsAccounts.AsNoTracking().SingleAsync();
      Assert.Equal(applicationId, technicalIdentity.ApplicationId);
      Assert.Equal(protectedBytes, technicalIdentity.ProtectedSecret);
      Assert.Equal(document, (await runtime.Documents.AsNoTracking().SingleAsync()).Id);
      var unresolved = await runtime.FileOperations.AsNoTracking().SingleAsync();
      Assert.Equal(operation, unresolved.Id);
      Assert.Equal(unknownRequest, unresolved.RequestId);
      Assert.Equal(FileOperationState.Unknown, unresolved.State);
      Assert.Equal(retiredRequest, (await runtime.UploadRequestTombstones.AsNoTracking().SingleAsync()).RequestId);

      // Deferred triggers must still reject invalid state after restoration.
      var orphan = await Assert.ThrowsAsync<PostgresException>(() =>
          PostgresSeed.InsertUserAsync(runtime, Guid.NewGuid(), "restore-orphan", "regular"));
      Assert.Equal("ck_user_workspace_cardinality", orphan.ConstraintName);
      await using var tx = await runtime.Database.BeginTransactionAsync();
      Assert.True(await CommandAsync(runtime, $"""
          SELECT minidocs.command_rename_document({workspace}, {document}, 1::bigint, 'Restored and writable')
          """));
      await tx.CommitAsync();
      Assert.Equal("Restored and writable", (await runtime.Documents.AsNoTracking().SingleAsync()).Title);
    }
    await using var restoredMigrator = fixture.CreateRoleContext(restored, "minidocs_migrator", migrationPassword);
    Assert.Equal(3, (await restoredMigrator.Database.GetAppliedMigrationsAsync()).Count());
    Assert.Empty(await restoredMigrator.Database.GetPendingMigrationsAsync());
  }

  private static async Task AssertRuntimePrivilegesAsync(MiniDocsDbContext db)
  {
    Assert.Equal("minidocs_runtime", await db.Database.SqlQueryRaw<string>(
        """SELECT current_user::text AS "Value" """).SingleAsync());
    Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());

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
