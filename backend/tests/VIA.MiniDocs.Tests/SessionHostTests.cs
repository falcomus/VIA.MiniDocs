using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Package", "D4-A")]
[Trait("Category", "PostgreSQL")]
public sealed partial class SessionHostTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  private const string SessionPath = "/api/v1/session";
  private const string AntiforgeryPath = "/api/v1/antiforgery";
  private const string Header = "X-CSRF-TOKEN";
  private const string SessionName = "__Host-MiniDocsSession";
  private const string CsrfName = "__Host-Antiforgery";

  [Fact]
  public async Task HttpsLoginCsrfAndLogoutAreFailClosedAndCannotBeReplayed()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "customer", password);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var rsa = RSA.Create(2048);
    var certRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var san = new SubjectAlternativeNameBuilder();
    san.AddIpAddress(IPAddress.Loopback);
    certRequest.CertificateExtensions.Add(san.Build());
    using var selfSigned = certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
    using var serverCertificate = X509CertificateLoader.LoadPkcs12(selfSigned.Export(X509ContentType.Pkcs12),
        null, X509KeyStorageFlags.DefaultKeySet);
    host.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(serverCertificate)));
    var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    var hash = serverCertificate.GetCertHashString();
    using var client = new HttpClient(new HttpClientHandler
    {
      AllowAutoRedirect = false,
      UseCookies = false,
      ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString() == hash
    }) { BaseAddress = new Uri(address) };

    await AssertProblem(await client.GetAsync(SessionPath), HttpStatusCode.Unauthorized, "authentication_required");
    var (antiforgery, anonymous) = await GetAntiforgeryAsync(client);
    AssertCookie(antiforgery, CsrfName, "strict");
    await AssertProblem(await LoginAsync(client, "customer", password, antiforgery, null),
        HttpStatusCode.Forbidden, "antiforgery_invalid");
    var login = await LoginAsync(client, "customer", password, antiforgery, anonymous);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    Assert.True(login.Headers.CacheControl?.NoStore);
    var session = Cookie(login, SessionName);
    AssertCookie(session, SessionName, "lax");
    Assert.DoesNotContain("expires=", session, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(password, await login.Content.ReadAsStringAsync());
    await AssertProblem(await LogoutAsync(client, session, antiforgery, anonymous),
        HttpStatusCode.Forbidden, "antiforgery_invalid");
    var (authenticatedCookie, authenticated) = await GetAntiforgeryAsync(client, session);
    var replacement = await LoginAsync(client, "customer", password, authenticatedCookie, authenticated, session);
    Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
    var replacementSession = Cookie(replacement, SessionName);
    Assert.NotEqual(session.Split(';')[0], replacementSession.Split(';')[0]);
    await AssertProblem(await LogoutAsync(client, session, authenticatedCookie, authenticated),
        HttpStatusCode.Unauthorized, "authentication_required");
    var (secondCookie, secondToken) = await GetAntiforgeryAsync(client, replacementSession);
    await AssertProblem(await LogoutAsync(client, replacementSession, authenticatedCookie, authenticated),
        HttpStatusCode.Forbidden, "antiforgery_invalid");
    var loggedOut = await LogoutAsync(client, replacementSession, secondCookie, secondToken);
    Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);
    await AssertProblem(await LogoutAsync(client, replacementSession, secondCookie, secondToken),
        HttpStatusCode.Unauthorized, "authentication_required");
    var next = await GetAntiforgeryAsync(client);
    Assert.NotEqual(authenticated, next.Token);
  }

  [Fact]
  public async Task UnconfirmedCommitDoesNotPublishACookie()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var user = await CreateUserAsync(database, "commit-ack", password);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys, new LoseCommitAcknowledgment());
    using var client = TestClient(host);
    var (cookie, token) = await GetAntiforgeryAsync(client);
    var login = await LoginAsync(client, "commit-ack", password, cookie, token);
    await AssertProblem(login, HttpStatusCode.ServiceUnavailable, "dependency_unavailable");
    Assert.False(login.Headers.Contains("Set-Cookie"));
    Assert.Equal(1, await database.Database.SqlQuery<int>($"""
        SELECT count(*)::integer AS "Value" FROM minidocs.identity_sessions WHERE user_id = {user}
        """).SingleAsync());
  }

  [Fact]
  public async Task RoleChangesAreVisibleWithoutIssuingNewCookie()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var user = await CreateUserAsync(database, "administrator", password, admin: true);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var anonymous = await GetAntiforgeryAsync(client);
    var login = await LoginAsync(client, "administrator", password, anonymous.Cookie, anonymous.Token);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var issued = await login.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("ADMINISTRATOR", issued.GetProperty("roles").EnumerateArray().Single().GetString());
    Assert.Equal(JsonValueKind.Null, issued.GetProperty("workspaceId").ValueKind);
    await database.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM minidocs.identity_user_roles WHERE user_id = {user}");
    using var get = new HttpRequestMessage(HttpMethod.Get, SessionPath);
    AddCookies(get, Cookie(login, SessionName));
    var status = await client.SendAsync(get);
    Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    var refreshed = await status.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal(0, refreshed.GetProperty("roles").GetArrayLength());
  }

  [Fact]
  public async Task BootstrapUsesIdentityPasswordPolicyAndCreatesAnAdminOnlyLogin()
  {
    await using var database = await CreateDatabaseAsync();
    await using var bootstrap = fixture.CreateBootstrapPeer(database);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var bootstrapKeys = new SessionKeyRingFixture();
    using var runtimeKeys = new SessionKeyRingFixture();
    using var local = CreateHost(bootstrap, bootstrapKeys);
    var password = Guid.NewGuid().ToString("N");
    await using (var scope = local.Services.CreateAsyncScope())
    {
      var operation = scope.ServiceProvider.GetRequiredService<AdministratorBootstrap>();
      Assert.False(await operation.CreateAsync("first-admin", "too-short", default));
      Assert.True(await operation.CreateAsync("first-admin", password, default));
      Assert.False(await operation.CreateAsync("second-admin", password, default));
    }
    using var host = CreateHost(runtime, runtimeKeys);
    using var client = TestClient(host);
    var cookie = await GetAntiforgeryAsync(client);
    var login = await LoginAsync(client, "first-admin", password, cookie.Cookie, cookie.Token);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var payload = await login.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("first-admin", payload.GetProperty("displayName").GetString());
    Assert.Equal(JsonValueKind.Null, payload.GetProperty("workspaceId").ValueKind);
    Assert.Equal(JsonValueKind.Null, payload.GetProperty("workspaceStatus").ValueKind);
    Assert.Equal("ADMINISTRATOR", payload.GetProperty("roles").EnumerateArray().Single().GetString());
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task LoginCannotPublishValidSessionAcrossAdministrativeDeactivation(bool afterCommit, bool reenable)
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var user = await CreateUserAsync(database, "account", password);
    var admin = await CreateUserAsync(database, "administrator", Guid.NewGuid().ToString("N"), admin: true);
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var otherConnection = fixture.CreateRuntimePeer(database);
    var adminSession = await PostgresSessionTests.CreateSessionAsync(otherConnection, admin);
    var checkpoint = new LoginCheckpoint();
    IInterceptor interceptor = afterCommit ? new PauseAfterCommit(checkpoint) : new PauseBeforeCreate(checkpoint);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys, interceptor);
    using var client = TestClient(host);
    var (cookie, token) = await GetAntiforgeryAsync(client);
    var pending = LoginAsync(client, "account", password, cookie, token);

    try
    {
      await checkpoint.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
          new AdministratorAccounts(otherConnection), adminSession, user, true));
      if (reenable)
        Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
            new AdministratorAccounts(otherConnection), adminSession, user, false));
    }
    finally { checkpoint.Release.TrySetResult(); }

    var login = await pending;
    if (afterCommit)
    {
      Assert.Equal(HttpStatusCode.OK, login.StatusCode);
      using var replay = new HttpRequestMessage(HttpMethod.Get, SessionPath);
      AddCookies(replay, Cookie(login, SessionName));
      await AssertProblem(await client.SendAsync(replay), HttpStatusCode.Unauthorized, "authentication_required");
    }
    else
    {
      await AssertProblem(login, HttpStatusCode.Unauthorized, "authentication_required");
      Assert.False(login.Headers.Contains("Set-Cookie"));
    }
    if (reenable)
    {
      var fresh = await GetAntiforgeryAsync(client);
      var newLogin = await LoginAsync(client, "account", password, fresh.Cookie, fresh.Token);
      Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
      Assert.Equal(HttpStatusCode.OK, (await GetSessionAsync(client, Cookie(newLogin, SessionName))).StatusCode);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task IdentityRehashOrLockoutCannotOverwriteAConcurrentDeactivation(bool rehash)
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var user = await CreateUserAsync(database, "routine-update", password, iterations: rehash ? 1_000 : 100_000);
    var original = await database.Users.Where(u => u.Id == user).Select(u => u.PasswordHash).SingleAsync();
    if (!rehash)
      await database.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.identity_users SET access_failed_count = 4 WHERE id = {user}
          """);
    var admin = await CreateUserAsync(database, "administrator", Guid.NewGuid().ToString("N"), admin: true);
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var adminConnection = fixture.CreateRuntimePeer(database);
    var adminSession = await PostgresSessionTests.CreateSessionAsync(adminConnection, admin);
    await adminConnection.Database.OpenConnectionAsync();
    var adminPid = ((NpgsqlConnection)adminConnection.Database.GetDbConnection()).ProcessID;
    var observe = new ObserveIdentityUpdate();
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys, observe);
    using var client = TestClient(host);
    var (cookie, token) = await GetAntiforgeryAsync(client);
    await using var transaction = await adminConnection.Database.BeginTransactionAsync();
    Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
        new AdministratorAccounts(adminConnection), adminSession, user, true));
    var pending = LoginAsync(client, "routine-update", rehash ? password : Guid.NewGuid().ToString("N"), cookie, token);
    var loginPid = await observe.Pid.Task.WaitAsync(TimeSpan.FromSeconds(10));
    await PostgresSessionTests.WaitForBlockerAsync(database, loginPid, adminPid, pending);
    await transaction.CommitAsync();
    await AssertProblem(await pending, HttpStatusCode.Unauthorized, "authentication_required");
    Assert.Equal(original, await database.Users.Where(u => u.Id == user).Select(u => u.PasswordHash).SingleAsync());
    Assert.True(await database.Users.AnyAsync(u => u.Id == user && u.IsDisabled));
    if (!rehash)
      Assert.True(await database.Users.AnyAsync(u => u.Id == user && u.AccessFailedCount == 4 && u.LockoutEnd == null));
  }

  [Fact]
  public async Task LockoutDoesNotRevealAccountForWrongPasswordsAndLimiterCountsNormalizedName()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var user = await CreateUserAsync(database, "locked", password);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var (cookie, token) = await GetAntiforgeryAsync(client);
    await database.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.identity_users SET access_failed_count = 4 WHERE id = {user}
        """);
    var wrong = "test-" + Guid.NewGuid().ToString("N");
    await AssertProblem(await LoginAsync(client, "locked", wrong, cookie, token),
        HttpStatusCode.Unauthorized, "authentication_required");
    Assert.True(await database.Users.AnyAsync(u => u.Id == user && u.LockoutEnd > DateTimeOffset.UtcNow));
    await AssertProblem(await LoginAsync(client, "locked", password, cookie, token),
        HttpStatusCode.Locked, "account_locked");
    for (var i = 0; i < 3; i++)
      await AssertProblem(await LoginAsync(client, "LOCKED", wrong, cookie, token),
          HttpStatusCode.Unauthorized, "authentication_required");
    var limited = await LoginAsync(client, "locked", password, cookie, token);
    await AssertProblem(limited, HttpStatusCode.TooManyRequests, "rate_limited");
    Assert.InRange(limited.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
  }

  [Fact]
  public async Task RingSurvivesRestartAndNewWriterButForeignRingRejectsOldCookies()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "restart", password);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var foreignKeys = new SessionKeyRingFixture();
    string oldTicket;
    (string Cookie, string Token) oldAntiforgery;
    Guid oldKeyId;
    using (var host = CreateHost(runtime, keys))
    using (var client = TestClient(host))
    {
      var anonymous = await GetAntiforgeryAsync(client);
      var login = await LoginAsync(client, "restart", password, anonymous.Cookie, anonymous.Token);
      Assert.Equal(HttpStatusCode.OK, login.StatusCode);
      oldTicket = Cookie(login, SessionName);
      oldKeyId = Assert.Single(host.Services.GetRequiredService<IKeyManager>().GetAllKeys()).KeyId;
      Assert.Equal(oldKeyId, GetTicketKeyId(oldTicket));
      oldAntiforgery = await GetAntiforgeryAsync(client, oldTicket);
    }
    using (var host = CreateHost(runtime, foreignKeys))
    using (var client = TestClient(host))
    {
      await AssertProblem(await GetSessionAsync(client, oldTicket), HttpStatusCode.Unauthorized, "authentication_required");
      await AssertProblem(await LoginAsync(client, "restart", password, oldAntiforgery.Cookie, oldAntiforgery.Token),
          HttpStatusCode.Forbidden, "antiforgery_invalid");
    }
    var next = keys.AddCertificate();
    Guid newKeyId;
    using (var host = CreateHost(runtime, keys, active: next, previous: keys.CertificatePath, allowCreation: false))
    using (var client = TestClient(host))
    {
      Assert.Equal(HttpStatusCode.OK, (await GetSessionAsync(client, oldTicket)).StatusCode);
      var now = DateTimeOffset.UtcNow;
      newKeyId = host.Services.GetRequiredService<IKeyManager>().CreateNewKey(now, now.AddDays(90)).KeyId;
    }
    Assert.NotEqual(oldKeyId, newKeyId);

    string newTicket;
    (string Cookie, string Token) newAntiforgery;
    using (var host = CreateHost(runtime, keys, active: next, previous: keys.CertificatePath, allowCreation: false))
    using (var client = TestClient(host))
    {
      var anonymous = await GetAntiforgeryAsync(client);
      var login = await LoginAsync(client, "restart", password, anonymous.Cookie, anonymous.Token);
      Assert.Equal(HttpStatusCode.OK, login.StatusCode);
      newTicket = Cookie(login, SessionName);
      Assert.Equal(newKeyId, GetTicketKeyId(newTicket));
      newAntiforgery = await GetAntiforgeryAsync(client, newTicket);
    }

    // Only the rotated XML entry and the new certificate are present. An
    // authenticated request proves that this exact file decrypts with that certificate.
    var isolated = keys.CreateRingDirectory();
    var newKeyFile = $"key-{newKeyId:D}.xml";
    File.Copy(Path.Combine(keys.KeyDirectory, newKeyFile), Path.Combine(isolated, newKeyFile));
    using (var host = CreateHost(runtime, keys, active: next, allowCreation: false, directory: isolated))
    using (var client = TestClient(host))
    {
      Assert.Equal(HttpStatusCode.OK, (await GetSessionAsync(client, newTicket)).StatusCode);
      await AssertProblem(await GetSessionAsync(client, oldTicket), HttpStatusCode.Unauthorized, "authentication_required");
    }
    using (var wrongCertificate = CreateHost(runtime, keys, allowCreation: false, directory: isolated))
      Assert.Contains("could not be initialized",
          Assert.Throws<InvalidOperationException>(() => TestClient(wrongCertificate)).Message);
    using (var missingOldCertificate = CreateHost(runtime, keys, active: next, allowCreation: false))
      Assert.Contains("could not be initialized",
          Assert.Throws<InvalidOperationException>(() => TestClient(missingOldCertificate)).Message);

    using (var host = CreateHost(runtime, keys, active: next, previous: keys.CertificatePath, allowCreation: false))
    using (var client = TestClient(host))
    {
      Assert.Equal(HttpStatusCode.NoContent,
          (await LogoutAsync(client, oldTicket, oldAntiforgery.Cookie, oldAntiforgery.Token)).StatusCode);
      Assert.Equal(HttpStatusCode.NoContent,
          (await LogoutAsync(client, newTicket, newAntiforgery.Cookie, newAntiforgery.Token)).StatusCode);
    }
  }

  private static Guid GetTicketKeyId(string cookie)
  {
    var value = cookie.Split(';')[0][(SessionName.Length + 1)..];
    var bytes = WebEncoders.Base64UrlDecode(value);
    // ASP.NET Core's four-byte Data Protection header precedes the key identifier.
    Assert.Equal(new byte[] { 0x09, 0xF0, 0xC9, 0xF0 }, bytes[..4]);
    return new Guid(bytes.AsSpan(4, 16));
  }

  [Fact]
  public async Task DatabaseFailureDuringGuardReturnsRedacted503WithNoAuthorization()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "outage", password);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var recorder = new RecordingDiagnostics();
    using var host = CreateHost(runtime, keys).WithWebHostBuilder(builder =>
        builder.ConfigureLogging(logging => logging.AddProvider(recorder)));
    using var client = TestClient(host);
    var (csrf, token) = await GetAntiforgeryAsync(client);
    var login = await LoginAsync(client, "outage", password, csrf, token);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    using var guard = new HttpRequestMessage(HttpMethod.Get, SessionPath);
    AddCookies(guard, Cookie(login, SessionName));
    var databaseName = database.Database.GetDbConnection().Database;
    await using var otherDatabase = await fixture.CreateDatabaseAsync();
    var connection = otherDatabase.Database.GetDbConnection();
    await connection.OpenAsync();
    await using var terminate = connection.CreateCommand();
    // Name comes exclusively from the fixture's UUID-based CREATE DATABASE.
    terminate.CommandText = $"""ALTER DATABASE "{databaseName}" WITH ALLOW_CONNECTIONS false""";
    await terminate.ExecuteNonQueryAsync();
    var error = await client.SendAsync(guard);
    await AssertProblem(error, HttpStatusCode.ServiceUnavailable, "dependency_unavailable");
    Assert.DoesNotContain(databaseName, await error.Content.ReadAsStringAsync());
    Assert.False(error.Headers.Contains("Set-Cookie"));
    var cookieValue = Cookie(login, SessionName).Split(';')[0][(SessionName.Length + 1)..];
    Assert.DoesNotContain(recorder.Output, entry => entry.Contains(password, StringComparison.Ordinal)
        || entry.Contains(token, StringComparison.Ordinal) || entry.Contains(cookieValue, StringComparison.Ordinal));
    Assert.Contains(recorder.Output, entry => entry.Contains("Session request failed (503)", StringComparison.Ordinal));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task LogoutDifferentiatesUnconfirmedFailureFromParallelConfirmedRevocation(bool parallelRevoke)
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var user = await CreateUserAsync(database, "logout-test", password);
    await using var runtime = fixture.CreateRuntimePeer(database);
    await using var peer = fixture.CreateRuntimePeer(database);
    var checkpoint = parallelRevoke ? new LoginCheckpoint() : null;
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys, new PauseOrFailBeforeRevoke(checkpoint));
    using var client = TestClient(host);
    var (cookie, token) = await GetAntiforgeryAsync(client);
    var login = await LoginAsync(client, "logout-test", password, cookie, token);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var session = Cookie(login, SessionName);
    var authenticated = await GetAntiforgeryAsync(client, session);
    var logout = LogoutAsync(client, session, authenticated.Cookie, authenticated.Token);
    if (checkpoint is null)
    {
      var response = await logout;
      await AssertProblem(response, HttpStatusCode.ServiceUnavailable, "dependency_unavailable");
      Assert.False(response.Headers.Contains("Set-Cookie"));
      using var get = new HttpRequestMessage(HttpMethod.Get, SessionPath);
      AddCookies(get, session);
      Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(get)).StatusCode);
    }
    else
    {
      try
      {
        await checkpoint.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var handleHash = await peer.Database.SqlQuery<byte[]>($"""
            SELECT handle_hash AS "Value" FROM minidocs.identity_sessions WHERE user_id = {user}
            """).SingleAsync();
        var authority = (await PostgresSessionTests.ReadAuthorityAsync(peer, user)) with { HandleHash = handleHash };
        await new SessionStore(peer).RevokeAsync(authority, default);
      }
      finally { checkpoint.Release.TrySetResult(); }
      Assert.Equal(HttpStatusCode.NoContent, (await logout).StatusCode);
      await AssertProblem(await LogoutAsync(client, session, authenticated.Cookie, authenticated.Token),
          HttpStatusCode.Unauthorized, "authentication_required");
    }
  }

  private async Task<MiniDocsDbContext> CreateDatabaseAsync()
  {
    var database = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(database);
    await using var migrator = fixture.CreateMigratorPeer(database);
    await migrator.Database.MigrateAsync();
    return database;
  }

  private static async Task<Guid> CreateUserAsync(MiniDocsDbContext db, string name, string password, bool admin = false,
      int iterations = 100_000)
  {
    var id = Guid.NewGuid();
    if (admin)
    {
      await PostgresSeed.InsertUserAsync(db, id, name, "admin_only");
      var role = Guid.NewGuid();
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO minidocs.identity_roles (id, name, normalized_name) VALUES ({role}, 'Administrator', 'ADMINISTRATOR');
          INSERT INTO minidocs.identity_user_roles VALUES ({id}, {role})
          """);
    }
    else await PostgresSeed.InsertUserAndWorkspace(db, id, Guid.NewGuid(), name);
    await PostgresSessionTests.WriteStampsAsync(db, id);
    var hasher = new PasswordHasher<MiniDocsUser>(Options.Create(new PasswordHasherOptions { IterationCount = iterations }));
    var hash = hasher.HashPassword(new MiniDocsUser { Id = id }, password);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.identity_users SET password_hash = {hash} WHERE id = {id}
        """);
    return id;
  }

  private static WebApplicationFactory<Program> CreateHost(MiniDocsDbContext runtime, SessionKeyRingFixture keys,
      IInterceptor? interceptor = null, string? active = null, string? previous = null, bool allowCreation = true,
      string? directory = null) =>
      new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
      {
        builder.UseEnvironment("Production");
        keys.Configure(builder, active, previous, allowCreation, directory);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
          ["ConnectionStrings:MiniDocs"] = runtime.Database.GetConnectionString()
        }));
        if (interceptor is not null)
          builder.ConfigureServices(services => services.AddDbContext<MiniDocsDbContext>(
              options => options.AddInterceptors(interceptor)));
      });

  private static HttpClient TestClient(WebApplicationFactory<Program> host) => host.CreateClient(new()
  {
    BaseAddress = new Uri("https://localhost"),
    AllowAutoRedirect = false,
    HandleCookies = false
  });

  private static async Task<(string Cookie, string Token)> GetAntiforgeryAsync(HttpClient client, string? session = null)
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, AntiforgeryPath);
    if (session is not null) AddCookies(request, session);
    var response = await client.SendAsync(request);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(response.Headers.CacheControl?.NoStore);
    var data = await response.Content.ReadFromJsonAsync<JsonElement>();
    return (Cookie(response, CsrfName), data.GetProperty("requestToken").GetString()!);
  }

  private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string name, string password, string cookie,
      string? token, string? previousSession = null)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, SessionPath + "/login")
    {
      Content = JsonContent.Create(new { userName = name, password })
    };
    AddCookies(request, previousSession is null ? [cookie] : [cookie, previousSession]);
    if (token is not null) request.Headers.Add(Header, token);
    return await client.SendAsync(request);
  }

  private static async Task<HttpResponseMessage> LogoutAsync(HttpClient client, string session, string csrf, string token)
  {
    using var request = new HttpRequestMessage(HttpMethod.Delete, SessionPath);
    request.Headers.Add(Header, token);
    AddCookies(request, session, csrf);
    return await client.SendAsync(request);
  }

  private static void AddCookies(HttpRequestMessage request, params string[] cookies) =>
      request.Headers.Add("Cookie", string.Join("; ", cookies.Select(cookie => cookie.Split(';')[0])));

  private static async Task<HttpResponseMessage> GetSessionAsync(HttpClient client, string session)
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, SessionPath);
    AddCookies(request, session);
    return await client.SendAsync(request);
  }

  private static string Cookie(HttpResponseMessage response, string name) =>
      Assert.Single(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal));

  private static void AssertCookie(string cookie, string name, string sameSite)
  {
    Assert.StartsWith(name + "=", cookie, StringComparison.Ordinal);
    Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("samesite=" + sameSite, cookie, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
  }

  private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
  {
    Assert.Equal(status, response.StatusCode);
    Assert.True(response.Headers.CacheControl?.NoStore);
    Assert.Null(response.Headers.Location);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal(code, json.GetProperty("code").GetString());
    Assert.NotEmpty(json.GetProperty("traceId").GetString()!);
  }

  private sealed class LoginCheckpoint
  {
    internal TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal async Task PauseAsync(CancellationToken cancellationToken)
    {
      Reached.TrySetResult();
      await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }
  }

  private sealed class RecordingDiagnostics : ILoggerProvider, ILogger
  {
    private readonly ActivityListener listener;
    internal ConcurrentQueue<string> Output { get; } = new();

    internal RecordingDiagnostics()
    {
      listener = new ActivityListener
      {
        ShouldListenTo = source => source.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || source.Name.StartsWith("Npgsql", StringComparison.Ordinal),
        Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStopped = activity =>
        {
          Output.Enqueue(activity.DisplayName);
          foreach (var tag in activity.TagObjects) Output.Enqueue($"{tag.Key}={tag.Value}");
        }
      };
      ActivitySource.AddActivityListener(listener);
    }

    public ILogger CreateLogger(string categoryName) => this;
    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
      Output.Enqueue(formatter(state, exception));
      if (exception is not null) Output.Enqueue(exception.ToString());
    }

    public void Dispose() => listener.Dispose();
  }

  private sealed class PauseBeforeCreate(LoginCheckpoint checkpoint) : DbCommandInterceptor
  {
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
      if (command.CommandText.Contains("minidocs.command_create_session(", StringComparison.Ordinal))
        await checkpoint.PauseAsync(cancellationToken);
      return result;
    }
  }

  private sealed class PauseOrFailBeforeRevoke(LoginCheckpoint? checkpoint) : DbCommandInterceptor
  {
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
      if (!command.CommandText.Contains("minidocs.command_revoke_session(", StringComparison.Ordinal)) return result;
      if (checkpoint is null) throw new TimeoutException();
      await checkpoint.PauseAsync(cancellationToken);
      return result;
    }
  }

  private sealed class PauseAfterCommit(LoginCheckpoint checkpoint) : DbTransactionInterceptor
  {
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default) => checkpoint.PauseAsync(cancellationToken);
  }

  private sealed class ObserveIdentityUpdate : DbCommandInterceptor
  {
    internal TaskCompletionSource<int> Pid { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
      if (command.CommandText.Contains("UPDATE minidocs.identity_users", StringComparison.Ordinal))
        Pid.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
      return ValueTask.FromResult(result);
    }
  }

  private sealed class LoseCommitAcknowledgment : DbTransactionInterceptor
  {
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default) => throw new TimeoutException();
  }
}
