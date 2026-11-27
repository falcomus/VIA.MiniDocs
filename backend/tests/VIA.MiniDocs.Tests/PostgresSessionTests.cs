using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
[Trait("Package", "D4-A")]
public sealed class PostgresSessionTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  private static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);
  private static readonly TimeSpan Absolute = TimeSpan.FromHours(8);

  [Fact]
  public async Task UpgradeChecksRuntimePrivilegesAndRestoresBootstrapMarker()
  {
    await using var database = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(database);
    await using var migrator = fixture.CreateMigratorPeer(database);
    await migrator.GetService<IMigrator>().MigrateAsync("20261126180000_FinalQualityHardening");
    await PostgresTestDatabase.VerifyPermissionsAsync(database);
    var regular = Guid.NewGuid();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(database, regular, workspace, "legacy");
    await migrator.Database.MigrateAsync();
    await PostgresTestDatabase.VerifyPermissionsAsync(database);
    Assert.Equal(regular, (await database.Workspaces.SingleAsync()).OwnerUserId);

    await using var runtime = fixture.CreateRuntimePeer(database);
    foreach (var command in new[]
    {
      "UPDATE minidocs.identity_users SET is_disabled = true",
      "DELETE FROM minidocs.identity_sessions",
      "INSERT INTO minidocs.identity_user_roles SELECT gen_random_uuid(), gen_random_uuid()",
      "SELECT minidocs.command_bootstrap_administrator(gen_random_uuid(), 'refused', 'REFUSED', 'invalid')"
    })
      Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,
          (await Assert.ThrowsAsync<PostgresException>(() => runtime.Database.ExecuteSqlRawAsync(command))).SqlState);

    await using var first = fixture.CreateBootstrapPeer(database);
    await using var second = fixture.CreateBootstrapPeer(database);
    var results = await Task.WhenAll(BootstrapAsync(first, "first"), BootstrapAsync(second, "second"));
    var winner = Assert.Single(results, result => result.HasValue)!.Value;
    Assert.Equal("admin_only", await database.Database.SqlQuery<string>(
        $"""SELECT account_kind AS "Value" FROM minidocs.identity_users WHERE id = {winner}""").SingleAsync());
    Assert.False(await database.Workspaces.AnyAsync(workspace => workspace.OwnerUserId == winner));
    Assert.Null(await BootstrapAsync(second, "later"));
    await using var restored = await fixture.CreateDatabaseAsync();
    await fixture.RestoreCopyAsync(database, restored);
    await restored.Database.ExecuteSqlRawAsync(PostgresRoleAndRestoreTests.ReadRoleScript("RestoreDatabaseConnect.sql"));
    await PostgresTestDatabase.VerifyPermissionsAsync(restored);
    await using var restoredBootstrap = fixture.CreateBootstrapPeer(restored);
    Assert.Null(await BootstrapAsync(restoredBootstrap, "restore-replay"));
    Assert.Equal(winner, await restored.Database.SqlQueryRaw<Guid>(
        """SELECT user_id AS "Value" FROM minidocs.administrator_bootstrap""").SingleAsync());
  }

  [Fact]
  public async Task DeactivationAndReactivationInvalidateOldSessionsAndPreserveWorkspace()
  {
    await using var database = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(database);
    var admin = await CreateAdminAsync(database);
    var target = Guid.NewGuid();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(database, target, workspace, "target");
    await WriteStampsAsync(database, target);
    var actor = await CreateSessionAsync(runtime, admin);
    var old = await CreateSessionAsync(runtime, target);
    var status = new AdministratorAccounts(runtime);
    Assert.Equal(DeactivationResult.LastAdministrator, await ChangeAsync(status, actor, admin, true));
    var version = (await status.ReadVersionAsync(target, default))!;
    Assert.Equal(DeactivationResult.Applied, await status.SetDisabledAsync(actor, target, version.ETag, true, default));
    Assert.Equal(DeactivationResult.VersionMismatch, await status.SetDisabledAsync(actor, target, version.ETag, true, default));
    Assert.Null(await new SessionStore(runtime).ReadAsync(old, Idle, default));
    Assert.Equal(DeactivationResult.Applied, await ChangeAsync(status, actor, target, false));
    Assert.Null(await new SessionStore(runtime).ReadAsync(old, Idle, default));
    var current = await CreateSessionAsync(runtime, target);
    Assert.NotEqual(old.Stamp, current.Stamp);
    Assert.Equal("Pending", (await runtime.Workspaces.SingleAsync(w => w.Id == workspace)).Status.ToString());
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task IdentityRoutineWriteBlocksActualD2OrDeactivation(bool repeatableRead, bool deactivation)
  {
    await using var database = await CreateDatabaseAsync();
    await using var holder = fixture.CreateRuntimePeer(database);
    await using var waiter = fixture.CreateRuntimePeer(database);
    var admin = await CreateAdminAsync(database);
    var target = Guid.NewGuid();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(database, target, workspace, "blocking");
    await WriteStampsAsync(database, target);
    var actor = await CreateSessionAsync(waiter, admin);
    await waiter.Database.OpenConnectionAsync();
    await holder.Database.OpenConnectionAsync();
    var holderPid = ((NpgsqlConnection)holder.Database.GetDbConnection()).ProcessID;
    var waiterPid = ((NpgsqlConnection)waiter.Database.GetDbConnection()).ProcessID;
    await using var writer = await holder.Database.BeginTransactionAsync();
    await using var waiting = await waiter.Database.BeginTransactionAsync(
        repeatableRead ? IsolationLevel.RepeatableRead : IsolationLevel.ReadCommitted);
    var version = (await new AdministratorAccounts(waiter).ReadVersionAsync(target, default))!;
    await holder.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.identity_users SET access_failed_count = 1,
          security_stamp = {Guid.NewGuid().ToString("N")}, concurrency_stamp = 'changed'
        WHERE id = {target}
        """);

    Task blocked = deactivation
        ? new AdministratorAccounts(waiter).SetDisabledAsync(actor, target, version.ETag, true, default)
        : PrepareAsync(waiter, workspace);
    await WaitForBlockerAsync(database, waiterPid, holderPid, blocked);
    await writer.CommitAsync();
    if (repeatableRead)
    {
      var conflict = await Assert.ThrowsAsync<InvalidOperationException>(async () => await blocked);
      Assert.Equal(PostgresErrorCodes.SerializationFailure, Assert.IsType<PostgresException>(conflict.InnerException).SqlState);
    }
    else if (deactivation)
      Assert.Equal(DeactivationResult.VersionMismatch, await (Task<DeactivationResult>)blocked);
    else
      Assert.True(await (Task<bool>)blocked);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task WorkspaceFirstSerializesD2WriteAndAdministrativeDeactivation(bool deactivationFirst)
  {
    await using var database = await CreateDatabaseAsync();
    await using var adminConnection = fixture.CreateRuntimePeer(database);
    await using var d2Connection = fixture.CreateRuntimePeer(database);
    var admin = await CreateAdminAsync(database);
    var target = Guid.NewGuid();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(database, target, workspace, "competition");
    await WriteStampsAsync(database, target);
    var actor = await CreateSessionAsync(adminConnection, admin);
    var status = new AdministratorAccounts(adminConnection);
    await adminConnection.Database.OpenConnectionAsync();
    await d2Connection.Database.OpenConnectionAsync();
    var adminPid = ((NpgsqlConnection)adminConnection.Database.GetDbConnection()).ProcessID;
    var d2Pid = ((NpgsqlConnection)d2Connection.Database.GetDbConnection()).ProcessID;
    if (deactivationFirst)
    {
      await using var transaction = await adminConnection.Database.BeginTransactionAsync();
      Assert.Equal(DeactivationResult.Applied, await ChangeAsync(status, actor, target, true));
      var blocked = PrepareAsync(d2Connection, workspace);
      await WaitForBlockerAsync(database, d2Pid, adminPid, blocked);
      await transaction.CommitAsync();
      Assert.False(await blocked);
    }
    else
    {
      await using var transaction = await d2Connection.Database.BeginTransactionAsync();
      Assert.True(await PrepareAsync(d2Connection, workspace));
      var blocked = ChangeAsync(status, actor, target, true);
      await WaitForBlockerAsync(database, adminPid, d2Pid, blocked);
      await transaction.CommitAsync();
      Assert.Equal(DeactivationResult.Applied, await blocked);
    }
    Assert.True(await database.Users.Where(u => u.Id == target).Select(u => u.IsDisabled).SingleAsync());
  }

  [Fact]
  public async Task RenewalCannotResurrectRevocationAndCleanupIsBounded()
  {
    await using var database = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var peer = fixture.CreateRuntimePeer(database);
    var admin = await CreateAdminAsync(database);
    var active = await CreateSessionAsync(runtime, admin);
    var previouslyActive = await CreateSessionAsync(runtime, admin);
    await database.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.identity_sessions SET issued_at = clock_timestamp() - interval '1 day',
          idle_until = clock_timestamp() + interval '1 minute' WHERE handle_hash = {active.HandleHash}
        """);
    await runtime.Database.OpenConnectionAsync();
    await peer.Database.OpenConnectionAsync();
    await using var transaction = await runtime.Database.BeginTransactionAsync();
    await new SessionStore(runtime).RevokeAsync(active, default);
    var renewal = new SessionStore(peer).RenewAsync(active, Idle, default);
    await WaitForBlockerAsync(database, ((NpgsqlConnection)peer.Database.GetDbConnection()).ProcessID,
        ((NpgsqlConnection)runtime.Database.GetDbConnection()).ProcessID, renewal);
    await transaction.CommitAsync();
    Assert.False(await renewal);
    Assert.Null(await new SessionStore(peer).ReadAsync(active, Idle, default));
    await database.Database.ExecuteSqlRawAsync("""
        UPDATE minidocs.identity_sessions SET issued_at = clock_timestamp() - interval '1 day',
          idle_until = clock_timestamp() - interval '1 minute'
        """);
    Assert.Null(await new SessionStore(peer).ReadAsync(previouslyActive, Idle, default));
    Assert.Equal(1, await new SessionStore(peer).CleanupAsync(1, default));
    Assert.Equal(1, await new SessionStore(peer).CleanupAsync(1, default));
    Assert.Equal(0, await new SessionStore(peer).CleanupAsync(1, default));
  }

  [Fact]
  public async Task RenewalIsCappedByAbsoluteDeadline()
  {
    await using var database = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(database);
    var admin = await CreateAdminAsync(database);
    var authority = await CreateSessionAsync(runtime, admin);
    await database.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.identity_sessions
        SET idle_until = clock_timestamp() + interval '1 minute', absolute_until = clock_timestamp() + interval '2 minutes'
        WHERE handle_hash = {authority.HandleHash}
        """);
    var sessions = new SessionStore(runtime);
    Assert.True(await sessions.RenewAsync(authority, Idle, default));
    Assert.True(await database.Database.SqlQuery<bool>($"""
        SELECT idle_until = absolute_until AS "Value" FROM minidocs.identity_sessions WHERE handle_hash = {authority.HandleHash}
        """).SingleAsync());
    Assert.False(await sessions.RenewAsync(authority, Idle, default));
    await database.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.identity_sessions SET issued_at = clock_timestamp() - interval '2 hours',
          idle_until = clock_timestamp() - interval '1 minute', absolute_until = clock_timestamp() - interval '1 minute'
        WHERE handle_hash = {authority.HandleHash}
        """);
    Assert.Null(await sessions.ReadAsync(authority, Idle, default));
    Assert.False(await sessions.RenewAsync(authority, Idle, default));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ConcurrentSelfDeactivationsCannotRemoveLastAdministrator(bool reverse)
  {
    await using var database = await CreateDatabaseAsync();
    var firstAdmin = await CreateAdminAsync(database);
    var secondAdmin = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(database, secondAdmin, "second", "admin_only");
    await WriteStampsAsync(database, secondAdmin);
    await database.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.identity_user_roles (user_id, role_id)
        SELECT {secondAdmin}, id FROM minidocs.identity_roles WHERE normalized_name = 'ADMINISTRATOR'
        """);
    await using var first = fixture.CreateRuntimePeer(database);
    await using var second = fixture.CreateRuntimePeer(database);
    await first.Database.OpenConnectionAsync();
    await second.Database.OpenConnectionAsync();
    var firstActor = await CreateSessionAsync(first, reverse ? secondAdmin : firstAdmin);
    var secondActor = await CreateSessionAsync(second, reverse ? firstAdmin : secondAdmin);
    await using var winner = await first.Database.BeginTransactionAsync();
    Assert.Equal(DeactivationResult.Applied,
        await ChangeAsync(new AdministratorAccounts(first), firstActor, firstActor.UserId, true));
    var blocked = ChangeAsync(new AdministratorAccounts(second), secondActor, secondActor.UserId, true);
    await WaitForBlockerAsync(database, ((NpgsqlConnection)second.Database.GetDbConnection()).ProcessID,
        ((NpgsqlConnection)first.Database.GetDbConnection()).ProcessID, blocked);
    await winner.CommitAsync();
    Assert.Equal(DeactivationResult.LastAdministrator, await blocked);
    Assert.Null(await new SessionStore(first).ReadAsync(firstActor, Idle, default));
    Assert.NotNull(await new SessionStore(second).ReadAsync(secondActor, Idle, default));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ActorLogoutAndDeactivationSerializeOnActualSessionRow(bool logoutFirst)
  {
    await using var database = await CreateDatabaseAsync();
    var admin = await CreateAdminAsync(database);
    var user = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(database, user, Guid.NewGuid(), "customer");
    await WriteStampsAsync(database, user);
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var peer = fixture.CreateRuntimePeer(database);
    await runtime.Database.OpenConnectionAsync();
    await peer.Database.OpenConnectionAsync();
    var actor = await CreateSessionAsync(runtime, admin);
    var runtimePid = ((NpgsqlConnection)runtime.Database.GetDbConnection()).ProcessID;
    var peerPid = ((NpgsqlConnection)peer.Database.GetDbConnection()).ProcessID;
    var adminService = new AdministratorAccounts(runtime);
    var sessions = new SessionStore(peer);
    if (logoutFirst)
    {
      await using var holder = await peer.Database.BeginTransactionAsync();
      await sessions.RevokeAsync(actor, default);
      var blocked = ChangeAsync(adminService, actor, user, true);
      await WaitForBlockerAsync(database, runtimePid, peerPid, blocked);
      await holder.CommitAsync();
      Assert.Equal(DeactivationResult.Forbidden, await blocked);
    }
    else
    {
      await using var holder = await runtime.Database.BeginTransactionAsync();
      Assert.Equal(DeactivationResult.Applied, await ChangeAsync(adminService, actor, user, true));
      var blocked = sessions.RevokeAsync(actor, default);
      await WaitForBlockerAsync(database, peerPid, runtimePid, blocked);
      await holder.CommitAsync();
      await blocked;
    }
    Assert.Null(await new SessionStore(runtime).ReadAsync(actor, Idle, default));
    Assert.Equal(!logoutFirst, await database.Users.Where(u => u.Id == user).Select(u => u.IsDisabled).SingleAsync());
  }

  private async Task<MiniDocsDbContext> CreateDatabaseAsync()
  {
    var database = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(database);
    await using var migrator = fixture.CreateMigratorPeer(database);
    await migrator.Database.MigrateAsync();
    return database;
  }

  private async Task<Guid> CreateAdminAsync(MiniDocsDbContext database)
  {
    await using var bootstrap = fixture.CreateBootstrapPeer(database);
    return (await BootstrapAsync(bootstrap, "administrator"))!.Value;
  }

  private static async Task<Guid?> BootstrapAsync(MiniDocsDbContext bootstrap, string name)
  {
    var id = Guid.NewGuid();
    var hash = new Microsoft.AspNetCore.Identity.PasswordHasher<MiniDocsUser>()
        .HashPassword(new MiniDocsUser { Id = id }, Guid.NewGuid().ToString("N"));
    return await bootstrap.Database.SqlQuery<bool>($"""
        SELECT minidocs.command_bootstrap_administrator({id}, {name}, {name.ToUpperInvariant()}, {hash}) AS "Value"
        """).SingleAsync() ? id : null;
  }

  private static async Task<bool> PrepareAsync(MiniDocsDbContext db, Guid workspace)
  {
    var encrypted = new byte[] { 1, 2, 3 };
    return await db.Database.SqlQuery<bool>($"""
        SELECT minidocs.command_prepare_provisioning(
          {workspace}, {Guid.NewGuid()}, {Guid.NewGuid()}, 'blocked-preparation', {encrypted}, {encrypted}) AS "Value"
        """).SingleAsync();
  }

  internal static async Task<DeactivationResult> ChangeAsync(AdministratorAccounts accounts, SessionAuthority actor, Guid target, bool disabled)
  {
    var account = (await accounts.ReadVersionAsync(target, default))!;
    return await accounts.SetDisabledAsync(actor, target, account.ETag, disabled, default);
  }

  internal static Task WriteStampsAsync(MiniDocsDbContext database, Guid target) =>
      database.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.identity_users SET security_stamp = {Guid.NewGuid().ToString("N")},
            concurrency_stamp = {Guid.NewGuid().ToString("N")} WHERE id = {target}
          """);

  internal static async Task<SessionAuthority> ReadAuthorityAsync(MiniDocsDbContext db, Guid user) =>
      new(user, RandomNumberGenerator.GetBytes(32),
          (await db.Users.Where(u => u.Id == user).Select(u => u.SecurityStamp).SingleAsync())!);

  internal static async Task<SessionAuthority> CreateSessionAsync(MiniDocsDbContext db, Guid user)
  {
    var authority = await ReadAuthorityAsync(db, user);
    Assert.NotNull(await new SessionStore(db).CreateAsync(authority, null, Idle, Absolute, default));
    return authority;
  }

  internal static async Task WaitForBlockerAsync(MiniDocsDbContext observer, int waiter, int blocker, Task waiting)
  {
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (!waiting.IsCompleted)
    {
      if (await observer.Database.SqlQuery<bool>(
          $"""SELECT {blocker} = ANY(pg_blocking_pids({waiter})) AS "Value" """).SingleAsync(deadline.Token))
        return;
      await Task.Delay(15, deadline.Token);
    }
    await waiting;
    Assert.Fail("The expected backend PID never blocked the competing transaction.");
  }
}
