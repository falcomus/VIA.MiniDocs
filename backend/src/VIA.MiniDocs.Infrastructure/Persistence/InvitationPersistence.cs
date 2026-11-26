namespace VIA.MiniDocs.Infrastructure;

public static class InvitationPersistence
{
  public static Task<bool> TryLockValidAsync(MiniDocsDbContext db, byte[] tokenHash,
      CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db,
          $"SELECT minidocs.command_lock_valid_invitation({tokenHash})", cancellationToken);

  public static Task<bool> TryCreateAsync(MiniDocsDbContext db, Guid invitation, byte[] tokenHash, Guid creator,
      DateTime expiresAt, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db,
          $"SELECT minidocs.command_create_invitation({invitation}, {tokenHash}, {creator}, {expiresAt})",
          cancellationToken);

  public static Task<bool> TryRevokeAsync(MiniDocsDbContext db, Guid invitation, long version, Guid actor,
      CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db,
          $"SELECT minidocs.command_revoke_invitation({invitation}, {version}, {actor})", cancellationToken);

  // The standalone claim is only used to verify the low-level lock and expiry behavior.
  public static Task<bool> TryRedeemAsync(MiniDocsDbContext db, byte[] tokenHash,
      Guid userId, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db,
          $"SELECT minidocs.command_redeem_invitation({tokenHash}, {userId})", cancellationToken);

  /// <summary>Creates the workspace and system topic while consuming an invitation for the new Identity user.</summary>
  public static Task<bool> TryRegisterWorkspaceAsync(MiniDocsDbContext db, byte[] tokenHash,
      Guid userId, Guid workspaceId, Guid systemTopicId, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db,
          $"SELECT minidocs.command_register_workspace({tokenHash}, {userId}, {workspaceId}, {systemTopicId})",
          cancellationToken);
}
