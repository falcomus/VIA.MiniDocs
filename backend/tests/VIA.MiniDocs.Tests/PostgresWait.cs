using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Infrastructure;

namespace VIA.MiniDocs.Tests;

internal static class PostgresWait
{
  internal static Task ForBlockedAsync(MiniDocsDbContext observer, int pid, Task contender) =>
      ForBlockedAsync(observer, () => pid, contender);

  internal static async Task ForBlockedAsync(MiniDocsDbContext observer, Func<int> readPid, Task contender)
  {
    var elapsed = Stopwatch.StartNew();
    while (elapsed.Elapsed < TimeSpan.FromSeconds(8))
    {
      if (contender.IsCompleted)
      {
        await contender;
        throw new InvalidOperationException("The concurrent writer did not wait for the required lock.");
      }
      var pid = readPid();
      if (pid != 0 && await observer.Database.SqlQuery<bool>(
          $"""SELECT cardinality(pg_blocking_pids({pid})) > 0 AS "Value" """).SingleAsync())
        return;
      await Task.Delay(25);
    }
    throw new TimeoutException("PostgreSQL did not report the expected blocked writer.");
  }
}
