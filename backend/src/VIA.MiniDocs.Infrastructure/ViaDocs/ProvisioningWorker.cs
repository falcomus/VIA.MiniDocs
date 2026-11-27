using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed class ProvisioningOptions
{
  public bool Enabled { get; set; }
  public int PollSeconds { get; set; } = 5;
  public int BatchSize { get; set; } = 8;
  public int ParallelAttempts { get; set; } = 2;
  public int LeaseSeconds { get; set; } = 120;
  public int AttemptSeconds { get; set; } = 75;
  public int ReserveSeconds { get; set; } = 15;
  public int TransactionSeconds { get; set; } = 5;

  internal bool IsValid(ViaDocsOptions client) =>
      PollSeconds is >= 1 and <= 60 && BatchSize is >= 1 and <= 100 &&
      ParallelAttempts is >= 1 and <= 2 && LeaseSeconds is > 0 && LeaseSeconds <= D2LeasePolicy.MaximumSeconds &&
      TransactionSeconds is >= 1 and <= 5 && AttemptSeconds > 0 && ReserveSeconds is >= 15 and <= 120 &&
      LeaseSeconds >= (long)AttemptSeconds + ReserveSeconds &&
      (!Enabled || (client.Enabled &&
          AttemptSeconds >= 2L * client.ShortRequestSeconds + 4L * TransactionSeconds + ReserveSeconds));
}

public sealed class ProvisioningAvailability
{
  private volatile bool inventoried;
  internal bool Inventoried { get => inventoried; set => inventoried = value; }
}

public static class TechnicalProvisioningRegistration
{
  public static IServiceCollection AddTechnicalProvisioning(this IServiceCollection services, IConfiguration configuration,
      bool workerAllowed)
  {
    services.AddSingleton<ProvisioningProtection>();
    services.AddScoped<ProvisioningReadStore>();
    services.AddScoped<ProvisioningDispatchContext>();
    services.AddScoped(provider => new ProvisioningAdministration(
        provider.GetRequiredService<MiniDocsDbContext>(), provider.GetRequiredService<ProvisioningReadStore>(),
        provider.GetRequiredService<ProvisioningProtection>(), provider.GetRequiredService<ProvisioningAvailability>(),
        provider.GetRequiredService<IOptions<ProvisioningOptions>>(), provider.GetRequiredService<IOptions<ViaDocsOptions>>(),
        provider.GetRequiredService<ViaDocsAuthTransport>()));
    services.AddScoped<IViaDocsCredentialProvider, ProvisioningCredentialProvider>();
    services.AddSingleton<ProvisioningAvailability>();
    services.AddOptions<ProvisioningOptions>().Bind(configuration.GetSection("Provisioning"))
        .Validate<IOptions<ViaDocsOptions>>((settings, client) => settings.IsValid(client.Value),
            "Invalid technical provisioning budgets or VIA.Docs configuration.").ValidateOnStart();
    if (workerAllowed) services.AddHostedService<ProvisioningWorker>();
    return services;
  }
}

