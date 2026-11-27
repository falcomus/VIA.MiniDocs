using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;

namespace VIA.MiniDocs.Api.Security;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record RegistrationInput(string Token, string UserName, string Password);
internal sealed record RegistrationResult(Guid UserId, SessionWorkspaceStatus WorkspaceStatus);

internal sealed class RegistrationRateLimits(TimeProvider clock)
{
  private readonly LoginRateLimits limits = new(clock);
  private readonly SemaphoreSlim slots = new(8);

  internal bool TryAcquire(string ip, string name, out int retryAfter)
  {
    retryAfter = 1;
    if (!slots.Wait(0)) return false;
    if (limits.TryAcquire(ip, name, out retryAfter)) return true;
    slots.Release();
    return false;
  }

  internal void Release() => slots.Release();
}

internal static class RegistrationHttp
{
  internal static async Task<IResult> RegisterAsync(HttpContext context, Registration registration,
      UserManager<MiniDocsUser> users, RegistrationRateLimits limits, CancellationToken token)
  {
    var (input, error) = await BoundedJson.ReadAsync<RegistrationInput>(context, token);
    if (error is not null) return error;
    if (input is null || !IdentityInput.IsUserName(input.UserName) || input.Password is not { Length: >= 12 and <= 128 }
        || !TryHashToken(input.Token, out var hash))
      return BoundedJson.Invalid(context);
    var normalized = users.NormalizeName(input.UserName);
    if (normalized.Length > 256) return BoundedJson.Invalid(context);
    var address = context.Connection.RemoteIpAddress;
    var ip = (address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address)?.ToString() ?? "unknown";
    if (!limits.TryAcquire(ip, normalized, out var retryAfter))
    {
      context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
      return SessionHttp.Problem(context, 429, "rate_limited", "Too many registration attempts.");
    }
    RegistrationOutcome result;
    try { result = await registration.RegisterAsync(hash, input.UserName, input.Password, token); }
    finally { limits.Release(); }
    return result.Status switch
    {
      RegistrationStatus.Registered => TypedResults.Json(new RegistrationResult(result.UserId, SessionWorkspaceStatus.Pending), statusCode: 201),
      RegistrationStatus.UnavailableInvitation => SessionHttp.Problem(context, 410, "invitation_expired", "The invitation is unavailable."),
      RegistrationStatus.Conflict => SessionHttp.Problem(context, 409, "state_conflict", "Registration could not be completed."),
      RegistrationStatus.Unknown => SessionHttp.Problem(context, 503, "dependency_unavailable", "The result could not be confirmed."),
      _ => BoundedJson.Invalid(context)
    };
  }

  internal static bool TryHashToken(string? token, out byte[] hash)
  {
    hash = [];
    if (token is not { Length: 43 }) return false;
    try
    {
      var value = WebEncoders.Base64UrlDecode(token);
      try
      {
        if (value.Length != 32 || WebEncoders.Base64UrlEncode(value) != token) return false;
        hash = SHA256.HashData(value);
        return true;
      }
      finally { CryptographicOperations.ZeroMemory(value); }
    }
    catch (FormatException) { return false; }
  }
}
