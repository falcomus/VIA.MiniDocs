using System.Security.Cryptography;
using Npgsql;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed record ProvisioningDispatch(Guid Workspace, Guid Operation, Guid Account, long Generation,
    OperationFence Fence, bool Redeem, double MinimumLeaseSeconds, long Started, int AttemptSeconds);

internal sealed class ProvisioningDispatchContext
{
  internal ProvisioningDispatch? Current { get; set; }
}

internal interface IProvisioningDispatchCredentialProvider : IViaDocsCredentialProvider
{
  Task<bool> AuthorizeDispatchAsync(ViaDocsPreparedIdentity identity, ProvisioningDispatch attempt, bool redeem, CancellationToken token);
}

internal sealed class ProvisioningCredentialProvider(ProvisioningReadStore store, ProvisioningProtection protection, TimeProvider clock)
    : IProvisioningDispatchCredentialProvider
{
  public async Task<bool> AuthorizeDispatchAsync(ViaDocsPreparedIdentity identity, ProvisioningDispatch attempt,
      bool redeem, CancellationToken token)
  {
    var row = await store.ReadCredentialAsync(identity, token);
    return attempt.AttemptSeconds - clock.GetElapsedTime(attempt.Started).TotalSeconds >= attempt.MinimumLeaseSeconds &&
        attempt.Redeem == redeem && identity.WorkspaceId == attempt.Workspace &&
        identity.Credential.Value == attempt.Account && row is not null &&
        row.OperationId == attempt.Operation && row.AccountId == attempt.Account &&
        row.Generation == attempt.Generation && row.Owns(attempt.Fence, attempt.MinimumLeaseSeconds) &&
        (redeem ? row.Status == "registering" : row.Status is "registering" or "recovery_required");
  }
  public async ValueTask<ViaDocsCredential?> ResolveCurrentAsync(ViaDocsPreparedIdentity identity, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    if (identity.WorkspaceId == Guid.Empty || identity.Credential.Value == Guid.Empty ||
        identity.Name != ProvisioningReadStore.TechnicalName(identity.WorkspaceId))
      return null;
    try
    {
      var row = await store.ReadCredentialAsync(identity, cancellationToken);
      if (row?.Secret is null || (!row.Bound &&
          !(row.Unverified && row.LeaseOwner is not null && row.LeaseSeconds > 0 &&
            (row.Status == "registering" && row.Pending || row.Recovery))))
        return null;
      var secret = protection.ReadCredential(row.WorkspaceId, identity.Credential.Value, row.Generation, row.Secret);
      return new ViaDocsCredential(row.Generation, secret);
    }
    catch (Exception error) when (error is CryptographicException || error.GetBaseException() is NpgsqlException or TimeoutException)
    {
      cancellationToken.ThrowIfCancellationRequested();
      throw new ViaDocsCredentialUnavailableException();
    }
  }
}