internal sealed class ProvisioningWorker(IServiceScopeFactory scopes, IOptions<ProvisioningOptions> options,
    IOptions<ViaDocsOptions> client, ViaDocsAuthTransport transport, ProvisioningAvailability availability,
    ProvisioningProtection protection, TimeProvider clock, ILogger<ProvisioningWorker> logger) : BackgroundService
{
  private ProvisioningOptions Settings => options.Value;
  private Guid? after;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!Settings.Enabled) return;
    try
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          if (!availability.Inventoried)
          {
            var legacy = await InventoryAsync(stoppingToken);
            if (legacy > 0) logger.LogWarning("Technical provisioning found {Count} unsupported protection envelopes.", legacy);
            availability.Inventoried = true;
          }
          await RunIterationAsync(stoppingToken);
        }
        catch (Exception error) when (Recoverable(error))
        {
          logger.LogWarning("Technical provisioning could not read the current database state.");
        }
        await Task.Delay(TimeSpan.FromSeconds(Settings.PollSeconds), clock, stoppingToken);
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    finally { availability.Inventoried = false; }
  }

  internal async Task RunIterationAsync(CancellationToken stoppingToken)
  {
    var rows = await WithScopeAsync((service, ct) => service.GetRequiredService<ProvisioningReadStore>()
        .PollAsync(after, Settings.BatchSize, client.Value.InvitationsEnabled && !transport.InvitationUnavailable, ct), stoppingToken);
    after = rows.Count == Settings.BatchSize ? rows[^1].WorkspaceId : null;
    await Parallel.ForEachAsync(rows, new ParallelOptions
    {
      MaxDegreeOfParallelism = Settings.ParallelAttempts,
      CancellationToken = stoppingToken
    }, RunCandidateAsync);
  }

  private async ValueTask RunCandidateAsync(ProvisioningSnapshot row, CancellationToken stoppingToken)
  {
    if (row.OperationId is not { } operation) return;
    var started = clock.GetTimestamp();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Settings.AttemptSeconds), clock);
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, stoppingToken);
    var token = linked.Token;
    var owner = Guid.NewGuid();
    var lease = TimeSpan.FromSeconds(Settings.LeaseSeconds);
    try
    {
      if (!Compatible(row)) return;
      if (row.Status == "registering")
      {
        await WriteAsync((db, ct) => ProvisioningPersistence.TryRecoverExpiredProvisioningAsync(
            db, row.WorkspaceId, operation, row.Version, ct), value => value, token);
        return;
      }
      var isDispatch = row.Ready;
      if (!isDispatch && !row.Recovery) return;
      LocalWriteResult<OperationFence?> claimed;
      try
      {
        claimed = await WriteAsync((db, ct) => isDispatch
            ? ProvisioningPersistence.TryBeginProvisioningAsync(db, row.WorkspaceId, operation, row.Version, owner, lease, ct)
            : ProvisioningPersistence.TryClaimVerificationAsync(db, row.WorkspaceId, operation, row.Version, owner, lease, ct),
            value => value is not null, token);
      }
      catch (Exception error) when (Recoverable(error))
      {
        // An observed Registering row never confers permission to dispatch.
        await ObserveUnconfirmedBeginAsync(row, owner, stoppingToken);
        return;
      }
      if (claimed.Status != LocalWriteStatus.Committed || claimed.Value is not { } fence) return;
      var current = await ReadOwnedAsync(row, fence, started, token);
      if (current is null) return;

      if (isDispatch)
      {
        var invitation = protection.ReadInvitation(row.WorkspaceId, operation, current.Invitation ?? []);
        var redemption = await AuthenticateAsync(current, fence, true, started, (auth, ct) =>
            auth.RedeemAsync(current.Identity(), new ViaDocsInvitation(invitation), ct), token);
        var candidate = redemption switch
        {
          ViaDocsResult<ViaDocsRegistrationCandidate>.Success success => success.Value,
          ViaDocsResult<ViaDocsRegistrationCandidate>.Failure failure => failure.Candidate,
          _ => null
        };
        if (candidate is null || candidate.ApplicationId == Guid.Empty || candidate.NormalizedName != current.Name)
        {
          LogFailure(redemption);
          return;
        }
        if (await ReadOwnedAsync(row, fence, started, token, false) is null) return;
        var recorded = await WriteAsync((db, ct) => ProvisioningPersistence.TryRecordProvisioningCandidateAsync(
            db, row.WorkspaceId, operation, fence, candidate.ApplicationId, ct), value => value is not null, token);
        if (recorded.Status != LocalWriteStatus.Committed || recorded.Value is not { } recordedFence) return;
        fence = recordedFence;
        if (redemption is ViaDocsResult<ViaDocsRegistrationCandidate>.Failure)
        {
          LogFailure(redemption);
          return;
        }
        current = await ReadOwnedAsync(row, fence, started, token);
        if (current is null) return;
      }

      var verification = await AuthenticateAsync(current, fence, false, started, (auth, ct) => auth.FindAsync(current.Identity(), ct), token);
      if (verification is not ViaDocsResult<ViaDocsVerifiedIdentity>.Success found)
      {
        LogFailure(verification);
        return;
      }
      var application = found.Value.ApplicationId;
      if (application == Guid.Empty || found.Value.NormalizedName != current.Name ||
          (current.Candidate is { } prior && prior != application))
      {
        logger.LogWarning("Technical provisioning verification conflicted with the prepared identity.");
        return;
      }
      try
      {
        if (await ReadOwnedAsync(row, fence, started, token, false) is null) return;
        await WriteAsync((db, ct) => ProvisioningPersistence.TryCompleteVerifiedAsync(
            db, row.WorkspaceId, operation, fence, application, ct), value => value, token);
      }
      catch (Exception error) when (Recoverable(error))
      {
        var observed = await ReadAsync(row.WorkspaceId, stoppingToken);
        var committed = observed?.OperationId == operation && observed.AccountId == row.AccountId &&
            observed.Status == "active" && observed.ApplicationId == application &&
            observed.Candidate == application && observed.VerifiedAt is not null;
        logger.LogInformation("Unconfirmed technical completion observed: {Committed}", committed);
      }
    }
    catch (Exception error) when (Recoverable(error) || error is CryptographicException)
    {
      if (!stoppingToken.IsCancellationRequested)
        logger.LogWarning("Technical provisioning attempt ended without a confirmed result.");
    }
  }

  private async Task<int> InventoryAsync(CancellationToken token)
  {
    var blocked = 0;
    Guid? cursor = null;
    while (true)
    {
      var page = await WithScopeAsync((service, ct) => service.GetRequiredService<ProvisioningReadStore>()
          .InventoryAsync(cursor, Settings.BatchSize, ct), token);
      foreach (var row in page)
        if (!Compatible(row)) blocked++;
      if (page.Count < Settings.BatchSize) return blocked;
      cursor = page[^1].WorkspaceId;
    }
  }

  private bool Compatible(ProvisioningSnapshot row)
  {
    if (row.AccountId is not { } account || row.Generation <= 0 || row.Secret is null ||
        row.Name != ProvisioningReadStore.TechnicalName(row.WorkspaceId) ||
        !ProvisioningProtection.HasCredentialFormat(row.Secret) ||
        (row.Status is "ready" or "registering") && !ProvisioningProtection.HasInvitationFormat(row.Invitation))
      return false;
    try
    {
      _ = protection.ReadCredential(row.WorkspaceId, account, row.Generation, row.Secret);
      return true;
    }
    catch (CryptographicException)
    {
      return false;
    }
  }

  private async Task ObserveUnconfirmedBeginAsync(ProvisioningSnapshot row, Guid owner, CancellationToken token)
  {
    var observed = await ReadAsync(row.WorkspaceId, token);
    var reserved = observed?.OperationId == row.OperationId && observed?.LeaseOwner == owner &&
        observed.Version == row.Version + 1;
    logger.LogInformation("Unconfirmed technical reservation observed: {Reserved}", reserved);
  }

  private async Task<ProvisioningSnapshot?> ReadOwnedAsync(ProvisioningSnapshot row, OperationFence fence, long started,
      CancellationToken token, bool network = true)
  {
    var current = await ReadAsync(row.WorkspaceId, token);
    var needed = Settings.ReserveSeconds + (network ? client.Value.ShortRequestSeconds : Settings.TransactionSeconds);
    return Settings.AttemptSeconds - clock.GetElapsedTime(started).TotalSeconds >= needed &&
        current is not null && current.OperationId == row.OperationId && current.AccountId == row.AccountId &&
        current.Generation == row.Generation && current.Name == row.Name && current.Owns(fence, needed)
        ? current : null;
  }

  private Task<ProvisioningSnapshot?> ReadAsync(Guid workspace, CancellationToken token) =>
      WithScopeAsync((service, ct) => service.GetRequiredService<ProvisioningReadStore>().ReadAsync(workspace, ct), token);

  private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> command, CancellationToken token)
  {
    await using var scope = scopes.CreateAsyncScope();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Settings.TransactionSeconds), clock);
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
    return await command(scope.ServiceProvider, linked.Token);
  }

  private Task<LocalWriteResult<T>> WriteAsync<T>(Func<MiniDocsDbContext, CancellationToken, Task<T>> action,
      Predicate<T> accepted, CancellationToken token) => WithScopeAsync((service, ct) =>
      LocalWriteTransaction.ExecuteAsync(service.GetRequiredService<MiniDocsDbContext>(), t =>
          action(service.GetRequiredService<MiniDocsDbContext>(), t), accepted, cancellationToken: ct), token);

  private async Task<T> AuthenticateAsync<T>(ProvisioningSnapshot row, OperationFence fence, bool redeem, long started,
      Func<IViaDocsAuthentication, CancellationToken, Task<T>> action, CancellationToken token)
  {
    await using var scope = scopes.CreateAsyncScope();
    scope.ServiceProvider.GetRequiredService<ProvisioningDispatchContext>().Current = new(row.WorkspaceId,
        row.OperationId!.Value, row.AccountId!.Value, row.Generation, fence, redeem,
        Settings.ReserveSeconds + client.Value.ShortRequestSeconds, started, Settings.AttemptSeconds);
    return await action(scope.ServiceProvider.GetRequiredService<IViaDocsAuthentication>(), token);
  }

  private void LogFailure<T>(ViaDocsResult<T> result)
  {
    if (result is ViaDocsResult<T>.Failure failure)
      logger.LogWarning("Technical provisioning authentication ended with {Kind}.", failure.Error.Kind);
  }

  private static bool Recoverable(Exception error) =>
      error.GetBaseException() is NpgsqlException or TimeoutException || error is OperationCanceledException;
}
