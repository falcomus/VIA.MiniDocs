using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

/// <summary>Identifies one local write authority. It is not a remote idempotency key.</summary>
public readonly record struct OperationFence(Guid Owner, long Version);

/// <summary>
/// Short, caller-owned transactions; never hold these locks over external HTTP calls.
/// Every successful update advances the version, invalidating the previously issued fence.
/// </summary>
public static class OperationLeasePersistence
{
  public static Task<bool> TryReserveUploadAsync(MiniDocsDbContext db, Guid workspace, Guid document,
      Guid topic, Guid operation, Guid request, string title, DocumentKind kind,
      CancellationToken cancellationToken = default)
  {
    var storedKind = (string)DomainTextConverters.DocumentKind.ConvertToProvider(kind)!;
    return D2Commands.InvokeAsync<bool>(db, $"""
        SELECT minidocs.command_reserve_upload({workspace}, {document}, {topic}, {operation}, {request}, {title}, {storedKind})
        """, cancellationToken);
  }

  public static async Task<OperationFence?> TryBeginUploadAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, long expectedVersion,
      Guid owner, TimeSpan duration, CancellationToken cancellationToken = default)
  {
    D2Commands.ValidateLease(owner, duration);
    var count = await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_begin_upload({workspaceId}, {documentId}, {operationId}, {owner}, {duration}, {expectedVersion})", cancellationToken);
    return count == 1 ? new OperationFence(owner, checked(expectedVersion + 1)) : null;
  }

  /// <summary>Persist an observed remote ID promptly, even before digest verification is available.</summary>
  public static async Task<OperationFence?> TryRecordUploadRemoteAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, OperationFence fence,
      Guid remoteFileId, CancellationToken cancellationToken = default)
  {
    if (remoteFileId == Guid.Empty) throw new ArgumentException("Remote file ID is required.", nameof(remoteFileId));
    var count = await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_record_upload_remote({workspaceId}, {documentId}, {operationId}, {remoteFileId}, {fence.Version}, {fence.Owner})", cancellationToken);
    return count == 1 ? new OperationFence(fence.Owner, checked(fence.Version + 1)) : null;
  }

  /// <summary>Persist fully measured local bytes, never a caller-provided expected hash.</summary>
  public static async Task<OperationFence?> TryRecordUploadDigestAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, OperationFence fence,
      long measuredSize, byte[] measuredSha256, CancellationToken cancellationToken = default)
  {
    ValidateDigest(measuredSize, measuredSha256);
    var count = await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_record_upload_digest({workspaceId}, {documentId}, {operationId}, {measuredSize}, {measuredSha256}, {fence.Version}, {fence.Owner})", cancellationToken);
    return count == 1 ? new OperationFence(fence.Owner, checked(fence.Version + 1)) : null;
  }

  /// <summary>Reclaim only a known remote ID for verification/local completion; never authorize another POST.</summary>
  public static async Task<OperationFence?> TryReclaimKnownUploadAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, long expectedVersion,
      Guid owner, TimeSpan duration, CancellationToken cancellationToken = default)
  {
    D2Commands.ValidateLease(owner, duration);
    var count = await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_reclaim_known_upload({workspaceId}, {documentId}, {operationId}, {owner}, {duration}, {expectedVersion})", cancellationToken);
    return count == 1 ? new OperationFence(owner, checked(expectedVersion + 1)) : null;
  }

  /// <summary>
  /// Commit association only after the caller has verified the remote metadata.
  /// Both rows change atomically, and the verified digest must match the recorded local bytes.
  /// </summary>
  public static async Task<bool> TryCompleteUploadAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, long expectedDocumentVersion, OperationFence fence,
      Guid remoteFileId, long remoteSize, byte[] remoteSha256, CancellationToken cancellationToken = default)
  {
    ValidateDigest(remoteSize, remoteSha256);
    return await D2Commands.InvokeAsync<bool>(db,
        $"""
        SELECT minidocs.command_complete_upload({workspaceId}, {documentId}, {operationId},
          {expectedDocumentVersion}, {fence.Version}, {fence.Owner}, {remoteFileId}, {remoteSize}, {remoteSha256})
        """, cancellationToken);
  }

  /// <summary>An expired in-flight upload is unknown; preserve its reservation and never retry it.</summary>
  public static async Task<bool> TryRecoverExpiredUploadAsync(MiniDocsDbContext db,
      Guid workspaceId, Guid documentId, Guid operationId, long expectedVersion,
      CancellationToken cancellationToken = default)
  {
    return await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_recover_expired_upload({workspaceId}, {documentId}, {operationId}, {expectedVersion})", cancellationToken) == 1;
  }

  public static Task<bool> TryFailUnstartedUploadAsync(MiniDocsDbContext db, Guid workspace, Guid document,
      Guid operation, long version, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_fail_unstarted_upload({workspace}, {document}, {operation}, {version})
          """, cancellationToken);

  public static Task<bool> TryReconcileKnownRemoteAsync(MiniDocsDbContext db, Guid workspace, Guid document,
      Guid operation, long version, Guid verifiedRemoteId, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_reconcile_unknown_upload({workspace}, {document}, {operation}, {version}, {verifiedRemoteId})
          """, cancellationToken);


  private static void ValidateDigest(long size, byte[] digest)
  {
    ArgumentNullException.ThrowIfNull(digest);
    if (size <= 0 || digest.Length != 32)
      throw new ArgumentException("A measured nonempty file and SHA-256 digest are required.");
  }
}
