using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace VIA.MiniDocs.Infrastructure;

internal static class D2Commands
{
  internal static async Task<T> InvokeAsync<T>(MiniDocsDbContext db, FormattableString sql,
      CancellationToken cancellationToken)
  {
    if (db.Database.CurrentTransaction is null)
      throw new InvalidOperationException("An explicit database transaction is required.");
    // Materialize the single function result without a composable outer SQL query.
    try
    {
      var results = await db.Database.SqlQuery<T>(sql).ToListAsync(cancellationToken);
      return results.Single();
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidParameterValue &&
        ex.ConstraintName == "ck_command_argument")
    {
      throw new ArgumentException("Invalid persistence command argument.");
    }
  }

  internal static void ValidateLease(Guid owner, TimeSpan duration)
  {
    if (owner == Guid.Empty)
      throw new ArgumentException("A nonempty lease owner is required.", nameof(owner));
    if (duration <= TimeSpan.Zero || duration > D2LeasePolicy.MaximumDuration || duration.Ticks % 10 != 0)
      throw new ArgumentOutOfRangeException(nameof(duration), "Lease duration must be positive, representable in PostgreSQL and within the lease limit.");
  }
}
