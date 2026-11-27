using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using VIA.MiniDocs.Api.Security;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  private const string InvitationsPath = "/api/v1/admin/invitations";
  private const string UsersPath = "/api/v1/admin/users";
  private const string RegistrationPath = "/api/v1/registration";
  private const string WorkspacePath = "/api/v1/workspace";

  [Fact]
  [Trait("Package", "D4-B")]
  public async Task AdminInvitationRegistersExactlyOnePendingWorkspaceWithoutIssuingSession()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "admin", password, admin: true);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var recorder = new RecordingDiagnostics();
    using var host = CreateHost(runtime, keys).WithWebHostBuilder(builder =>
        builder.ConfigureLogging(logging => logging.AddProvider(recorder)));
    using var client = TestClient(host);
    var anon = await GetAntiforgeryAsync(client);
    var login = await LoginAsync(client, "admin", password, anon.Cookie, anon.Token);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var admin = Cookie(login, SessionName);
    var csrf = await GetAntiforgeryAsync(client, admin);
    await AssertProblem(await RequestAsync(client, HttpMethod.Get, WorkspacePath, admin),
        HttpStatusCode.NotFound, "resource_not_found");
    await AssertProblem(await RequestAsync(client, HttpMethod.Post, InvitationsPath, admin, csrf.Cookie, null, new { }),
        HttpStatusCode.Forbidden, "antiforgery_invalid");

    var invitation = await RequestAsync(client, HttpMethod.Post, InvitationsPath, admin, csrf.Cookie, csrf.Token, new { });
    Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
    var invite = await invitation.Content.ReadFromJsonAsync<JsonElement>();
    var value = invite.GetProperty("token").GetString()!;
    Assert.Equal(43, value.Length);
    var location = invitation.Headers.Location!.OriginalString;
    Assert.DoesNotContain(value, location, StringComparison.Ordinal);
    var row = await database.UserInvitations.AsNoTracking().SingleAsync();
    Assert.InRange(row.ExpiresAt - row.CreatedAt, TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1), TimeSpan.FromHours(24));
    Assert.True(VIA.MiniDocs.Api.Security.RegistrationHttp.TryHashToken(value, out var hash));
    Assert.Equal(hash, row.TokenHash);
    var detail = await RequestAsync(client, HttpMethod.Get, location, admin);
    Assert.False((await detail.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("token", out _));

    await AssertProblem(await RequestAsync(client, HttpMethod.Post, RegistrationPath, null, anon.Cookie, anon.Token,
        new { token = value, userName = "admin", password }), HttpStatusCode.Conflict, "state_conflict");
    var registered = await RequestAsync(client, HttpMethod.Post, RegistrationPath, null, anon.Cookie, anon.Token,
        new { token = value, userName = "member", password });
    Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
    Assert.False(registered.Headers.Contains("Set-Cookie"));
    Assert.Null(registered.Headers.Location);
    var registration = await registered.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("pending", registration.GetProperty("workspaceStatus").GetString());
    var userId = registration.GetProperty("userId").GetGuid();
    var workspace = await database.Workspaces.SingleAsync(w => w.OwnerUserId == userId);
    Assert.Single(await database.Topics.ObserveAllWorkspaces().Where(t => t.WorkspaceId == workspace.Id).ToListAsync());
    Assert.Equal(userId, (await database.UserInvitations.AsNoTracking().SingleAsync()).RedeemedUserId);
    await AssertProblem(await RequestAsync(client, HttpMethod.Post, RegistrationPath, null, anon.Cookie, anon.Token,
        new { token = value, userName = "other", password }), HttpStatusCode.Gone, "invitation_expired");
    await AssertProblem(await RequestAsync(client, HttpMethod.Delete, location, admin, csrf.Cookie, csrf.Token),
        HttpStatusCode.Conflict, "state_conflict");

    var memberLogin = await LoginAsync(client, "member", password, anon.Cookie, anon.Token);
    var member = Cookie(memberLogin, SessionName);
    var status = await RequestAsync(client, HttpMethod.Get, WorkspacePath + $"?workspaceId={Guid.NewGuid()}", member);
    Assert.True(status.Headers.CacheControl?.NoStore);
    Assert.Equal(workspace.Id, (await status.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    await AssertProblem(await RequestAsync(client, HttpMethod.Get, UsersPath, member), HttpStatusCode.Forbidden, "forbidden");
    Assert.DoesNotContain(recorder.Output, entry => entry.Contains(value, StringComparison.Ordinal) ||
        entry.Contains(password, StringComparison.Ordinal) ||
        entry.Contains(Convert.ToHexString(hash), StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Package", "D4-B")]
  public async Task AdminListsBindCursorsAndPatchUsesStrongIfMatch()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "admin", password, admin: true);
    var member = await CreateUserAsync(database, "member", password);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var anon = await GetAntiforgeryAsync(client);
    var login = await LoginAsync(client, "admin", password, anon.Cookie, anon.Token);
    var admin = Cookie(login, SessionName);
    var csrf = await GetAntiforgeryAsync(client, admin);
    var first = await RequestAsync(client, HttpMethod.Get, UsersPath + "?limit=1", admin);
    Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    var firstPage = await first.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Single(firstPage.GetProperty("items").EnumerateArray());
    var cursor = firstPage.GetProperty("nextCursor").GetString()!;
    var next = await RequestAsync(client, HttpMethod.Get, UsersPath + "?limit=1&cursor=" + cursor, admin);
    Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    var nextPage = await next.Content.ReadFromJsonAsync<JsonElement>();
    Assert.NotEqual(firstPage.GetProperty("items")[0].GetProperty("id").GetGuid(),
        nextPage.GetProperty("items")[0].GetProperty("id").GetGuid());
    foreach (var query in new[] { UsersPath + "?limit=2&cursor=" + cursor,
                 UsersPath + "?limit=1&cursor=invalid" + cursor, InvitationsPath + "?limit=1&cursor=" + cursor })
      await AssertProblem(await RequestAsync(client, HttpMethod.Get, query, admin), HttpStatusCode.BadRequest, "cursor_invalid");

    var target = $"{UsersPath}/{member:D}";
    var tag = (await new AdministratorAccounts(runtime).ReadVersionAsync(member, default))!.ETag;
    var patch = new { isDisabled = true };
    await AssertProblem(await RequestAsync(client, HttpMethod.Patch, target, admin, csrf.Cookie, csrf.Token, patch),
        HttpStatusCode.PreconditionRequired, "precondition_required");
    await AssertProblem(await RequestAsync(client, HttpMethod.Patch, target, admin, csrf.Cookie, csrf.Token, patch, "*"),
        HttpStatusCode.BadRequest, "validation_failed");
    var updated = await RequestAsync(client, HttpMethod.Patch, target, admin, csrf.Cookie, csrf.Token, patch, tag);
    Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
    Assert.True(body.GetProperty("isDisabled").GetBoolean());
    Assert.Equal(updated.Headers.ETag!.ToString(), body.GetProperty("etag").GetString());
    Assert.False(body.TryGetProperty("securityStamp", out _));
    Assert.False(body.TryGetProperty("concurrencyStamp", out _));
    await AssertProblem(await RequestAsync(client, HttpMethod.Patch, target, admin, csrf.Cookie, csrf.Token, patch, tag),
        HttpStatusCode.PreconditionFailed, "version_mismatch");
  }

  private static async Task<HttpResponseMessage> RequestAsync(HttpClient client, HttpMethod method, string path,
      string? session = null, string? csrfCookie = null, string? token = null, object? payload = null, string? tag = null)
  {
    using var request = new HttpRequestMessage(method, path);
    var cookies = new[] { session, csrfCookie }.OfType<string>().ToArray();
    if (cookies.Length != 0) AddCookies(request, cookies);
    if (token is not null) request.Headers.Add(Header, token);
    if (tag is not null) request.Headers.TryAddWithoutValidation("If-Match", tag);
    if (payload is not null) request.Content = JsonContent.Create(payload);
    return await client.SendAsync(request);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  [Trait("Package", "D4-B")]
  public async Task ARegistrationHoldsItsInvitationAndExposesNoPartialCommit(bool revokeWaiter)
  {
    await using var database = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var peer = fixture.CreateRuntimePeer(database);
    var password = Guid.NewGuid().ToString("N");
    var seed = await SeedInvitationAsync(database, runtime, password);
    var pause = new PauseRegistration();
    var wait = new ObserveSql(revokeWaiter ? "command_revoke_invitation(" : "command_lock_valid_invitation(");
    using var keys = new SessionKeyRingFixture();
    using var firstHost = CreateHost(runtime, keys, pause);
    using var firstClient = TestClient(firstHost);
    var firstCsrf = await GetAntiforgeryAsync(firstClient);
    using var peerHost = CreateHost(peer, keys, wait);
    using var peerClient = TestClient(peerHost);
    var peerCsrf = await GetAntiforgeryAsync(peerClient);
    string? adminCookie = null;
    if (revokeWaiter)
    {
      var admin = await LoginAsync(peerClient, "administrator", password, peerCsrf.Cookie, peerCsrf.Token);
      adminCookie = Cookie(admin, SessionName);
      peerCsrf = await GetAntiforgeryAsync(peerClient, adminCookie);
    }

    var first = RequestAsync(firstClient, HttpMethod.Post, RegistrationPath, null, firstCsrf.Cookie, firstCsrf.Token,
        new { token = seed.Token, userName = "first-member", password });
    try
    {
      var firstPid = await pause.Pid.Task.WaitAsync(TimeSpan.FromSeconds(10));
      Assert.False(await database.Users.AnyAsync(u => u.UserName == "first-member"));
      Assert.Empty(await database.Workspaces.ToListAsync());
      Assert.Empty(await database.Topics.ObserveAllWorkspaces().ToListAsync());
      Assert.Null((await database.UserInvitations.AsNoTracking().SingleAsync()).RedeemedAt);
      var waiting = revokeWaiter
          ? RequestAsync(peerClient, HttpMethod.Delete, $"{InvitationsPath}/{seed.Id:D}", adminCookie, peerCsrf.Cookie, peerCsrf.Token)
          : RequestAsync(peerClient, HttpMethod.Post, RegistrationPath, null, peerCsrf.Cookie, peerCsrf.Token,
              new { token = seed.Token, userName = "second-member", password });
      var waitingPid = await wait.Pid.Task.WaitAsync(TimeSpan.FromSeconds(10));
      await PostgresSessionTests.WaitForBlockerAsync(database, waitingPid, firstPid, waiting);
      pause.Release.TrySetResult();
      Assert.Equal(HttpStatusCode.Created, (await first).StatusCode);
      Assert.Equal(revokeWaiter ? HttpStatusCode.Conflict : HttpStatusCode.Gone, (await waiting).StatusCode);
      Assert.Single(await database.Workspaces.ToListAsync());
      Assert.Single(await database.Topics.ObserveAllWorkspaces().ToListAsync());
    }
    finally { pause.Release.TrySetResult(); }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  [Trait("Package", "D4-B")]
  public async Task FailureBeforeWorkspaceRollsBackAndLostCommitIsObservedWithoutRetry(bool commitLost)
  {
    await using var database = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    var password = Guid.NewGuid().ToString("N");
    var invitation = await SeedInvitationAsync(database, runtime, password);
    IInterceptor failure = commitLost ? new LoseCommitAcknowledgment() : new FailWorkspace();
    using var host = CreateHost(runtime, keys, failure);
    using var client = TestClient(host);
    var csrf = await GetAntiforgeryAsync(client);
    var input = new { token = invitation.Token, userName = "member", password };
    var response = await RequestAsync(client, HttpMethod.Post, RegistrationPath, null, csrf.Cookie, csrf.Token, input);
    await AssertProblem(response, commitLost ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.InternalServerError,
        commitLost ? "dependency_unavailable" : "internal_error");
    Assert.False(response.Headers.Contains("Set-Cookie"));
    Assert.Equal(commitLost ? 1 : 0, await database.Users.CountAsync(u => u.UserName == "member"));
    Assert.Equal(commitLost ? 1 : 0, await database.Workspaces.CountAsync());
    Assert.Equal(commitLost ? 1 : 0, await database.Topics.ObserveAllWorkspaces().CountAsync());
    Assert.Equal(commitLost, (await database.UserInvitations.AsNoTracking().SingleAsync()).RedeemedAt is not null);
    if (commitLost)
      await AssertProblem(await RequestAsync(client, HttpMethod.Post, RegistrationPath, null, csrf.Cookie, csrf.Token, input),
          HttpStatusCode.Gone, "invitation_expired");
  }

  private static async Task<(Guid Id, string Token, SessionAuthority Actor)> SeedInvitationAsync(
      VIA.MiniDocs.Infrastructure.MiniDocsDbContext database, VIA.MiniDocs.Infrastructure.MiniDocsDbContext runtime,
      string password)
  {
    var admin = await CreateUserAsync(database, "administrator", password, admin: true);
    var session = await PostgresSessionTests.CreateSessionAsync(runtime, admin);
    var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    var id = Guid.NewGuid();
    Assert.Equal(InvitationWrite.Applied, (await new AdministratorInvitations(runtime).CreateAsync(
        session, id, System.Security.Cryptography.SHA256.HashData(bytes), null, default)).Status);
    return (id, Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(bytes), session);
  }

  private sealed class ObserveSql(string sql) : DbCommandInterceptor
  {
    internal TaskCompletionSource<int> Pid { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
      if (command.CommandText.Contains(sql, StringComparison.Ordinal))
        Pid.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
      return ValueTask.FromResult(result);
    }
  }

  private sealed class PauseRegistration : DbCommandInterceptor
  {
    internal TaskCompletionSource<int> Pid { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
      if (command.CommandText.Contains("command_register_workspace(", StringComparison.Ordinal))
      {
        Pid.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
        await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
      }
      return result;
    }
  }

  private sealed class FailWorkspace : DbCommandInterceptor
  {
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
        command.CommandText.Contains("command_register_workspace(", StringComparison.Ordinal)
            ? throw new InvalidOperationException("Injected workspace failure") : ValueTask.FromResult(result);
  }

  [Fact]
  [Trait("Package", "D4-B")]
  public async Task RevocationWinningTheRowLockPreventsRegistration()
  {
    await using var database = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var peer = fixture.CreateRuntimePeer(database);
    var password = Guid.NewGuid().ToString("N");
    var seed = await SeedInvitationAsync(database, runtime, password);
    var pause = new PauseBeforeCommit();
    var wait = new ObserveSql("command_lock_valid_invitation(");
    using var keys = new SessionKeyRingFixture();
    using var writer = CreateHost(runtime, keys, pause);
    using var adminClient = TestClient(writer);
    var anonymous = await GetAntiforgeryAsync(adminClient);
    var login = await LoginAsync(adminClient, "administrator", password, anonymous.Cookie, anonymous.Token);
    var admin = Cookie(login, SessionName);
    var csrf = await GetAntiforgeryAsync(adminClient, admin);
    using var reader = CreateHost(peer, keys, wait);
    using var client = TestClient(reader);
    var publicCsrf = await GetAntiforgeryAsync(client);
    pause.Enabled = true;
    var revoking = RequestAsync(adminClient, HttpMethod.Delete, $"{InvitationsPath}/{seed.Id:D}", admin, csrf.Cookie, csrf.Token);
    try
    {
      var blocker = await pause.Pid.Task.WaitAsync(TimeSpan.FromSeconds(10));
      var pending = RequestAsync(client, HttpMethod.Post, RegistrationPath, null, publicCsrf.Cookie, publicCsrf.Token,
          new { token = seed.Token, userName = "member", password });
      await PostgresSessionTests.WaitForBlockerAsync(database,
          await wait.Pid.Task.WaitAsync(TimeSpan.FromSeconds(10)), blocker, pending);
      pause.Release.TrySetResult();
      Assert.Equal(HttpStatusCode.NoContent, (await revoking).StatusCode);
      await AssertProblem(await pending, HttpStatusCode.Gone, "invitation_expired");
      Assert.Empty(await database.Workspaces.ToListAsync());
    }
    finally { pause.Release.TrySetResult(); }
  }

  [Fact]
  [Trait("Package", "D4-B")]
  public async Task ExpiryWhileWaitingIsEvaluatedOnDatabaseClock()
  {
    await using var database = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var holder = fixture.CreatePeer(database);
    var password = Guid.NewGuid().ToString("N");
    var seed = await SeedInvitationAsync(database, runtime, password);
    await using var transaction = await holder.Database.BeginTransactionAsync();
    await holder.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.user_invitations SET expires_at = clock_timestamp() + interval '3 seconds' WHERE id = {seed.Id}
        """);
    var blocker = ((NpgsqlConnection)holder.Database.GetDbConnection()).ProcessID;
    var wait = new ObserveSql("command_lock_valid_invitation(");
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys, wait);
    using var client = TestClient(host);
    var csrf = await GetAntiforgeryAsync(client);
    var pending = RequestAsync(client, HttpMethod.Post, RegistrationPath, null, csrf.Cookie, csrf.Token,
        new { token = seed.Token, userName = "member", password });
    await PostgresSessionTests.WaitForBlockerAsync(database,
        await wait.Pid.Task.WaitAsync(TimeSpan.FromSeconds(10)), blocker, pending);
    await holder.Database.ExecuteSqlInterpolatedAsync($"""
        SELECT pg_sleep(GREATEST(0, EXTRACT(EPOCH FROM expires_at - clock_timestamp())::double precision))
        FROM minidocs.user_invitations WHERE id = {seed.Id}
        """);
    await transaction.CommitAsync();
    await AssertProblem(await pending, HttpStatusCode.Gone, "invitation_expired");
    Assert.Empty(await database.Workspaces.ToListAsync());
  }

  private sealed class PauseBeforeCommit : DbTransactionInterceptor
  {
    internal bool Enabled { get; set; }
    internal TaskCompletionSource<int> Pid { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
      if (Enabled)
      {
        Pid.TrySetResult(((NpgsqlConnection)transaction.Connection!).ProcessID);
        await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
      }
      return result;
    }
  }

  public enum AuthorityLoss { Disabled, Role, Stamp, Logout }

  [Theory]
  [InlineData(AuthorityLoss.Disabled, false, false)]
  [InlineData(AuthorityLoss.Disabled, true, false)]
  [InlineData(AuthorityLoss.Disabled, false, true)]
  [InlineData(AuthorityLoss.Disabled, true, true)]
  [InlineData(AuthorityLoss.Role, false, false)]
  [InlineData(AuthorityLoss.Role, true, false)]
  [InlineData(AuthorityLoss.Role, false, true)]
  [InlineData(AuthorityLoss.Role, true, true)]
  [InlineData(AuthorityLoss.Stamp, false, false)]
  [InlineData(AuthorityLoss.Stamp, true, false)]
  [InlineData(AuthorityLoss.Stamp, false, true)]
  [InlineData(AuthorityLoss.Stamp, true, true)]
  [InlineData(AuthorityLoss.Logout, false, false)]
  [InlineData(AuthorityLoss.Logout, true, false)]
  [InlineData(AuthorityLoss.Logout, false, true)]
  [InlineData(AuthorityLoss.Logout, true, true)]
  [Trait("Package", "D4-B")]
  public async Task InvitationWriteSerializesWithLossOfAdministratorAuthority(AuthorityLoss loss, bool lossFirst, bool revoke)
  {
    await using var database = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var mutator = loss == AuthorityLoss.Role ? fixture.CreatePeer(database) : fixture.CreateRuntimePeer(database);
    var password = Guid.NewGuid().ToString("N");
    var seed = await SeedInvitationAsync(database, runtime, password);
    var secondId = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(database, secondId, "second-admin", "admin_only");
    await PostgresSessionTests.WriteStampsAsync(database, secondId);
    await database.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.identity_user_roles(user_id,role_id)
        SELECT {secondId},id FROM minidocs.identity_roles WHERE normalized_name='ADMINISTRATOR'
        """);
    var second = await PostgresSessionTests.CreateSessionAsync(mutator, secondId);
    var pause = new PauseBeforeCommit();
    var observer = new ObserveSql("lock_admin_authority(");
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys, lossFirst ? observer : pause);
    using var client = TestClient(host);
    var start = await GetAntiforgeryAsync(client);
    var loggedIn = await LoginAsync(client, "administrator", password, start.Cookie, start.Token);
    var cookie = Cookie(loggedIn, SessionName);
    var csrf = await GetAntiforgeryAsync(client, cookie);
    var actualHandle = await runtime.Database.SqlQuery<byte[]>($"""
        SELECT handle_hash AS "Value" FROM minidocs.identity_sessions
        WHERE user_id = {seed.Actor.UserId} AND handle_hash <> {seed.Actor.HandleHash}
        """).SingleAsync();
    var actor = seed.Actor with { HandleHash = actualHandle };
    await using var transaction = await mutator.Database.BeginTransactionAsync();
    var mutatorPid = ((NpgsqlConnection)mutator.Database.GetDbConnection()).ProcessID;

    Task<HttpResponseMessage> SendWrite() => revoke
        ? RequestAsync(client, HttpMethod.Delete, $"{InvitationsPath}/{seed.Id:D}", cookie, csrf.Cookie, csrf.Token)
        : RequestAsync(client, HttpMethod.Post, InvitationsPath, cookie, csrf.Cookie, csrf.Token, new { });

    async Task RemoveAuthorityAsync()
    {
      switch (loss)
      {
        case AuthorityLoss.Disabled:
          Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
              new AdministratorAccounts(mutator), second, actor.UserId, true));
          break;
        case AuthorityLoss.Role:
          // Role administration is reserved for a later interface. Reproduce a
          // trusted lifecycle write in the test using the same advisory boundary.
          await mutator.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3841, 401)");
          await mutator.Database.ExecuteSqlInterpolatedAsync(
              $"DELETE FROM minidocs.identity_user_roles WHERE user_id = {actor.UserId}");
          break;
        case AuthorityLoss.Stamp:
          await PostgresSessionTests.WriteStampsAsync(mutator, actor.UserId);
          break;
        case AuthorityLoss.Logout:
          await new SessionStore(mutator).RevokeAsync(actor, default);
          break;
      }
    }

    if (lossFirst)
    {
      await RemoveAuthorityAsync();
      var blocked = SendWrite();
      await PostgresSessionTests.WaitForBlockerAsync(database,
          await observer.Pid.Task.WaitAsync(TimeSpan.FromSeconds(10)), mutatorPid, blocked);
      await transaction.CommitAsync();
      await AssertProblem(await blocked, HttpStatusCode.Forbidden, "forbidden");
    }
    else
    {
      pause.Enabled = true;
      var write = SendWrite();
      try
      {
        var writerPid = await pause.Pid.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var blocked = RemoveAuthorityAsync();
        await PostgresSessionTests.WaitForBlockerAsync(database, mutatorPid, writerPid, blocked);
        pause.Release.TrySetResult();
        Assert.Equal(revoke ? HttpStatusCode.NoContent : HttpStatusCode.Created, (await write).StatusCode);
        await blocked;
        await transaction.CommitAsync();
      }
      finally { pause.Release.TrySetResult(); }
    }
    Assert.Equal(!revoke && !lossFirst ? 2 : 1, await database.UserInvitations.CountAsync());
    Assert.Equal(revoke && !lossFirst,
        (await database.UserInvitations.AsNoTracking().SingleAsync(i => i.Id == seed.Id)).RevokedAt is not null);
  }
}
