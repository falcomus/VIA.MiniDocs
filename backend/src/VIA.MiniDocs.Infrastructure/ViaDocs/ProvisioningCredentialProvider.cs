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
  ValueTask<ViaDocsCredential?> ResolveAuthorizedAsync(ViaDocsPreparedIdentity identity, Guid? application,
      ProvisioningDispatch? attempt, bool redeem, CancellationToken token);
}

internal sealed class ProvisioningCredentialProvider(ProvisioningReadStore store, ProvisioningProtection protection, TimeProvider clock)
    : IProvisioningDispatchCredentialProvider
{
  public ValueTask<ViaDocsCredential?> ResolveCurrentAsync(ViaDocsPreparedIdentity identity, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    return ValueTask.FromResult<ViaDocsCredential?>(null);
  }

  public async ValueTask<ViaDocsCredential?> ResolveAuthorizedAsync(ViaDocsPreparedIdentity identity,
      Guid? application, ProvisioningDispatch? attempt, bool redeem, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    if (identity.WorkspaceId == Guid.Empty || identity.Credential.Value == Guid.Empty ||
        identity.Name != ProvisioningReadStore.TechnicalName(identity.WorkspaceId))
      return null;
    if (application is null && (attempt is null || attempt.Redeem != redeem ||
        identity.WorkspaceId != attempt.Workspace || identity.Credential.Value != attempt.Account))
      return null;
    try
    {
      var row = await store.ReadCredentialAsync(identity, cancellationToken);
      if (row?.Secret is null)
        return null;
      var authorized = application is { } applicationId
          ? applicationId != Guid.Empty && row.Bound && row.ApplicationId == applicationId
          : attempt is not null && row.OperationId == attempt.Operation &&
            row.Generation == attempt.Generation && row.Owns(attempt.Fence, attempt.MinimumLeaseSeconds) &&
            (!redeem || row.Status == "registering") &&
            attempt.AttemptSeconds - clock.GetElapsedTime(attempt.Started).TotalSeconds >= attempt.MinimumLeaseSeconds;
      if (!authorized)
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
