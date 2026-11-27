using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.ViaDocs;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  private const string ProvisioningPath = "/api/v1/admin/provisioning";

  [Fact]
  [Trait("Package", "D4-C")]
  public async Task ProvisioningHttpOnlyConfirmsExistingWorkAndRedactsSensitiveState()
  {
    await using var db = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(db);
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(db, "admin", password, admin: true);
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "target");
    using var keys = new SessionKeyRingFixture();
    using var recorder = new RecordingDiagnostics();
    var lose = new LoseProvisioningCommit();
    var clock = new RecoveryWindowClock();
    using var host = CreateHost(runtime, keys, lose).WithWebHostBuilder(builder =>
    {
      builder.ConfigureLogging(logging => logging.AddProvider(recorder));
      builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ViaDocs:Enabled"] = "true",
        ["ViaDocs:InvitationsEnabled"] = "true",
        ["ViaDocs:BaseAddress"] = "https://example.invalid/",
        ["Provisioning:Enabled"] = "true"
      }));
      builder.ConfigureServices(services =>
      {
        services.AddSingleton<TimeProvider>(clock);
        var worker = services.Single(item => item.ServiceType == typeof(IHostedService) &&
            item.ImplementationType == typeof(ProvisioningWorker));
        services.Remove(worker);
      });
    });
    using var client = TestClient(host);
    var availability = host.Services.GetRequiredService<ProvisioningAvailability>();
    var status = $"{ProvisioningPath}?workspaceId={workspace:D}";
    var invitation = $"/api/v1/admin/workspaces/{workspace:D}/technical-invitation";
    var recovery = $"/api/v1/admin/workspaces/{workspace:D}/recovery";
    var token = new string('c', 64);
    await AssertProblem(await client.GetAsync(status), HttpStatusCode.Unauthorized, "authentication_required");
    var start = await GetAntiforgeryAsync(client);
    var session = Cookie(await LoginAsync(client, "admin", password, start.Cookie, start.Token), SessionName);
    var csrf = await GetAntiforgeryAsync(client, session);

    async Task<HttpResponseMessage> Post(string path, object body) =>
        await RequestAsync(client, HttpMethod.Post, path, session, csrf.Cookie, csrf.Token, body);

    async Task AssertAccepted(HttpResponseMessage response, string expected)
    {
      Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
      Assert.True(response.Headers.CacheControl?.NoStore);
      Assert.Equal(status, response.Headers.Location?.OriginalString);
      Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
      AssertStatus(await response.Content.ReadFromJsonAsync<JsonElement>(), expected);
    }

    void AssertStatus(JsonElement item, string expected)
    {
      Assert.Equal(workspace, item.GetProperty("workspaceId").GetGuid());
      Assert.Equal(expected, item.GetProperty("state").GetString());
      Assert.Equal(new[] { "state", "updatedAt", "workspaceId" }, item.EnumerateObject().Select(p => p.Name).Order());
    }

    var pending = await RequestAsync(client, HttpMethod.Get, status, session);
    Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
    Assert.True(pending.Headers.CacheControl?.NoStore);
    AssertStatus((await pending.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")[0], "pending_invitation");
    await AssertProblem(await RequestAsync(client, HttpMethod.Post, invitation, session, csrf.Cookie, null, new { token }),
        HttpStatusCode.Forbidden, "antiforgery_invalid");
    await AssertProblem(await Post(invitation, new { token }), HttpStatusCode.ServiceUnavailable, "dependency_unavailable");
    availability.Inventoried = true;
    await AssertProblem(await Post(invitation, new { token = token + "X" }), HttpStatusCode.BadRequest, "validation_failed");
    await AssertProblem(await Post(recovery, new { action = 1 }), HttpStatusCode.BadRequest, "validation_failed");
    await AssertAccepted(await Post(invitation, new { token }), "ready");
    var store = new ProvisioningReadStore(db);
    var ready = (await store.ReadAsync(workspace, default))!;
    for (var index = 0; index < 2; index++)
      await AssertAccepted(await Post(recovery, new { action = "retry_if_safe" }), "ready");
    Assert.Equal(ready.Version, (await store.ReadAsync(workspace, default))!.Version);
    // The existing two accepted requests count toward the five-per-actor budget.
    for (var index = 0; index < 3; index++)
      await AssertAccepted(await Post(recovery, new { action = "retry_if_safe" }), "ready");
    var limited = await Post(recovery, new { action = "retry_if_safe" });
    Assert.True(limited.Headers.CacheControl?.NoStore);
    Assert.Equal(TimeSpan.FromSeconds(60), limited.Headers.RetryAfter?.Delta);
    await AssertProblem(limited, HttpStatusCode.TooManyRequests, "rate_limited");
    Assert.Equal(ready.Version, (await store.ReadAsync(workspace, default))!.Version);
    clock.NextWindow();

    var operation = ready.OperationId!.Value;
    var begun = await LocalWriteTransaction.ExecuteAsync(runtime, ct => ProvisioningPersistence.TryBeginProvisioningAsync(
        runtime, workspace, operation, ready.Version, Guid.NewGuid(), TimeSpan.FromSeconds(120), ct), value => value is not null);
    Assert.Equal(LocalWriteStatus.Committed, begun.Status);
    await AssertProblem(await Post(recovery, new { action = "verify" }), HttpStatusCode.Conflict, "operation_uncertain");
    await AssertProblem(await Post(recovery, new { action = "retry_if_safe" }), HttpStatusCode.Conflict, "operation_uncertain");
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.provisioning_operations SET lease_until = clock_timestamp() - interval '1 second' WHERE id = {operation}
        """);
    var expired = await LocalWriteTransaction.ExecuteAsync(runtime, ct => ProvisioningPersistence.TryRecoverExpiredProvisioningAsync(
        runtime, workspace, operation, begun.Value!.Value.Version, ct), value => value);
    Assert.Equal(LocalWriteStatus.Committed, expired.Status);
    var recovering = (await store.ReadAsync(workspace, default))!;
    await AssertAccepted(await Post(recovery, new { action = "verify" }), "recovery_required");
    await AssertAccepted(await Post(recovery, new { action = "verify" }), "recovery_required");
    lose.Next = true;
    await AssertProblem(await Post(recovery, new { action = "verify" }), HttpStatusCode.ServiceUnavailable, "dependency_unavailable");
    Assert.Equal(recovering.Version, (await store.ReadAsync(workspace, default))!.Version);
    Assert.Equal(1, await db.ProvisioningOperations.CountAsync());
    var protectedSecret = ready.Secret!;
    var secret = host.Services.GetRequiredService<ProvisioningProtection>()
        .ReadCredential(workspace, ready.AccountId!.Value, ready.Generation, protectedSecret);
    foreach (var diagnostic in recorder.Output)
    {
      Assert.DoesNotContain(token, diagnostic);
      Assert.DoesNotContain(secret, diagnostic);
      Assert.DoesNotContain(Convert.ToBase64String(protectedSecret), diagnostic);
    }
  }

  private sealed class RecoveryWindowClock : TimeProvider
  {
    private long timestamp;
    public override long TimestampFrequency => 1;
    public override long GetTimestamp() => Interlocked.Read(ref timestamp);
    internal void NextWindow() => Interlocked.Add(ref timestamp, 60);
  }

  private sealed class LoseProvisioningCommit : DbTransactionInterceptor
  {
    internal bool Next { get; set; }

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
      if (!Next) return Task.CompletedTask;
      Next = false;
      throw new TimeoutException();
    }
  }
}
