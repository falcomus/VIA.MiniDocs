using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed partial class PostgresFinalHardeningTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  private PostgresTestDatabase Fixture => fixture;
  [Theory]
  [InlineData("GRANT UPDATE(text) ON minidocs.transcript_pages TO minidocs_runtime", "column privilege UPDATE")]
  [InlineData("GRANT INSERT ON minidocs.file_operations TO minidocs_runtime", "table privilege INSERT")]
  [InlineData("GRANT DELETE ON minidocs.file_operation_receipts TO minidocs_runtime", "table privilege DELETE")]
  [InlineData("GRANT UPDATE(\"MigrationId\") ON public.\"__EFMigrationsHistory\" TO minidocs_runtime", "column privilege UPDATE")]
  [InlineData("CREATE FUNCTION minidocs.command_unexpected() RETURNS boolean LANGUAGE sql AS 'SELECT true'; GRANT EXECUTE ON FUNCTION minidocs.command_unexpected() TO minidocs_runtime", "unexpected function")]
  [InlineData("ALTER FUNCTION minidocs.command_restore(uuid,uuid,bigint) OWNER TO minidocs_migrator", "function definition or owner")]
  [InlineData("ALTER FUNCTION minidocs.command_restore(uuid,uuid,bigint) SET search_path = public", "function definition or owner")]
  [InlineData("ALTER FUNCTION minidocs.command_restore(uuid,uuid,bigint) SECURITY INVOKER", "function definition or owner")]
  [InlineData("CREATE OR REPLACE FUNCTION minidocs.command_restore(p_workspace uuid,p_document uuid,p_version bigint) RETURNS integer LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS 'BEGIN RETURN 0; END;'", "unexpected function body")]
  [InlineData("GRANT EXECUTE ON FUNCTION minidocs.lock_workspace(uuid) TO minidocs_runtime", "unexpected function privilege")]
  [InlineData("ALTER DEFAULT PRIVILEGES FOR ROLE minidocs_migrator IN SCHEMA minidocs GRANT EXECUTE ON FUNCTIONS TO minidocs_runtime", "default ACL")]
  [InlineData("ALTER DEFAULT PRIVILEGES FOR ROLE minidocs_command_owner GRANT EXECUTE ON FUNCTIONS TO PUBLIC", "default ACL")]
  [InlineData("GRANT pg_read_all_data TO minidocs_runtime", "role membership")]
  [InlineData("CREATE ROLE fixture_inherited; GRANT pg_read_all_data TO fixture_inherited; GRANT fixture_inherited TO minidocs_runtime", "role membership")]
  [InlineData("ALTER ROLE minidocs_runtime BYPASSRLS", "role attributes")]
  [InlineData("CREATE ROLE fixture_outside; GRANT UPDATE ON minidocs.file_operations TO fixture_outside", "table ownership, existence or PUBLIC rights")]
  [InlineData("GRANT USAGE ON SEQUENCE minidocs.identity_user_claims_id_seq TO minidocs_runtime", "sequence ownership or rights")]
  [InlineData("GRANT CREATE ON SCHEMA public TO minidocs_runtime", "schema rights")]
  [InlineData("ALTER TABLE minidocs.file_operations DISABLE TRIGGER guard_file_transition", "unexpected trigger")]
  [InlineData("ALTER TABLE minidocs.pending_transcripts DROP CONSTRAINT ck_pending_terminal_metadata", "inactive constraint")]
  [InlineData("DROP INDEX minidocs.ux_provisioning_active_workspace", "invalid active provisioning index")]
  [InlineData("ALTER TABLE minidocs.identity_sessions DROP CONSTRAINT identity_sessions_handle_hash_check, ADD CONSTRAINT identity_sessions_handle_hash_check CHECK (octet_length(handle_hash) <= 32)", "unexpected D4 constraint identity_sessions_handle_hash_check")]
  [InlineData("ALTER TABLE minidocs.identity_sessions DROP CONSTRAINT ck_identity_session_times, ADD CONSTRAINT ck_identity_session_times CHECK (issued_at <= idle_until AND idle_until <= absolute_until AND (revoked_at IS NULL OR revoked_at >= issued_at))", "unexpected D4 constraint ck_identity_session_times")]
  [InlineData("ALTER TABLE minidocs.administrator_bootstrap DROP CONSTRAINT administrator_bootstrap_singleton_check, ADD CONSTRAINT administrator_bootstrap_singleton_check CHECK (singleton IS NOT NULL)", "unexpected D4 constraint administrator_bootstrap_singleton_check")]
  [InlineData("ALTER TABLE minidocs.identity_sessions DROP CONSTRAINT identity_sessions_pkey, ADD CONSTRAINT identity_sessions_pkey PRIMARY KEY (user_id)", "unexpected D4 constraint identity_sessions_pkey")]
  [InlineData("ALTER TABLE minidocs.identity_sessions DROP CONSTRAINT identity_sessions_user_id_fkey, ADD CONSTRAINT identity_sessions_user_id_fkey FOREIGN KEY (user_id) REFERENCES minidocs.workspaces(id)", "unexpected D4 constraint identity_sessions_user_id_fkey")]
  [InlineData("ALTER TABLE minidocs.administrator_bootstrap DROP CONSTRAINT administrator_bootstrap_user_id_fkey, ADD CONSTRAINT administrator_bootstrap_user_id_fkey FOREIGN KEY (user_id) REFERENCES minidocs.identity_users(id) ON DELETE CASCADE", "unexpected D4 constraint administrator_bootstrap_user_id_fkey")]
  [InlineData("ALTER TABLE minidocs.identity_sessions ALTER COLUMN idle_until DROP NOT NULL", "unexpected D4 column identity_sessions.idle_until")]
  public async Task GateRejectsCatalogDrift(string change, string expected)
  {
    await using var db = await OpenAsync();
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
      await db.Database.ExecuteSqlRawAsync(change);
      var error = await Assert.ThrowsAsync<PostgresException>(() => PostgresTestDatabase.VerifyPermissionsAsync(db));
      Assert.Contains(expected, error.MessageText);
      await tx.RollbackAsync();
    }
    await using var readOnly = await db.Database.BeginTransactionAsync();
    await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
    await PostgresTestDatabase.VerifyPermissionsAsync(db);
    await readOnly.CommitAsync();
  }

  [Theory]
  [InlineData(false, false, IsolationLevel.ReadCommitted)]
  [InlineData(true, false, IsolationLevel.ReadCommitted)]
  [InlineData(false, false, IsolationLevel.RepeatableRead)]
  [InlineData(true, false, IsolationLevel.RepeatableRead)]
  [InlineData(false, true, IsolationLevel.ReadCommitted)]
  [InlineData(true, true, IsolationLevel.ReadCommitted)]
  [InlineData(false, true, IsolationLevel.RepeatableRead)]
  [InlineData(true, true, IsolationLevel.RepeatableRead)]
  public async Task NewWorkRechecksStateAfterWorkspaceWait(bool disableOwner, bool provisioning, IsolationLevel isolation)
  {
    await using var db = await OpenAsync();
    await using var runtime = fixture.CreateRuntimePeer(db);
    var owner = Guid.NewGuid();
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertUserAndWorkspace(db, owner, workspace, "waiting");
    var operation = Guid.NewGuid();
    var worker = Guid.NewGuid();
    var latest = await PrepareAsync(runtime, workspace, operation, activate: !provisioning);

    await using var blocker = await db.Database.BeginTransactionAsync();
    if (disableOwner)
    {
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.workspaces SET version = version WHERE id = {workspace};
          UPDATE minidocs.identity_users SET is_disabled = true WHERE id = {owner};
          """);
    }
    else
    {
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.workspaces SET status = 'suspended', version = version + 1 WHERE id = {workspace}
          """);
    }

    await using var observer = fixture.CreatePeer(db);
    var pid = 0;
    var contender = LocalWriteTransaction.ExecuteAsync(runtime, async ct =>
    {
      Volatile.Write(ref pid, await runtime.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync(ct));
      return provisioning
          ? (await ProvisioningPersistence.TryBeginProvisioningAsync(runtime, workspace, operation, latest,
              worker, TimeSpan.FromMinutes(1), ct)).HasValue
          : await OperationLeasePersistence.TryReserveUploadAsync(runtime, workspace, Guid.NewGuid(), topic,
              Guid.NewGuid(), Guid.NewGuid(), "Rejected", DocumentKind.Note, ct);
    }, value => value, isolation);
    await PostgresWait.ForBlockedAsync(observer, () => Volatile.Read(ref pid), contender);
    await blocker.CommitAsync();

    Assert.Equal(isolation == IsolationLevel.ReadCommitted ? LocalWriteStatus.Rejected : LocalWriteStatus.ConcurrencyConflict,
        (await contender).Status);
    var saved = await observer.ProvisioningOperations.AsNoTracking().SingleAsync();
    Assert.Equal(provisioning ? ProvisioningStatus.Ready : ProvisioningStatus.Active, saved.Status);
    Assert.Null(saved.LeaseOwner);
    Assert.Empty(await observer.FileOperations.AsNoTracking().ToListAsync());
    Assert.Empty(await observer.Documents.AsNoTracking().ToListAsync());
  }

  [Fact]
  public async Task PreviousCombinedPredicateUsedThePreWaitWorkspaceSnapshot()
  {
    await using var db = await OpenAsync("20261126170000_EnforceWriteBoundaries");
    await using var runtime = fixture.CreateRuntimePeer(db);
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "previous-snapshot");
    await PrepareAsync(runtime, workspace, Guid.NewGuid(), activate: true);

    await using var blocker = await db.Database.BeginTransactionAsync();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.workspaces SET status = 'suspended', version = version + 1 WHERE id = {workspace}
        """);
    await using var observer = fixture.CreatePeer(db);
    var pid = 0;
    var document = Guid.NewGuid();
    var operation = Guid.NewGuid();
    var contender = LocalWriteTransaction.ExecuteAsync(runtime, async ct =>
    {
      Volatile.Write(ref pid, await runtime.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync(ct));
      return await OperationLeasePersistence.TryReserveUploadAsync(runtime, workspace, document, topic, operation,
          Guid.NewGuid(), "Previous command", DocumentKind.Note, ct);
    }, value => value);
    await PostgresWait.ForBlockedAsync(observer, () => Volatile.Read(ref pid), contender);
    await blocker.CommitAsync();
    Assert.Equal(LocalWriteStatus.Committed, (await contender).Status);
    Assert.Equal(WorkspaceStatus.Suspended, (await observer.Workspaces.AsNoTracking().SingleAsync()).Status);
    Assert.Equal(operation, (await observer.FileOperations.AsNoTracking().SingleAsync()).Id);
  }

  [Fact]
  public async Task EveryApprovedRuntimeSignatureRejectsMissingRequiredInput()
  {
    await using var db = await OpenAsync();
    await using var runtime = Fixture.CreateRuntimePeer(db);
    // The gate already checked this catalog against the authored signature list.
    // Build NULL calls from that verified inventory, never from external input.
    var calls = await db.Database.SqlQueryRaw<string>("""
        SELECT format('SELECT minidocs.%I(%s)', p.proname,
          (SELECT string_agg('NULL::' || format_type(type, NULL), ', ' ORDER BY ordinal)
           FROM unnest(p.proargtypes) WITH ORDINALITY AS args(type, ordinal))) AS "Value"
        FROM pg_proc p
        WHERE p.pronamespace = 'minidocs'::regnamespace AND has_function_privilege('minidocs_runtime', p.oid, 'EXECUTE')
        """).ToListAsync();
    Assert.Equal(44, calls.Count);
    foreach (var call in calls)
    {
      var error = await Assert.ThrowsAsync<PostgresException>(() => runtime.Database.ExecuteSqlRawAsync(call));
      Assert.True(error.SqlState == PostgresErrorCodes.InvalidParameterValue &&
          error.ConstraintName == "ck_command_argument", $"Wrong error contract for {call}: {error.SqlState}");
    }
    Assert.Empty(await db.Workspaces.AsNoTracking().ToListAsync());
    Assert.Empty(await db.FileOperations.AsNoTracking().ToListAsync());
  }

  [Fact]
  public async Task InvalidLeaseArgumentsAreRejectedBeforeReservingAnyWork()
  {
    await using var db = await OpenAsync();
    await using var runtime = fixture.CreateRuntimePeer(db);
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "lease-limit");
    await PrepareAsync(runtime, workspace, Guid.NewGuid(), activate: true);
    var doc = Guid.NewGuid();
    var op = Guid.NewGuid();
    var worker = Guid.NewGuid();
    Assert.Equal(LocalWriteStatus.Committed, (await LocalWriteTransaction.ExecuteAsync(runtime, ct =>
        OperationLeasePersistence.TryReserveUploadAsync(runtime, workspace, doc, topic, op, Guid.NewGuid(),
            "PDF", DocumentKind.Note, ct), value => value)).Status);
    foreach (var duration in new[] { TimeSpan.FromSeconds(-1), TimeSpan.Zero, TimeSpan.FromMinutes(15.01), TimeSpan.MaxValue })
    {
      await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => OperationLeasePersistence.TryBeginUploadAsync(
          runtime, workspace, doc, op, 1, worker, duration));
    }
    foreach (var duration in new[] { "-1 second", "0", "901 seconds", "2147483647 days", "infinity", "-infinity", null })
    {
      var error = await Assert.ThrowsAsync<PostgresException>(() => runtime.Database.SqlQuery<int>($"""
          SELECT minidocs.command_begin_upload({workspace}, {doc}, {op}, {worker}, {duration}::interval, 1::bigint)
          """).ToListAsync());
      Assert.Equal(PostgresErrorCodes.InvalidParameterValue, error.SqlState);
      Assert.Equal("ck_command_argument", error.ConstraintName);
    }
    Assert.Equal(FileOperationState.Prepared, (await db.FileOperations.AsNoTracking().SingleAsync()).State);
    var lease = await LocalWriteTransaction.ExecuteAsync(runtime, ct => OperationLeasePersistence.TryBeginUploadAsync(
        runtime, workspace, doc, op, 1, worker, TimeSpan.FromMinutes(15), ct), value => value.HasValue);
    Assert.Equal(LocalWriteStatus.Committed, lease.Status);
    var saved = await db.FileOperations.AsNoTracking().SingleAsync();
    Assert.Equal(FileOperationState.InFlight, saved.State);
    Assert.InRange(saved.LeaseUntil!.Value - saved.UpdatedAt, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
  }

  private async Task<MiniDocsDbContext> OpenAsync(string? migration = null)
  {
    var db = await fixture.CreateDatabaseAsync();
    try
    {
      await fixture.PrepareRuntimeAsync(db);
      await using var migrator = fixture.CreateMigratorPeer(db);
      await migrator.GetService<IMigrator>().MigrateAsync(migration);
      if (migration is null) await PostgresTestDatabase.VerifyPermissionsAsync(db);
      return db;
    }
    catch
    {
      await db.DisposeAsync();
      throw;
    }
  }

  private static async Task<long> PrepareAsync(MiniDocsDbContext runtime, Guid workspace, Guid operation, bool activate)
  {
    var result = await LocalWriteTransaction.ExecuteAsync(runtime, async ct =>
    {
      Assert.True(await ProvisioningPersistence.TryPrepareAsync(runtime, workspace, Guid.NewGuid(), operation,
          $"test-account-{workspace:N}", [1], [2], ct));
      if (!activate) return 1L;
      var started = Assert.IsType<OperationFence>(await ProvisioningPersistence.TryBeginProvisioningAsync(
          runtime, workspace, operation, 1, Guid.NewGuid(), TimeSpan.FromMinutes(1), ct));
      Assert.True(await ProvisioningPersistence.TryCompleteVerifiedAsync(
          runtime, workspace, operation, started, Guid.NewGuid(), ct));
      return started.Version + 1;
    }, _ => true);
    Assert.Equal(LocalWriteStatus.Committed, result.Status);
    return result.Value;
  }
}
