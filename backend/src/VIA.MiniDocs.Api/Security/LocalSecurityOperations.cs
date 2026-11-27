using Npgsql;
using VIA.MiniDocs.Infrastructure.Identity;

namespace VIA.MiniDocs.Api.Security;

internal static class LocalSecurityOperations
{
  internal static async Task<int> RunAsync(IServiceProvider services, string operation)
  {
    await using var scope = services.CreateAsyncScope();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try
    {
      if (operation == "--cleanup-sessions")
      {
        var count = await scope.ServiceProvider.GetRequiredService<SessionStore>().CleanupAsync(500, deadline.Token);
        Console.WriteLine($"Expired sessions removed: {count}");
        return 0;
      }

      // A protected local credential source writes username and password to
      // redirected stdin. There is no echo and no argument/config fallback.
      if (!Console.IsInputRedirected) return Reject();
      var user = await Console.In.ReadLineAsync(deadline.Token);
      var password = await Console.In.ReadLineAsync(deadline.Token);
      if (user is null || password is null) return Reject();
      var completed = await scope.ServiceProvider.GetRequiredService<AdministratorBootstrap>()
          .CreateAsync(user, password, deadline.Token);
      if (!completed) return Reject();
      Console.WriteLine("Administrator bootstrap committed.");
      return 0;
    }
    catch (Exception exception) when (exception is NpgsqlException or OperationCanceledException or InvalidOperationException)
    {
      Console.Error.WriteLine("The local operation could not be confirmed. Check local configuration and database state.");
      return 2;
    }
  }

  private static int Reject()
  {
    Console.Error.WriteLine("Administrator bootstrap was refused.");
    return 1;
  }
}
