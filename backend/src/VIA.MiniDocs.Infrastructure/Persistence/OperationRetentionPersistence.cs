namespace VIA.MiniDocs.Infrastructure;

/// <summary>
/// Bounded workspace-scoped maintenance. Never removes documents, unresolved operations,
/// or permanent request tombstones. Caller owns the transaction.
/// </summary>
public static class OperationRetentionPersistence
{
  public static async Task<int> RetireCompletedUploadsAsync(MiniDocsDbContext db, Guid workspaceId,
      int limit, CancellationToken cancellationToken = default)
  {
    ValidateLimit(limit);
    // All reservation/retirement paths serialize on the same workspace row.
    // The deferred database constraint verifies tombstone and operation exclusivity at commit.
    return await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_retire_completed_uploads({workspaceId}, {limit})", cancellationToken);
  }

  public static async Task<int> RemoveExpiredReceiptsAsync(MiniDocsDbContext db, Guid workspaceId,
      int limit, CancellationToken cancellationToken = default)
  {
    ValidateLimit(limit);
    return await D2Commands.InvokeAsync<int>(db,
        $"SELECT minidocs.command_remove_expired_receipts({workspaceId}, {limit})", cancellationToken);
  }

  private static void ValidateLimit(int limit)
  {
    if (limit is < 1 or > 1000)
      throw new ArgumentOutOfRangeException(nameof(limit), "Maintenance batches must contain 1 to 1000 rows.");
  }
}
