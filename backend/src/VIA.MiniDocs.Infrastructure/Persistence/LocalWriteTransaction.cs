using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace VIA.MiniDocs.Infrastructure;

public enum LocalWriteStatus
{
  Committed,
  Rejected,
  ConcurrencyConflict
}

public readonly record struct LocalWriteResult<T>(LocalWriteStatus Status, T? Value);

/// <summary>Owns the transaction for a local command and exposes its result only after commit.</summary>
public static class LocalWriteTransaction
{
  /// <remarks>The callback is limited to local database work. The caller decides whether a conflict is safe to retry.</remarks>
  public static async Task<LocalWriteResult<T>> ExecuteAsync<T>(MiniDocsDbContext db,
      Func<CancellationToken, Task<T>> command, Predicate<T> accepted,
      IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
  {
    if (db.ChangeTracker.HasChanges())
      throw new InvalidOperationException("Start a local write with a clean change tracker.");

    await using var tx = await db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
    try
    {
      var value = await command(cancellationToken);
      if (!accepted(value))
      {
        await tx.RollbackAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
        return new(LocalWriteStatus.Rejected, default);
      }
      if (db.ChangeTracker.HasChanges())
        throw new InvalidOperationException("A local write cannot commit pending tracked changes.");
      // A lost commit acknowledgement must escape without a lease result or
      // retry. The caller reconciles it by the previously allocated operation ID.
      await tx.CommitAsync(cancellationToken);
      db.ChangeTracker.Clear();
      return new(LocalWriteStatus.Committed, value);
    }
    catch (Exception ex) when (ex.GetBaseException() is PostgresException
        { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure })
    {
      // Commit-time failures may already have ended the transaction. Disposal rolls back
      // an open transaction before the conflict result is returned to the caller.
      return new(LocalWriteStatus.ConcurrencyConflict, default);
    }
    finally
    {
      db.ChangeTracker.Clear();
    }
  }
}
