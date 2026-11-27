using System.Security.Cryptography;
using System.Text.Json;

namespace VIA.MiniDocs.Api.Security;

internal static class BoundedJson
{
  private const int MaximumBytes = 16 * 1024;
  private static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Web)
  {
    AllowDuplicateProperties = false,
    PropertyNameCaseInsensitive = false
  };

  internal static async Task<(T? Input, IResult? Error)> ReadAsync<T>(HttpContext context, CancellationToken token)
      where T : class
  {
    if (!context.Request.HasJsonContentType())
      return (null, SessionHttp.Problem(context, 415, "unsupported_media_type", "A JSON request is required."));
    if (context.Request.ContentLength > MaximumBytes) return (null, TooLarge(context));
    var buffer = new byte[MaximumBytes + 1];
    try
    {
      var length = await context.Request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, token);
      if (length > MaximumBytes) return (null, TooLarge(context));
      var input = JsonSerializer.Deserialize<T>(buffer.AsSpan(0, length), Options);
      return input is null ? (null, Invalid(context)) : (input, null);
    }
    catch (JsonException) { return (null, Invalid(context)); }
    finally { CryptographicOperations.ZeroMemory(buffer); }
  }

  internal static IResult Invalid(HttpContext context) =>
      SessionHttp.Problem(context, 400, "validation_failed", "The request is invalid.");

  private static IResult TooLarge(HttpContext context) =>
      SessionHttp.Problem(context, 413, "payload_too_large", "The request is too large.");
}
