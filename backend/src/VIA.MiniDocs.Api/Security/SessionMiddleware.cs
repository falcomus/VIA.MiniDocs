using Microsoft.AspNetCore.Antiforgery;
using Npgsql;

namespace VIA.MiniDocs.Api.Security;

internal sealed class SessionMiddleware(RequestDelegate next, ILogger<SessionMiddleware> logger, IWebHostEnvironment environment)
{
  public async Task InvokeAsync(HttpContext context)
  {
    if (context.GetEndpoint()?.Metadata.GetMetadata<SessionRoute>() is null)
    {
      await next(context);
      return;
    }
    context.Response.Headers.CacheControl = "no-store";
    if (environment.IsEnvironment("ContractTests"))
    {
      await SessionHttp.Problem(context, 503, "dependency_unavailable", "A required service is unavailable.").ExecuteAsync(context);
      return;
    }
    if (!context.Request.IsHttps)
    {
      await SessionHttp.Problem(context, 400, "validation_failed", "HTTPS is required.").ExecuteAsync(context);
      return;
    }
    try { await next(context); }
    catch (Exception exception) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
    {
      var dependencyError = IsDependencyError(exception);
      logger.LogWarning("Session request failed ({Status}) {TraceId}", dependencyError ? 503 : 500, context.TraceIdentifier);
      context.Response.Clear();
      context.Response.Headers.CacheControl = "no-store";
      await (dependencyError
          ? SessionHttp.Problem(context, 503, "dependency_unavailable", "A required service is unavailable.")
          : SessionHttp.Problem(context, 500, "internal_error", "An unexpected error occurred.")).ExecuteAsync(context);
    }
  }

  private static bool IsDependencyError(Exception exception) =>
      exception is NpgsqlException or TimeoutException ||
      exception.InnerException is { } cause && IsDependencyError(cause);
}

internal sealed class SessionAntiforgeryMiddleware(RequestDelegate next)
{
  public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
  {
    if (context.GetEndpoint()?.Metadata.GetMetadata<ValidateSessionAntiforgery>() is not null)
    {
      try { await antiforgery.ValidateRequestAsync(context); }
      catch (AntiforgeryValidationException)
      {
        await SessionHttp.Problem(context, 403, "antiforgery_invalid", "The request token is invalid.").ExecuteAsync(context);
        return;
      }
    }
    await next(context);
  }
}
