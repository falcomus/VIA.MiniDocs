namespace VIA.MiniDocs.Infrastructure;

public static class ProvisioningPersistence
{
  public static async Task<OperationFence?> TryBeginProvisioningAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid operationId, long expectedVersion, Guid owner, TimeSpan duration,
      CancellationToken cancellationToken = default)
  {
    D2Commands.ValidateLease(owner, duration);
    var count = await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_begin_provisioning({workspaceId}, {operationId}, {owner}, {duration}, {expectedVersion})", cancellationToken);
    return count == 1 ? new OperationFence(owner, checked(expectedVersion + 1)) : null;
  }

  public static async Task<OperationFence?> TryRecordProvisioningCandidateAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid operationId, OperationFence fence, Guid candidateApplicationId,
      CancellationToken cancellationToken = default)
  {
    if (candidateApplicationId == Guid.Empty)
      throw new ArgumentException("Candidate application ID is required.", nameof(candidateApplicationId));
    var count = await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_record_provisioning_candidate({workspaceId}, {operationId}, {candidateApplicationId}, {fence.Version}, {fence.Owner})", cancellationToken);
    return count == 1 ? new OperationFence(fence.Owner, checked(fence.Version + 1)) : null;
  }

  /// <summary>Expired registration is unknown, never automatically ready for a second POST.</summary>
  public static async Task<bool> TryRecoverExpiredProvisioningAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid operationId, long expectedVersion, CancellationToken cancellationToken = default)
  {
    return await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_recover_expired_provisioning({workspaceId}, {operationId}, {expectedVersion})", cancellationToken) == 1;
  }

  public static Task<bool> TryPrepareAsync(MiniDocsDbContext db, Guid workspace, Guid account,
      Guid operation, string name, byte[] protectedSecret, byte[] protectedInvitation,
      CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_prepare_provisioning(
            {workspace}, {account}, {operation}, {name}, {protectedSecret}, {protectedInvitation})
          """, cancellationToken);

  /// <summary>Reserves a verification attempt. This fence does not authorize another registration POST.</summary>
  public static async Task<OperationFence?> TryClaimVerificationAsync(MiniDocsDbContext db, Guid workspace,
      Guid operation, long version, Guid owner, TimeSpan duration, CancellationToken cancellationToken = default)
  {
    D2Commands.ValidateLease(owner, duration);
    var next = await D2Commands.InvokeAsync<long?>(db, $"""
        SELECT minidocs.command_claim_provisioning_verification({workspace}, {operation}, {version}, {owner}, {duration})
        """, cancellationToken);
    return next is long value ? new OperationFence(owner, value) : null;
  }

  /// <summary>Commits local activation after the caller verifies the prepared identity with VIA.Docs.</summary>
  public static Task<bool> TryCompleteVerifiedAsync(MiniDocsDbContext db, Guid workspace, Guid operation,
      OperationFence fence, Guid application, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_complete_provisioning(
            {workspace}, {operation}, {fence.Version}, {fence.Owner}, {application})
          """, cancellationToken);

  public static Task<bool> TryReplaceInvitationAsync(MiniDocsDbContext db, Guid workspace, Guid operation,
      long expectedVersion, byte[] protectedInvitation, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_replace_technical_invitation(
            {workspace}, {operation}, {expectedVersion}, {protectedInvitation})
          """, cancellationToken);
}
