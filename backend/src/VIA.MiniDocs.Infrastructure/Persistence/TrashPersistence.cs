namespace VIA.MiniDocs.Infrastructure;

/// <summary>A local lease bound to the exact remote file that may be processed.</summary>
public readonly record struct PurgeReservation(Guid RemoteFileId, OperationFence Fence);

/// <summary>Authority to reconcile a remote result, never to send a DELETE.</summary>
public readonly record struct DeleteVerificationLease(Guid RemoteFileId, OperationFence Fence);

/// <summary>
/// Persistence primitives for authorized trash operations. Caller owns the transaction,
/// must roll back on failure, and must commit the reservation before any remote call.
/// A lease obtained for verification never authorizes another remote DELETE.
/// </summary>
public static class TrashPersistence
{
  public static async Task<bool> TryTrashAsync(MiniDocsDbContext db, Guid workspaceId,
      Guid documentId, long expectedVersion, CancellationToken cancellationToken = default)
  {
    return await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_trash({workspaceId}, {documentId}, {expectedVersion})", cancellationToken) == 1;
  }

  public static async Task<bool> TryRestoreAsync(MiniDocsDbContext db, Guid workspaceId,
      Guid documentId, long expectedVersion, CancellationToken cancellationToken = default)
  {
    return await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_restore({workspaceId}, {documentId}, {expectedVersion})", cancellationToken) == 1;
  }

  /// <summary>
  /// Reserve a single external DELETE. The caller must commit before sending it.
  /// A false/null result never authorizes an external operation.
  /// </summary>
  public static async Task<PurgeReservation?> TryBeginPurgeAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, long expectedDocumentVersion, Guid operationId,
      Guid owner, TimeSpan duration, CancellationToken cancellationToken = default)
  {
    D2Commands.ValidateLease(owner, duration);
    if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
    var remote = await D2Commands.InvokeAsync<Guid?>(db,
        $"SELECT minidocs.command_begin_purge({workspaceId}, {documentId}, {expectedDocumentVersion}, {operationId}, {owner}, {duration})", cancellationToken);
    return remote is Guid fileId ? new PurgeReservation(fileId, new OperationFence(owner, 2)) : null;
  }

  /// <summary>A possible remote side effect remains unresolved; no second DELETE is authorized.</summary>
  public static async Task<bool> TryMarkDeleteUnknownAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, OperationFence fence,
      CancellationToken cancellationToken = default)
  {
    return await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_mark_delete_unknown({workspaceId}, {documentId}, {operationId}, {fence.Version}, {fence.Owner})", cancellationToken) == 1;
  }

  public static async Task<bool> TryRecoverExpiredDeleteAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, long expectedVersion,
      CancellationToken cancellationToken = default)
  {
    return await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_recover_expired_delete({workspaceId}, {documentId}, {operationId}, {expectedVersion})", cancellationToken) == 1;
  }

  /// <summary>
  /// Reserve a read-only reconciliation of an unknown DELETE; do not send another DELETE.
  /// An expired verification lease may be replaced because it cannot perform a remote write.
  /// </summary>
  public static async Task<DeleteVerificationLease?> TryBeginDeleteVerificationAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, long expectedVersion, Guid owner,
      TimeSpan duration, CancellationToken cancellationToken = default)
  {
    D2Commands.ValidateLease(owner, duration);
    var remote = await D2Commands.InvokeAsync<Guid?>(db,
        $"SELECT minidocs.command_begin_delete_verification({workspaceId}, {documentId}, {operationId}, {expectedVersion}, {owner}, {duration})", cancellationToken);
    return remote is Guid fileId ? new DeleteVerificationLease(fileId, new OperationFence(owner, checked(expectedVersion + 1))) : null;
  }

  /// <summary>
  /// Only after the authorized caller has confirmed the exact remote deletion or proven
  /// its absence under the correct technical account. A timeout, 401/403 or unqualified
  /// 404 is not evidence. This method does not perform or verify remote I/O.
  /// </summary>
  public static Task<bool> TryCompleteConfirmedPurgeAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, PurgeReservation reservation,
      CancellationToken cancellationToken = default) =>
      TryCompleteAsync(db, workspaceId, documentId, operationId, reservation.RemoteFileId,
          reservation.Fence, false, cancellationToken);

  /// <summary>Complete after authorized verification of the exact remote file's absence.</summary>
  public static Task<bool> TryCompleteConfirmedPurgeAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, DeleteVerificationLease verification,
      CancellationToken cancellationToken = default) =>
      TryCompleteAsync(db, workspaceId, documentId, operationId, verification.RemoteFileId,
          verification.Fence, true, cancellationToken);

  private static async Task<bool> TryCompleteAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, Guid remoteFileId, OperationFence fence,
      bool verificationOnly, CancellationToken cancellationToken)
  {
    return await D2Commands.InvokeAsync<bool>(db,
        $"SELECT minidocs.command_complete_purge({workspaceId}, {documentId}, {operationId}, {remoteFileId}, {fence.Version}, {fence.Owner}, {verificationOnly})", cancellationToken);
  }

}
