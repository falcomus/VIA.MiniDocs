using System.Data.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VIA.MiniDocs.Application;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;
using VIA.MiniDocs.Infrastructure.ViaDocs;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Package", "D4-C")]
[Trait("Category", "PostgreSQL")]
public sealed partial class TechnicalProvisioningTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  private static readonly TimeSpan Lease = TimeSpan.FromSeconds(120);
  private static readonly string Token = new('a', 64);

  [Fact]
  public async Task CommitBeforeSingleRedeemAndFreshFindBeforeComplete()
  {
    await using var rig = await CreateRigAsync();
    await rig.PrepareAsync();
    var application = Guid.NewGuid();
    rig.Auth.OnRedeem = async (identity, invitation, _) =>
    {
      Assert.Equal(Token, invitation.Token);
      var persisted = await rig.ObserveAsync();
      Assert.Equal("registering", persisted.Status);
      Assert.NotNull(persisted.LeaseOwner);
      Assert.Null(persisted.Candidate);
      return new ViaDocsResult<ViaDocsRegistrationCandidate>.Success(new(application, identity.Name));
    };
    rig.Auth.OnFind = async (identity, _) =>
    {
      var persisted = await rig.ObserveAsync();
      Assert.Equal(application, persisted.Candidate);
      Assert.Equal("registering", persisted.Status);
      return new ViaDocsResult<ViaDocsVerifiedIdentity>.Success(new(application, identity.Name));
    };
    await rig.Worker().RunIterationAsync(default);
    var complete = await rig.ObserveAsync();
    Assert.Equal("active", complete.Status);
    Assert.Equal("active", complete.WorkspaceStatus);
    Assert.Equal(application, complete.ApplicationId);
    Assert.NotNull(complete.VerifiedAt);
    Assert.Null(complete.Invitation);
    Assert.Equal(1L, complete.Generation);
    await rig.Worker().RunIterationAsync(default);
    Assert.Equal(1, rig.Auth.Redeems);
    Assert.Equal(1, rig.Auth.Finds);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  [InlineData(3)]
  public async Task LostCommitAcknowledgmentsNeverAuthorizeAnotherPost(int stage)
  {
    var interceptor = new LostCommit();
    await using var rig = await CreateRigAsync(interceptor);
    await rig.PrepareAsync();
    var application = Guid.NewGuid();
    rig.Auth.SetIdentity(application);
    interceptor.Remaining = stage;
    await rig.Worker().RunIterationAsync(default);
    var after = await rig.ObserveAsync();
    Assert.Equal(stage == 3 ? "active" : "registering", after.Status);
    Assert.Equal(stage == 1 ? 0 : 1, rig.Auth.Redeems);
    if (stage < 3)
    {
      await rig.ExpireLeaseAsync();
      await rig.Worker().RunIterationAsync(default);
      await rig.Worker().RunIterationAsync(default);
      Assert.Equal("active", (await rig.ObserveAsync()).Status);
    }
    Assert.Equal(stage == 1 ? 0 : 1, rig.Auth.Redeems);
    Assert.Equal(1, rig.Auth.Finds);
  }

  [Fact]
  public async Task DisableAndReenableDuringPausedFindNeverRevivesTheOldFence()
  {
    using var ring = new TestCredentialRing();
    await using var rig = await CreateRigAsync(transport: null, keyRing: ring.Open());
    await rig.PrepareAsync();
    var application = Guid.NewGuid();
    rig.Auth.SetIdentity(application);
    rig.Auth.OnRedeem = (_, _, _) => Task.FromResult<ViaDocsResult<ViaDocsRegistrationCandidate>>(
        new ViaDocsResult<ViaDocsRegistrationCandidate>.Failure(new(ViaDocsFailureKind.UnknownOutcome)));
    await rig.Worker().RunIterationAsync(default);
    await rig.ExpireLeaseAsync();
    await rig.Worker().RunIterationAsync(default);
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    rig.Auth.OnFind = async (identity, ct) =>
    {
      entered.TrySetResult();
      await release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
      return new ViaDocsResult<ViaDocsVerifiedIdentity>.Success(new(application, identity.Name));
    };
    var oldWorker = rig.Worker().RunIterationAsync(default);
    try
    {
      await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
      var old = await rig.ObserveAsync();
      var admin = new AdministratorAccounts(rig.Runtime);
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(admin, rig.Actor, rig.Owner, true));
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(admin, rig.Actor, rig.Owner, false));
      var invalidated = await rig.ObserveAsync();
      Assert.True(invalidated.Version > old.Version);
      Assert.Equal(old.LeaseUntil, invalidated.LeaseUntil);
      Assert.False(invalidated.Owns(new OperationFence(old.LeaseOwner!.Value, old.Version), 0));
    }
    finally { release.TrySetResult(); }
    await oldWorker;
    Assert.Equal("recovery_required", (await rig.ObserveAsync()).Status);
    Assert.Null((await rig.ObserveAsync()).ApplicationId);
    var (restartedHost, restartedAuth) = CreateServices(rig.Runtime, [], keyRing: ring.Open());
    await using var restarted = restartedHost;
    restartedAuth.SetIdentity(application);
    var resumed = ActivatorUtilities.CreateInstance<ProvisioningWorker>(restarted);
    await resumed.RunIterationAsync(default);
    Assert.Equal(1, rig.Auth.Finds);
    Assert.Equal(0, restartedAuth.Finds);
    await rig.ExpireLeaseAsync();
    await resumed.RunIterationAsync(default);
    Assert.Equal("active", (await rig.ObserveAsync()).Status);
    Assert.Equal(1, restartedAuth.Finds);
    Assert.Equal(0, restartedAuth.Redeems);
    Assert.Equal(1, rig.Auth.Redeems);
  }

  [Fact]
  public async Task RecoveryFromSuspendedPreservesSuspensionAndRejectsConflictingIdentity()
  {
    await using var rig = await CreateRigAsync();
    await rig.PrepareAsync();
    var application = Guid.NewGuid();
    rig.Auth.SetIdentity(application);
    rig.Auth.OnFind = (identity, _) => Task.FromResult<ViaDocsResult<ViaDocsVerifiedIdentity>>(
        new ViaDocsResult<ViaDocsVerifiedIdentity>.Success(new(Guid.NewGuid(), identity.Name)));
    await rig.Worker().RunIterationAsync(default);
    Assert.Equal("registering", (await rig.ObserveAsync()).Status);
    await rig.Database.Database.ExecuteSqlInterpolatedAsync(
        $"UPDATE minidocs.workspaces SET status = 'suspended', version = version + 1 WHERE id = {rig.Workspace}");
    var suspended = await rig.ObserveAsync();
    var write = await LocalWriteTransaction.ExecuteAsync(rig.Runtime, ct =>
        ProvisioningPersistence.TryCompleteVerifiedAsync(rig.Runtime, rig.Workspace, suspended.OperationId!.Value,
            new OperationFence(suspended.LeaseOwner!.Value, suspended.Version), application, ct), value => value);
    Assert.Equal(LocalWriteStatus.Rejected, write.Status);
    await rig.ExpireLeaseAsync();
    await rig.Worker().RunIterationAsync(default);
    rig.Auth.SetIdentity(application);
    await rig.Worker().RunIterationAsync(default);
    var completed = await rig.ObserveAsync();
    Assert.Equal("active", completed.Status);
    Assert.Equal("suspended", completed.WorkspaceStatus);
    Assert.Equal(application, completed.ApplicationId);
    Assert.Equal(1, rig.Auth.Redeems);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task InvitationExchangeAndBeginSerializeOnWorkspaceWithoutChangingCredentials(bool exchangeFirst)
  {
    await using var rig = await CreateRigAsync();
    await rig.PrepareAsync();
    var ready = await rig.ObserveAsync();
    var operation = ready.OperationId!.Value;
    var owner = Guid.NewGuid();
    var replacement = rig.Services.GetRequiredService<ProvisioningProtection>()
        .ProtectInvitation(rig.Workspace, operation, new string('b', 64));
    await using var peer = fixture.CreateRuntimePeer(rig.Database);
    await using var first = await rig.Runtime.Database.BeginTransactionAsync();
    await using var second = await peer.Database.BeginTransactionAsync();
    Task<bool> Exchange(MiniDocsDbContext db) => ProvisioningPersistence.TryReplaceInvitationAsync(db,
        rig.Workspace, operation, ready.Version, replacement, default);
    async Task<bool> Begin(MiniDocsDbContext db) => await ProvisioningPersistence.TryBeginProvisioningAsync(db,
        rig.Workspace, operation, ready.Version, owner, Lease, default) is not null;
    Assert.True(await (exchangeFirst ? Exchange(rig.Runtime) : Begin(rig.Runtime)));
    var blocked = exchangeFirst ? Begin(peer) : Exchange(peer);
    await PostgresSessionTests.WaitForBlockerAsync(rig.Database, Pid(peer), Pid(rig.Runtime), blocked);
    await first.CommitAsync();
    Assert.False(await blocked);
    await second.CommitAsync();
    var after = await rig.ObserveAsync();
    Assert.Equal(ready.Version + 1, after.Version);
    Assert.Equal(exchangeFirst ? "ready" : "registering", after.Status);
    Assert.Equal(exchangeFirst ? replacement : ready.Invitation, after.Invitation);
    Assert.Equal((ready.AccountId, ready.OperationId, ready.Name, ready.Generation),
        (after.AccountId, after.OperationId, after.Name, after.Generation));
    Assert.Equal(ready.Secret, after.Secret);
  }

  [Fact]
  public async Task ParallelWorkersAndProviderScopesRespectCurrentAuthority()
  {
    await using var rig = await CreateRigAsync();
    await rig.PrepareAsync();
    var ready = await rig.ObserveAsync();
    Assert.Null(await rig.ResolveAsync(ready.Identity()));
    var application = Guid.NewGuid();
    rig.Auth.SetIdentity(application);
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    rig.Auth.OnRedeem = async (identity, _, ct) =>
    {
      entered.TrySetResult();
      await release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
      return new ViaDocsResult<ViaDocsRegistrationCandidate>.Failure(new(ViaDocsFailureKind.UnknownOutcome));
    };
    var worker = rig.Worker().RunIterationAsync(default);
    try
    {
      await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
      await rig.Worker().RunIterationAsync(default);
      var current = await rig.ObserveAsync();
      var identity = current.Identity();
      var attempt = new ProvisioningDispatch(rig.Workspace, current.OperationId!.Value, identity.Credential.Value,
          current.Generation, new(current.LeaseOwner!.Value, current.Version), true, 0,
          TimeProvider.System.GetTimestamp(), (int)Lease.TotalSeconds);
      Assert.Equal(1L, (await rig.ResolveAsync(identity, attempt: attempt))!.Generation);
      Assert.Null(await rig.ResolveAsync(new(Guid.NewGuid(), identity.Name, identity.Credential), attempt: attempt));
      Assert.Null(await rig.ResolveAsync(new(identity.WorkspaceId, identity.Name, new(Guid.NewGuid())), attempt: attempt));
      Assert.Null(await rig.ResolveAsync(new(identity.WorkspaceId, identity.Name + "x", identity.Credential), attempt: attempt));
      var admin = new AdministratorAccounts(rig.Runtime);
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(admin, rig.Actor, rig.Owner, true));
      Assert.Null(await rig.ResolveAsync(identity, attempt: attempt));
      await rig.ExpireLeaseAsync();
      await rig.Worker().RunIterationAsync(default);
      var expired = await rig.ObserveAsync();
      Assert.Equal("recovery_required", expired.Status);
      var claim = await LocalWriteTransaction.ExecuteAsync(rig.Runtime, ct =>
          ProvisioningPersistence.TryClaimVerificationAsync(rig.Runtime, rig.Workspace, expired.OperationId!.Value,
              expired.Version, Guid.NewGuid(), Lease, ct), value => value is not null);
      Assert.Equal(LocalWriteStatus.Rejected, claim.Status);
      Assert.Null(await rig.ResolveAsync(identity, attempt: attempt));
    }
    finally { release.TrySetResult(); }
    await worker;
    Assert.Equal(1, rig.Auth.Redeems);
    Assert.Equal(0, rig.Auth.Finds);
    Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
        new AdministratorAccounts(rig.Runtime), rig.Actor, rig.Owner, false));
    await rig.Worker().RunIterationAsync(default);
    var active = await rig.ObserveAsync();
    Assert.Equal("active", active.Status);
    Assert.Equal(1L, (await rig.ResolveAsync(active.Identity(), active.ApplicationId))!.Generation);
    Assert.Equal(1, rig.Auth.Redeems);
    Assert.Equal(1, rig.Auth.Finds);
  }

  private static int Pid(MiniDocsDbContext db) => ((NpgsqlConnection)db.Database.GetDbConnection()).ProcessID;

  [Theory]
  [InlineData(false, false)] [InlineData(false, true)]
  [InlineData(true, false)] [InlineData(true, true)]
  public async Task ExhaustedBudgetOrLeasePreventsCandidateAndCompletion(bool completion, bool lease)
  {
    var timing = new BudgetClock();
    await using var rig = await CreateRigAsync(timing);
    await rig.PrepareAsync();
    var application = Guid.NewGuid();
    async Task Exhaust()
    {
      if (lease)
        await rig.Database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE minidocs.provisioning_operations SET lease_until = clock_timestamp() + interval '10 seconds'
            WHERE workspace_id = {rig.Workspace}
            """);
      else timing.Advance(TimeSpan.FromSeconds(60));
    }
    rig.Auth.OnRedeem = async (identity, _, _) =>
    {
      if (!completion) await Exhaust();
      return new ViaDocsResult<ViaDocsRegistrationCandidate>.Success(new(application, identity.Name));
    };
    rig.Auth.OnFind = async (identity, _) =>
    {
      if (completion) await Exhaust();
      return new ViaDocsResult<ViaDocsVerifiedIdentity>.Success(new(application, identity.Name));
    };
    await rig.Worker().RunIterationAsync(default);
    var current = await rig.ObserveAsync();
    Assert.Equal("registering", current.Status);
    Assert.Equal(completion ? application : null, current.Candidate);
    Assert.Null(current.ApplicationId);
    Assert.Equal(1, rig.Auth.Redeems);
    Assert.Equal(completion ? 1 : 0, rig.Auth.Finds);
  }

  [Fact]
  public async Task ShutdownAfterPossibleDispatchDoesNotPersistCandidateOrComplete()
  {
    await using var rig = await CreateRigAsync();
    await rig.PrepareAsync();
    using var shutdown = new CancellationTokenSource();
    rig.Auth.OnRedeem = (identity, _, _) =>
    {
      shutdown.Cancel();
      return Task.FromResult<ViaDocsResult<ViaDocsRegistrationCandidate>>(
          new ViaDocsResult<ViaDocsRegistrationCandidate>.Success(new(Guid.NewGuid(), identity.Name)));
    };
    try { await rig.Worker().RunIterationAsync(shutdown.Token); }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    var row = await rig.ObserveAsync();
    Assert.Equal("registering", row.Status);
    Assert.Null(row.Candidate);
    Assert.Null(row.ApplicationId);
    Assert.Equal(1, rig.Auth.Redeems);
    Assert.Equal(0, rig.Auth.Finds);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task OldWorkerCannotDispatchAfterReenableBeforeCredentialResolution(bool recovery)
  {
    var checkpoint = new CredentialCheckpoint();
    var calls = 0;
    using var transport = new AuthHandler((_, _) =>
    {
      Interlocked.Increment(ref calls);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    });
    await using var rig = await CreateRigAsync(transport, null, checkpoint);
    await rig.PrepareAsync();
    if (recovery)
    {
      await rig.Worker().RunIterationAsync(default);
      Assert.Equal(1, calls);
      await rig.ExpireLeaseAsync();
      await rig.Worker().RunIterationAsync(default);
    }
    checkpoint.Enabled = true;
    var worker = rig.Worker().RunIterationAsync(default);
    try
    {
      await checkpoint.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
      var before = await rig.ObserveAsync();
      var admin = new AdministratorAccounts(rig.Runtime);
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(admin, rig.Actor, rig.Owner, true));
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(admin, rig.Actor, rig.Owner, false));
      Assert.True((await rig.ObserveAsync()).Version > before.Version);
      var stale = await LocalWriteTransaction.ExecuteAsync(rig.Runtime, ct => ProvisioningPersistence.TryCompleteVerifiedAsync(
          rig.Runtime, rig.Workspace, before.OperationId!.Value, new(before.LeaseOwner!.Value, before.Version), Guid.NewGuid(), ct), x => x);
      Assert.Equal(LocalWriteStatus.Rejected, stale.Status);
    }
    finally { checkpoint.Release.TrySetResult(); }
    await worker;
    Assert.Equal(recovery ? 1 : 0, calls);
    Assert.NotEqual("active", (await rig.ObserveAsync()).Status);
  }

  [Fact]
  public async Task TwoAdministratorsPreparingTheSameWorkspaceCannotReplaceTheWinner()
  {
    var gate = new AdminWriteGate();
    await using var rig = await CreateRigAsync(gate);
    var secondId = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(rig.Database, secondId, "other-admin", "admin_only");
    await PostgresSessionTests.WriteStampsAsync(rig.Database, secondId);
    await rig.Database.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.identity_user_roles(user_id,role_id)
        SELECT {secondId},id FROM minidocs.identity_roles WHERE normalized_name = 'ADMINISTRATOR'
        """);
    var second = await PostgresSessionTests.CreateSessionAsync(rig.Runtime, secondId);
    gate.Enabled = true;
    gate.PauseCommit = true;
    var first = rig.SupplyAsync(rig.Actor, rig.Workspace, Token);
    try
    {
      var holder = await gate.CommitPid.Task.WaitAsync(TimeSpan.FromSeconds(5));
      var waiting = rig.SupplyAsync(second, rig.Workspace, new string('b', 64));
      var waiter = await gate.SecondGuardPid.Task.WaitAsync(TimeSpan.FromSeconds(5));
      await PostgresSessionTests.WaitForBlockerAsync(rig.Database, waiter, holder, waiting);
      gate.Release.TrySetResult();
      Assert.Equal(ProvisioningDecision.Accepted, (await first).Decision);
      Assert.Equal(ProvisioningDecision.Conflict, (await waiting).Decision);
    }
    finally { gate.Release.TrySetResult(); }
    var persisted = await rig.ObserveAsync();
    Assert.Equal(1L, persisted.Version);
    Assert.Equal(Token, rig.Services.GetRequiredService<ProvisioningProtection>().ReadInvitation(
        rig.Workspace, persisted.OperationId!.Value, persisted.Invitation!));
    Assert.Equal(1, await rig.Database.ViaDocsAccounts.CountAsync());
  }

  [Fact]
  public async Task RealD3ClientUsesFreshCredentialScopesAndIsolatesBoundTokensForTwoWorkspaces()
  {
    var applications = new ConcurrentDictionary<string, (Guid Id, string Secret)>();
    var calls = new ConcurrentQueue<(string Path, string Name)>();
    using var transport = new AuthHandler(async (request, ct) =>
    {
      Assert.Equal("example.invalid", request.RequestUri!.Host);
      Assert.Equal(HttpMethod.Post, request.Method);
      var payload = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
      var name = payload.GetProperty("name").GetString()!;
      var secret = payload.GetProperty("secret").GetString()!;
      var path = request.RequestUri.AbsolutePath;
      calls.Enqueue((path, name));
      if (path.EndsWith("/register-invitation", StringComparison.Ordinal))
      {
        Assert.Equal(Token, payload.GetProperty("token").GetString());
        Assert.True(applications.TryAdd(name, (Guid.NewGuid(), secret)));
      }
      else
      {
        Assert.Equal("/api/v1/applications/login", path);
        Assert.False(payload.TryGetProperty("token", out _));
      }
      var identity = applications[name];
      Assert.Equal(identity.Secret, secret);
      var expiry = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600);
      var jwt = string.Join('.', WebEncoders.Base64UrlEncode("{\"alg\":\"HS256\"}"u8.ToArray()),
          WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { sub = identity.Id, exp = expiry.ToUnixTimeSeconds() })),
          WebEncoders.Base64UrlEncode(new byte[32]));
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = JsonContent.Create(new { applicationId = identity.Id, name, accessToken = jwt, expiresUtc = expiry })
      };
    });
    await using var rig = await CreateRigAsync(transport);
    await rig.PrepareAsync();
    var second = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(rig.Database, Guid.NewGuid(), second, "second-target");
    Assert.Equal(ProvisioningDecision.Accepted, (await rig.SupplyAsync(rig.Actor, second, Token)).Decision);
    await rig.Worker().RunIterationAsync(default);
    var first = await rig.ObserveAsync();
    var other = (await new ProvisioningReadStore(rig.Database).ReadAsync(second, default))!;
    Assert.True(first.Bound && other.Bound);
    Assert.Equal(2, applications.Count);
    Assert.NotEqual(applications[first.Name!].Secret, applications[other.Name!].Secret);
    Assert.Equal(4, calls.Count);

    async Task<Guid> Login(ProvisioningSnapshot row)
    {
      await using var scope = rig.Services.CreateAsyncScope();
      var auth = scope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>();
      return Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Success>(await auth.LoginAsync(
          new(row.Identity(), row.ApplicationId!.Value), default)).Value.ApplicationId;
    }

    for (var repeat = 0; repeat < 2; repeat++)
      Assert.Equal(new[] { first.ApplicationId!.Value, other.ApplicationId!.Value }, await Task.WhenAll(Login(first), Login(other)));
    Assert.Equal(6, calls.Count);
    await using (var scope = rig.Services.CreateAsyncScope())
    {
      var auth = scope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>();
      var wrongApplication = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
          await auth.LoginAsync(new(first.Identity(), other.ApplicationId!.Value), default));
      Assert.Equal(ViaDocsFailureKind.Authentication, wrongApplication.Error.Kind);
      await rig.Database.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.workspaces SET status = 'suspended', version = version + 1 WHERE id = {second}
          """);
      var suspended = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
          await auth.LoginAsync(new(other.Identity(), other.ApplicationId!.Value), default));
      Assert.Equal(ViaDocsFailureKind.Authentication, suspended.Error.Kind);
      Assert.Equal(6, calls.Count);
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
          new AdministratorAccounts(rig.Runtime), rig.Actor, rig.Owner, true));
      var disabled = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
          await auth.LoginAsync(new(first.Identity(), first.ApplicationId!.Value), default));
      Assert.Equal(ViaDocsFailureKind.Authentication, disabled.Error.Kind);
      Assert.Equal(6, calls.Count);
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
          new AdministratorAccounts(rig.Runtime), rig.Actor, rig.Owner, false));
      await rig.Database.Database.ExecuteSqlInterpolatedAsync($"""
          UPDATE minidocs.workspaces SET status = 'active', version = version + 1 WHERE id = {second}
          """);
    }
    await rig.Database.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.via_docs_accounts SET credential_generation = 2 WHERE workspace_id = {first.WorkspaceId}
        """);
    await Assert.ThrowsAsync<ViaDocsCredentialUnavailableException>(() => rig.ResolveAsync(first.Identity(), first.ApplicationId));
    Assert.Equal(other.ApplicationId, await Login(other));
    Assert.Equal(6, calls.Count);
  }

  [Fact]
  public async Task ProductionProviderRejectsUnboundCallsWithoutMatchingDispatch()
  {
    var calls = 0;
    var fault = new DispatchReadFault();
    using var transport = new AuthHandler((_, _) =>
    {
      Interlocked.Increment(ref calls);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    });
    await using var rig = await CreateRigAsync(transport, null, fault);
    await rig.PrepareAsync();
    var ready = await rig.ObserveAsync();
    var leaseOwner = Guid.NewGuid();
    var began = await LocalWriteTransaction.ExecuteAsync(rig.Runtime, ct =>
        ProvisioningPersistence.TryBeginProvisioningAsync(rig.Runtime, rig.Workspace,
            ready.OperationId!.Value, ready.Version, leaseOwner, Lease, ct), fence => fence is not null);
    Assert.Equal(LocalWriteStatus.Committed, began.Status);
    var row = await rig.ObserveAsync();
    await using var scope = rig.Services.CreateAsyncScope();
    var auth = scope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>();
    var context = scope.ServiceProvider.GetRequiredService<ProvisioningDispatchContext>();
    var attempt = new ProvisioningDispatch(row.WorkspaceId, row.OperationId!.Value, row.AccountId!.Value,
        row.Generation, new(leaseOwner, row.Version), true, 0, TimeProvider.System.GetTimestamp(), (int)Lease.TotalSeconds);
    foreach (var invalid in new[] { null, attempt with { Generation = row.Generation + 1 },
        attempt with { Operation = Guid.NewGuid() }, attempt with { Fence = new(leaseOwner, row.Version - 1) },
        attempt with { Redeem = false } })
    {
      context.Current = invalid;
      var denied = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(
          await auth.RedeemAsync(row.Identity(), new(Token), default));
      Assert.Equal(ViaDocsFailureKind.Authentication, denied.Error.Kind);
      Assert.Equal(0, calls);
    }
    context.Current = null;
    var find = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(await auth.FindAsync(row.Identity(), default));
    Assert.Equal(ViaDocsFailureKind.Authentication, find.Error.Kind);
    var bound = Assert.IsType<ViaDocsResult<ViaDocsVerifiedIdentity>.Failure>(
        await auth.LoginAsync(new(row.Identity(), Guid.NewGuid()), default));
    Assert.Equal(ViaDocsFailureKind.Authentication, bound.Error.Kind);
    Assert.Equal(0, calls);

    context.Current = attempt;
    fault.ReadsUntilFailure = 2;
    var unavailable = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(
        await auth.RedeemAsync(row.Identity(), new(Token), default));
    Assert.Equal(ViaDocsFailureKind.CredentialUnavailable, unavailable.Error.Kind);
    Assert.Equal(0, calls);

    using var cancellation = new CancellationTokenSource();
    fault.Cancel = cancellation.Cancel;
    fault.ReadsUntilFailure = 2;
    var cancelled = Assert.IsType<ViaDocsResult<ViaDocsRegistrationCandidate>.Failure>(
        await auth.RedeemAsync(row.Identity(), new(Token), cancellation.Token));
    Assert.Equal(ViaDocsFailureKind.Cancellation, cancelled.Error.Kind);
    Assert.Equal(0, calls);
  }

  [Fact]
  public async Task UpgradeKeepsLegacyCredentialsByteIdenticalAndBlocksTheirDispatchAfterRestore()
  {
    await using var database = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(database);
    await using (var migrator = fixture.CreateMigratorPeer(database))
      await migrator.GetService<IMigrator>().MigrateAsync("20261127090000_SessionSecurity");
    var workspace = Guid.NewGuid();
    var account = Guid.NewGuid();
    var operation = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(database, Guid.NewGuid(), workspace, "legacy");
    byte[] oldSecret = [5, 7, 9];
    byte[] oldInvitation = [2, 4, 6];
    await using (var runtime = fixture.CreateRuntimePeer(database))
    {
      var prepared = await LocalWriteTransaction.ExecuteAsync(runtime, token => ProvisioningPersistence.TryPrepareAsync(
          runtime, workspace, account, operation, $"minidocs-{workspace:N}", oldSecret, oldInvitation, token), value => value);
      Assert.Equal(LocalWriteStatus.Committed, prepared.Status);
    }
    await using (var migrator = fixture.CreateMigratorPeer(database)) await migrator.Database.MigrateAsync();
    await PostgresTestDatabase.VerifyPermissionsAsync(database);
    await using var restored = await fixture.CreateDatabaseAsync();
    await fixture.RestoreCopyAsync(database, restored);
    await restored.Database.ExecuteSqlRawAsync(PostgresRoleAndRestoreTests.ReadRoleScript("RestoreDatabaseConnect.sql"));
    await PostgresTestDatabase.VerifyPermissionsAsync(restored);
    await using var restoredRuntime = fixture.CreateRuntimePeer(restored);
    var (provider, fake) = CreateServices(restoredRuntime, []);
    await using var services = provider;
    var store = new ProvisioningReadStore(restoredRuntime);
    var before = (await store.ReadAsync(workspace, default))!;
    Assert.Equal(oldSecret, before.Secret);
    Assert.Equal(oldInvitation, before.Invitation);
    Assert.Equal(1L, before.Generation);
    await ActivatorUtilities.CreateInstance<ProvisioningWorker>(provider).RunIterationAsync(default);
    var after = (await store.ReadAsync(workspace, default))!;
    Assert.Equal(before.Version, after.Version);
    Assert.Equal("ready", after.Status);
    Assert.Equal(oldSecret, after.Secret);
    Assert.Equal(oldInvitation, after.Invitation);
    Assert.Equal(0, fake.Redeems);
    Assert.Equal(0, fake.Finds);
  }

  [Fact]
  public async Task DisableAndReenableAfterBeginCommitButBeforeFenceReadPreventsDispatch()
  {
    var checkpoint = new PostCommitCheckpoint();
    await using var rig = await CreateRigAsync(checkpoint);
    await rig.PrepareAsync();
    rig.Auth.SetIdentity(Guid.NewGuid());
    checkpoint.Enabled = true;
    var oldWorker = rig.Worker().RunIterationAsync(default);
    try
    {
      await checkpoint.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
      var reserved = await rig.ObserveAsync();
      Assert.Equal("registering", reserved.Status);
      var admin = new AdministratorAccounts(rig.Runtime);
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(admin, rig.Actor, rig.Owner, true));
      Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(admin, rig.Actor, rig.Owner, false));
      Assert.Equal(reserved.Version + 1, (await rig.ObserveAsync()).Version);
    }
    finally { checkpoint.Release.TrySetResult(); }
    await oldWorker;
    Assert.Equal(0, rig.Auth.Redeems);
    await rig.Worker().RunIterationAsync(default);
    Assert.Equal(0, rig.Auth.Finds);
    await rig.ExpireLeaseAsync();
    await rig.Worker().RunIterationAsync(default);
    await rig.Worker().RunIterationAsync(default);
    Assert.Equal("active", (await rig.ObserveAsync()).Status);
    Assert.Equal(0, rig.Auth.Redeems);
    Assert.Equal(1, rig.Auth.Finds);
  }

  [Theory]
  [InlineData(0, false)]
  [InlineData(0, true)]
  [InlineData(1, false)]
  [InlineData(1, true)]
  [InlineData(2, false)]
  [InlineData(2, true)]
  public async Task AdminSessionRevocationSerializesWithPrepareExchangeAndRecovery(int command, bool lossFirst)
  {
    var gate = new AdminWriteGate();
    await using var rig = await CreateRigAsync(gate);
    if (command != 0) await rig.PrepareAsync();
    if (command == 2)
    {
      rig.Auth.OnRedeem = (_, _, _) => Task.FromResult<ViaDocsResult<ViaDocsRegistrationCandidate>>(
          new ViaDocsResult<ViaDocsRegistrationCandidate>.Failure(new(ViaDocsFailureKind.UnknownOutcome)));
      await rig.Worker().RunIterationAsync(default);
      await rig.ExpireLeaseAsync();
      await rig.Worker().RunIterationAsync(default);
      Assert.Equal("recovery_required", (await rig.ObserveAsync()).Status);
    }
    var before = await rig.ObserveAsync();
    await using var mutator = fixture.CreateRuntimePeer(rig.Database);
    await using var tx = await mutator.Database.BeginTransactionAsync();
    var sessions = new SessionStore(mutator);
    await using var scope = rig.Services.CreateAsyncScope();
    var admin = scope.ServiceProvider.GetRequiredService<ProvisioningAdministration>();
    Task<ProvisioningAcceptance> Apply() => command == 2 ? admin.RecoverAsync(rig.Actor, rig.Workspace, ProvisioningAction.Verify, default)
        : admin.SupplyAsync(rig.Actor, rig.Workspace, new string('b', 64), default);
    gate.Enabled = true;
    if (lossFirst)
    {
      await sessions.RevokeAsync(rig.Actor, default);
      var waiting = Apply();
      await PostgresSessionTests.WaitForBlockerAsync(rig.Database, await gate.GuardPid.Task.WaitAsync(TimeSpan.FromSeconds(5)),
          Pid(mutator), waiting);
      await tx.CommitAsync();
      Assert.Equal(ProvisioningDecision.Forbidden, (await waiting).Decision);
    }
    else
    {
      gate.PauseCommit = true;
      var applying = Apply();
      try
      {
        var writer = await gate.CommitPid.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiting = sessions.RevokeAsync(rig.Actor, default);
        await PostgresSessionTests.WaitForBlockerAsync(rig.Database, Pid(mutator), writer, waiting);
        gate.Release.TrySetResult();
        Assert.Equal(ProvisioningDecision.Accepted, (await applying).Decision);
        await waiting;
        await tx.CommitAsync();
      }
      finally { gate.Release.TrySetResult(); }
    }
    var after = await rig.ObserveAsync();
    Assert.Equal(before.Version + (!lossFirst && command != 2 ? 1 : 0), after.Version);
    Assert.Equal(ProvisioningDecision.Forbidden, (await Apply()).Decision);
  }

  [Theory]
  [InlineData(0, false, 0)] [InlineData(0, true, 0)]
  [InlineData(1, false, 0)] [InlineData(1, true, 0)]
  [InlineData(2, false, 0)] [InlineData(2, true, 0)]
  [InlineData(0, false, 1)] [InlineData(0, true, 1)]
  [InlineData(1, false, 1)] [InlineData(1, true, 1)]
  [InlineData(2, false, 1)] [InlineData(2, true, 1)]
  [InlineData(0, false, 2)] [InlineData(0, true, 2)]
  [InlineData(1, false, 2)] [InlineData(1, true, 2)]
  [InlineData(2, false, 2)] [InlineData(2, true, 2)]
  public async Task AdministratorAuthorityLossSerializesWithProvisioning(int command, bool lossFirst, int loss)
  {
    var gate = new AdminWriteGate();
    await using var rig = await CreateRigAsync(gate);
    if (command != 0) await rig.PrepareAsync();
    if (command == 2)
    {
      rig.Auth.OnRedeem = (_, _, _) => Task.FromResult<ViaDocsResult<ViaDocsRegistrationCandidate>>(
          new ViaDocsResult<ViaDocsRegistrationCandidate>.Failure(new(ViaDocsFailureKind.UnknownOutcome)));
      await rig.Worker().RunIterationAsync(default);
      await rig.ExpireLeaseAsync();
      await rig.Worker().RunIterationAsync(default);
    }
    var before = await rig.ObserveAsync();
    var secondId = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(rig.Database, secondId, "second-admin", "admin_only");
    await PostgresSessionTests.WriteStampsAsync(rig.Database, secondId);
    await rig.Database.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.identity_user_roles(user_id,role_id)
        SELECT {secondId},id FROM minidocs.identity_roles WHERE normalized_name='ADMINISTRATOR'
        """);
    await using var mutator = loss == 1 ? fixture.CreatePeer(rig.Database) : fixture.CreateRuntimePeer(rig.Database);
    var second = await PostgresSessionTests.CreateSessionAsync(mutator, secondId);
    await using var tx = await mutator.Database.BeginTransactionAsync();
    await using var scope = rig.Services.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<ProvisioningAdministration>();
    Task<ProvisioningAcceptance> Apply() => command == 2
        ? service.RecoverAsync(rig.Actor, rig.Workspace, ProvisioningAction.Verify, default)
        : service.SupplyAsync(rig.Actor, rig.Workspace, new string('b', 64), default);
    async Task RemoveAuthority()
    {
      if (loss == 0)
        Assert.Equal(DeactivationResult.Applied, await PostgresSessionTests.ChangeAsync(
            new AdministratorAccounts(mutator), second, rig.Actor.UserId, true));
      else if (loss == 1)
      {
        await mutator.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3841, 401)");
        await mutator.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM minidocs.identity_user_roles WHERE user_id = {rig.Actor.UserId}");
      }
      else await PostgresSessionTests.WriteStampsAsync(mutator, rig.Actor.UserId);
    }
    gate.Enabled = true;
    if (lossFirst)
    {
      await RemoveAuthority();
      var waiting = Apply();
      await PostgresSessionTests.WaitForBlockerAsync(rig.Database, await gate.GuardPid.Task.WaitAsync(TimeSpan.FromSeconds(5)), Pid(mutator), waiting);
      await tx.CommitAsync();
      Assert.Equal(ProvisioningDecision.Forbidden, (await waiting).Decision);
    }
    else
    {
      gate.PauseCommit = true;
      var applying = Apply();
      try
      {
        var writer = await gate.CommitPid.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiting = RemoveAuthority();
        await PostgresSessionTests.WaitForBlockerAsync(rig.Database, Pid(mutator), writer, waiting);
        gate.Release.TrySetResult();
        Assert.Equal(ProvisioningDecision.Accepted, (await applying).Decision);
        await waiting;
        await tx.CommitAsync();
      }
      finally { gate.Release.TrySetResult(); }
    }
    var after = await rig.ObserveAsync();
    Assert.Equal(before.Version + (!lossFirst && command != 2 ? 1 : 0), after.Version);
    Assert.Equal(ProvisioningDecision.Forbidden, (await Apply()).Decision);
  }

  private Task<Rig> CreateRigAsync(params IInterceptor[] interceptors) => CreateRigAsync(null, null, interceptors);

  private async Task<Rig> CreateRigAsync(AuthHandler? transport, IDataProtectionProvider? keyRing = null,
      params IInterceptor[] interceptors)
  {
    var db = await fixture.CreateDatabaseAsync();
    await fixture.PrepareRuntimeAsync(db);
    await using (var migrator = fixture.CreateMigratorPeer(db)) await migrator.Database.MigrateAsync();
    var runtime = fixture.CreateRuntimePeer(db);
    var owner = Guid.NewGuid();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, owner, workspace, "target");
    await PostgresSessionTests.WriteStampsAsync(db, owner);
    var admin = Guid.NewGuid();
    await PostgresSeed.InsertUserAsync(db, admin, "admin", "admin_only");
    await PostgresSessionTests.WriteStampsAsync(db, admin);
    var role = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.identity_roles(id,name,normalized_name) VALUES ({role}, 'Administrator', 'ADMINISTRATOR');
        INSERT INTO minidocs.identity_user_roles(user_id,role_id) VALUES ({admin},{role})
        """);
    var actor = await PostgresSessionTests.CreateSessionAsync(runtime, admin);
    var (provider, fake) = CreateServices(runtime, interceptors, transport, keyRing);
    return new Rig(db, runtime, provider, workspace, owner, actor, fake);
  }

  private static (ServiceProvider Provider, FakeAuthentication Fake) CreateServices(MiniDocsDbContext runtime,
      IInterceptor[] interceptors, AuthHandler? transport = null, IDataProtectionProvider? keyRing = null)
  {
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ViaDocs:Enabled"] = "true",
      ["ViaDocs:InvitationsEnabled"] = "true",
      ["ViaDocs:BaseAddress"] = "https://example.invalid/",
      ["Provisioning:Enabled"] = "true"
    }).Build();
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<IDataProtectionProvider>(keyRing ?? new EphemeralDataProtectionProvider());
    var guard = interceptors.OfType<AdminWriteGate>().SingleOrDefault();
    services.AddDbContext<MiniDocsDbContext>(options =>
    {
      options.UseNpgsql(runtime.Database.GetConnectionString(), postgres => postgres.SetPostgresVersion(18, 7))
          .AddInterceptors(interceptors);
      if (guard is not null) options.AddInterceptors(new ObserveAdminGuard(guard));
    });
    services.AddViaDocs(config).AddTechnicalProvisioning(config, workerAllowed: false);
    if (interceptors.OfType<BudgetClock>().SingleOrDefault() is { } budgetClock)
      services.AddSingleton<TimeProvider>(budgetClock);
    var credentialCheckpoint = interceptors.OfType<CredentialCheckpoint>().SingleOrDefault();
    if (credentialCheckpoint is not null)
    {
      services.AddScoped<ProvisioningCredentialProvider>();
      services.AddScoped<IViaDocsCredentialProvider>(p => new PausedCredentials(
          p.GetRequiredService<ProvisioningCredentialProvider>(), credentialCheckpoint));
    }
    var fake = new FakeAuthentication();
    if (transport is null) services.AddSingleton<IViaDocsAuthentication>(fake);
    else services.AddHttpClient(ViaDocsRegistration.AuthClientName).ConfigurePrimaryHttpMessageHandler(() => transport);
    var provider = services.BuildServiceProvider();
    provider.GetRequiredService<ProvisioningAvailability>().Inventoried = true;
    return (provider, fake);
  }

  private sealed record Rig(MiniDocsDbContext Database, MiniDocsDbContext Runtime, ServiceProvider Services,
      Guid Workspace, Guid Owner, SessionAuthority Actor, FakeAuthentication Auth) : IAsyncDisposable
  {
    internal async Task PrepareAsync()
    {
      Assert.Equal(ProvisioningDecision.Accepted, (await SupplyAsync(Actor, Workspace, Token)).Decision);
    }

    internal async Task<ProvisioningAcceptance> SupplyAsync(SessionAuthority actor, Guid workspace, string invitation)
    {
      await using var scope = Services.CreateAsyncScope();
      return await scope.ServiceProvider.GetRequiredService<ProvisioningAdministration>().SupplyAsync(actor, workspace, invitation, default);
    }

    internal async Task<ProvisioningSnapshot> ObserveAsync()
    {
      var result = await new ProvisioningReadStore(Database).ReadAsync(Workspace, default);
      return Assert.IsType<ProvisioningSnapshot>(result);
    }

    internal Task ExpireLeaseAsync() => Database.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.provisioning_operations SET lease_until = clock_timestamp() - interval '1 second'
        WHERE workspace_id = {Workspace}
        """);

    internal ProvisioningWorker Worker() => ActivatorUtilities.CreateInstance<ProvisioningWorker>(Services);

    internal async Task<ViaDocsCredential?> ResolveAsync(ViaDocsPreparedIdentity identity,
        Guid? application = null, ProvisioningDispatch? attempt = null)
    {
      await using var scope = Services.CreateAsyncScope();
      var provider = (IProvisioningDispatchCredentialProvider)scope.ServiceProvider.GetRequiredService<IViaDocsCredentialProvider>();
      return await provider.ResolveAuthorizedAsync(identity, application, attempt, attempt?.Redeem ?? false, default);
    }

    public async ValueTask DisposeAsync()
    {
      await Services.DisposeAsync();
      await Runtime.DisposeAsync();
      await Database.DisposeAsync();
    }
  }

  private sealed class LostCommit : DbTransactionInterceptor
  {
    internal int Remaining { get; set; }
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
      if (Remaining > 0 && --Remaining == 0) throw new TimeoutException();
      return Task.CompletedTask;
    }
  }

  private sealed class CredentialCheckpoint : DbCommandInterceptor
  {
    internal bool Enabled { get; set; }
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
  }

  private sealed class DispatchReadFault : DbCommandInterceptor
  {
    internal int ReadsUntilFailure { get; set; }
    internal Action? Cancel { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
      if (ReadsUntilFailure > 0 && command.CommandText.Contains("AS \"Generation\"", StringComparison.Ordinal) &&
          --ReadsUntilFailure == 0)
      {
        Cancel?.Invoke();
        throw new TimeoutException();
      }
      return ValueTask.FromResult(result);
    }
  }

  private sealed class BudgetClock : DbCommandInterceptor
  {
    private readonly AdjustableTimeProvider clock = new();
    internal void Advance(TimeSpan duration) => clock.Advance(duration);
    public static implicit operator TimeProvider(BudgetClock value) => value.clock;
  }

  private sealed class AdjustableTimeProvider : TimeProvider
  {
    private long offset;
    public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;
    public override long GetTimestamp() => TimeProvider.System.GetTimestamp() + Interlocked.Read(ref offset);
    public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
    internal void Advance(TimeSpan duration) => Interlocked.Add(ref offset, (long)(duration.TotalSeconds * TimestampFrequency));
  }

  private sealed class PausedCredentials(ProvisioningCredentialProvider inner, CredentialCheckpoint checkpoint)
      : IProvisioningDispatchCredentialProvider
  {
    public ValueTask<ViaDocsCredential?> ResolveCurrentAsync(ViaDocsPreparedIdentity identity, CancellationToken token) =>
        inner.ResolveCurrentAsync(identity, token);

    public async ValueTask<ViaDocsCredential?> ResolveAuthorizedAsync(ViaDocsPreparedIdentity identity,
        Guid? application, ProvisioningDispatch? attempt, bool redeem, CancellationToken token)
    {
      if (checkpoint.Enabled)
      {
        checkpoint.Enabled = false;
        checkpoint.Entered.TrySetResult();
        await checkpoint.Release.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
      }
      return await inner.ResolveAuthorizedAsync(identity, application, attempt, redeem, token);
    }

  }

  private sealed class ObserveAdminGuard(AdminWriteGate gate) : DbCommandInterceptor
  {
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
      if (gate.Enabled && command.CommandText.Contains("lock_admin_authority(", StringComparison.Ordinal))
      {
        var pid = ((NpgsqlConnection)command.Connection!).ProcessID;
        if (!gate.GuardPid.TrySetResult(pid)) gate.SecondGuardPid.TrySetResult(pid);
      }
      return ValueTask.FromResult(result);
    }
  }

  private sealed class PostCommitCheckpoint : DbTransactionInterceptor
  {
    internal bool Enabled { get; set; }
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
      if (!Enabled) return;
      Enabled = false;
      Entered.TrySetResult();
      await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }
  }

  private sealed class AdminWriteGate : DbTransactionInterceptor
  {
    internal bool Enabled { get; set; }
    internal bool PauseCommit { get; set; }
    internal TaskCompletionSource<int> GuardPid { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<int> SecondGuardPid { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<int> CommitPid { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
      if (Enabled && PauseCommit)
      {
        CommitPid.TrySetResult(((NpgsqlConnection)transaction.Connection!).ProcessID);
        await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
      }
      return result;
    }
  }

  private sealed class AuthHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        send(request, cancellationToken);
  }

  private sealed class TestCredentialRing : IDisposable
  {
    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("minidocs-provisioning-restart-");
    private readonly X509Certificate2 certificate;

    internal TestCredentialRing()
    {
      using var key = RSA.Create(2048);
      var request = new CertificateRequest("CN=MiniDocs restart test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
      certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
    }

    internal IDataProtectionProvider Open() => DataProtectionProvider.Create(folder,
        config => config.SetApplicationName("MiniDocs.Provisioning.RestartTest").ProtectKeysWithCertificate(certificate));

    public void Dispose()
    {
      certificate.Dispose();
      folder.Delete(recursive: true);
    }
  }

  private sealed class FakeAuthentication : IViaDocsAuthentication
  {
    private int redeems, finds;
    internal int Redeems => redeems;
    internal int Finds => finds;
    internal Func<ViaDocsPreparedIdentity, ViaDocsInvitation, CancellationToken, Task<ViaDocsResult<ViaDocsRegistrationCandidate>>>? OnRedeem { get; set; }
    internal Func<ViaDocsPreparedIdentity, CancellationToken, Task<ViaDocsResult<ViaDocsVerifiedIdentity>>>? OnFind { get; set; }

    internal void SetIdentity(Guid id)
    {
      OnRedeem = (identity, _, _) => Task.FromResult<ViaDocsResult<ViaDocsRegistrationCandidate>>(
          new ViaDocsResult<ViaDocsRegistrationCandidate>.Success(new(id, identity.Name)));
      OnFind = (identity, _) => Task.FromResult<ViaDocsResult<ViaDocsVerifiedIdentity>>(
          new ViaDocsResult<ViaDocsVerifiedIdentity>.Success(new(id, identity.Name)));
    }

    public Task<ViaDocsResult<ViaDocsRegistrationCandidate>> RedeemAsync(ViaDocsPreparedIdentity identity,
        ViaDocsInvitation invitation, CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref redeems);
      return OnRedeem!(identity, invitation, cancellationToken);
    }

    public Task<ViaDocsResult<ViaDocsVerifiedIdentity>> FindAsync(ViaDocsPreparedIdentity identity, CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref finds);
      return OnFind!(identity, cancellationToken);
    }

    public Task<ViaDocsResult<ViaDocsVerifiedIdentity>> LoginAsync(ViaDocsBoundIdentity identity,
        CancellationToken cancellationToken) => throw new InvalidOperationException("Cached login cannot verify provisioning.");
  }
}
