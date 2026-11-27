using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Infrastructure;

namespace VIA.MiniDocs.Tests;

internal static class PostgresObservations
{
  // D2 assertions inspect the database behind the application's tenant reader.
  internal static IQueryable<T> ObserveAllWorkspaces<T>(this DbSet<T> rows) where T : class =>
      rows.IgnoreQueryFilters([MiniDocsDbContext.WorkspaceIsolationFilter]);
}
