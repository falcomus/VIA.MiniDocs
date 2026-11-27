using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;

namespace VIA.MiniDocs.Api.Security;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Login(string UserName, string Password);
internal sealed record Session(Guid UserId, string DisplayName, string[] Roles, Guid? WorkspaceId, SessionWorkspaceStatus? WorkspaceStatus);
internal sealed record AntiForgery(string RequestToken);

internal enum SessionWorkspaceStatus
{
  [JsonStringEnumMemberName("pending")] Pending,
  [JsonStringEnumMemberName("active")] Active,
  [JsonStringEnumMemberName("suspended")] Suspended
}

internal static class SessionHttp
{
  internal const string SessionCookie = "__Host-MiniDocsSession";
  internal const string AntiforgeryCookie = "__Host-Antiforgery";
  internal const string AntiforgeryHeader = "X-CSRF-TOKEN";
  internal const string HandleClaim = "minidocs:session";
  internal const string StampClaim = "minidocs:stamp";
  private const int MaxBodyBytes = 16 * 1024;

  internal static void MapMiniDocsSessions(this WebApplication app)
  {
    var routes = app.MapGroup("/api/v1").WithMetadata(new SessionRoute());
    routes.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        TypedResults.Ok(new AntiForgery(antiforgery.GetAndStoreTokens(context).RequestToken!)))
        .AllowAnonymous().ProducesProblem(503);
    routes.MapPost("/session/login", LoginAsync).AllowAnonymous().WithMetadata(new ValidateSessionAntiforgery())
        .Produces<Session>()
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(413)
        .ProducesProblem(415).ProducesProblem(423).ProducesProblem(429).ProducesProblem(503);
    routes.MapGet("/session", (HttpContext context) =>
        TypedResults.Ok(ToResponse((SessionSnapshot)context.Items[typeof(SessionSnapshot)]!)))
        .RequireAuthorization().ProducesProblem(401).ProducesProblem(503);
    routes.MapDelete("/session", LogoutAsync).RequireAuthorization().WithMetadata(new ValidateSessionAntiforgery())
        .Produces(204).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503);
  }

  internal static IResult Problem(HttpContext context, int status, string code, string title) =>
      Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?>
      {
        ["code"] = code,
        ["traceId"] = context.TraceIdentifier
      });

  internal static IResult Unauthorized(HttpContext context) =>
      Problem(context, 401, "authentication_required", "Authentication is required.");

  internal static ClaimsPrincipal Principal(SessionAuthority session, string handle, SessionSnapshot? snapshot)
  {
    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, session.UserId.ToString("D")),
      new(HandleClaim, handle),
      new(StampClaim, session.Stamp)
    };
    if (snapshot is not null)
    {
      claims.Add(new Claim(ClaimTypes.Name, snapshot.UserName));
      claims.AddRange(snapshot.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
    }
    return new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
  }

  internal static SessionAuthority? ReadAuthority(ClaimsPrincipal? principal)
  {
    if (principal?.Identity?.IsAuthenticated != true ||
        !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
        principal.FindFirstValue(HandleClaim) is not { Length: 43 } handle ||
        principal.FindFirstValue(StampClaim) is not { Length: > 0 } stamp)
      return null;
    try
    {
      var raw = WebEncoders.Base64UrlDecode(handle);
      return raw.Length == 32 ? new SessionAuthority(userId, SHA256.HashData(raw), stamp) : null;
    }
    catch (FormatException) { return null; }
  }

  private static async Task<IResult> LoginAsync(HttpContext context, UserManager<MiniDocsUser> users,
      SignInManager<MiniDocsUser> signIn, DummyPassword dummy, LoginRateLimits limits, SessionStore sessions,
      IOptions<SessionLifetime> lifetime, CancellationToken cancellationToken)
  {
    if (!context.Request.HasJsonContentType())
      return Problem(context, 415, "unsupported_media_type", "A JSON request is required.");
    if (context.Request.ContentLength > MaxBodyBytes) return TooLarge(context);

    var body = new byte[MaxBodyBytes + 1];
    var length = await context.Request.Body.ReadAtLeastAsync(body, body.Length, false, cancellationToken);
    Login? input;
    try
    {
      if (length > MaxBodyBytes) return TooLarge(context);
      input = JsonSerializer.Deserialize<Login>(body.AsSpan(0, length), JsonSerializerOptions.Web);
    }
    catch (JsonException) { return InvalidInput(context); }
    finally { CryptographicOperations.ZeroMemory(body); }

    if (input is null || !IdentityInput.IsUserName(input.UserName) || input.Password is not { Length: > 0 and <= 1024 })
      return InvalidInput(context);
    var normalized = users.NormalizeName(input.UserName!);
    if (normalized.Length > 256) return InvalidInput(context);
    var ip = context.Connection.RemoteIpAddress;
    var partition = (ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4() : ip)?.ToString() ?? "unknown";
    if (!limits.TryAcquire(partition, normalized, out var retryAfter))
    {
      context.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
      return Problem(context, 429, "rate_limited", "Too many login attempts.");
    }

    var user = await users.FindByNameAsync(input.UserName!);
    if (user is null || user.IsDisabled || user.TwoFactorEnabled || user.PasswordHash is null)
    {
      dummy.Verify(users.PasswordHasher, input.Password);
      return Unauthorized(context);
    }
    if (await users.IsLockedOutAsync(user))
      return users.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, input.Password)
          == PasswordVerificationResult.Failed ? Unauthorized(context)
          : Problem(context, 423, "account_locked", "Login is temporarily unavailable for this account.");
    var result = await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
    if (!result.Succeeded || user.SecurityStamp is not { Length: > 0 } stamp) return Unauthorized(context);

    var raw = RandomNumberGenerator.GetBytes(32);
    var handle = WebEncoders.Base64UrlEncode(raw);
    var authority = new SessionAuthority(user.Id, SHA256.HashData(raw), stamp);
    var snapshot = await sessions.CreateAsync(authority, ReadAuthority(context.User), lifetime.Value.Idle,
        lifetime.Value.Absolute, cancellationToken);
    if (snapshot is null) return Unauthorized(context);

    // No cookie is emitted before the database confirms the local commit.
    await context.SignInAsync(IdentityConstants.ApplicationScheme, Principal(authority, handle, null),
        new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow + lifetime.Value.Absolute });
    ClearAntiforgery(context);
    return TypedResults.Ok(ToResponse(snapshot));
  }

  private static async Task<IResult> LogoutAsync(HttpContext context, SessionStore sessions, CancellationToken cancellationToken)
  {
    if (ReadAuthority(context.User) is not { } session) return Unauthorized(context);
    await sessions.RevokeAsync(session, cancellationToken);
    await context.SignOutAsync(IdentityConstants.ApplicationScheme);
    ClearAntiforgery(context);
    return TypedResults.NoContent();
  }

  private static void ClearAntiforgery(HttpContext context) => context.Response.Cookies.Delete(AntiforgeryCookie,
      new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/" });

  private static Session ToResponse(SessionSnapshot snapshot) =>
      new(snapshot.UserId, snapshot.UserName, snapshot.Roles, snapshot.WorkspaceId, ParseWorkspaceStatus(snapshot.WorkspaceStatus));

  internal static SessionWorkspaceStatus? ParseWorkspaceStatus(string? value) => value switch
      {
        null => null,
        "pending" => SessionWorkspaceStatus.Pending,
        "active" => SessionWorkspaceStatus.Active,
        "suspended" => SessionWorkspaceStatus.Suspended,
        _ => throw new InvalidOperationException("Invalid stored workspace status.")
      };

  private static IResult InvalidInput(HttpContext context) =>
      Problem(context, 400, "validation_failed", "The login request is invalid.");

  private static IResult TooLarge(HttpContext context) =>
      Problem(context, 413, "payload_too_large", "The login request is too large.");
}

internal sealed class SessionRoute;
internal sealed class ValidateSessionAntiforgery;

internal sealed class SessionAntiforgeryBinding : IAntiforgeryAdditionalDataProvider
{
  public string GetAdditionalData(HttpContext context) => context.User.FindFirstValue(SessionHttp.HandleClaim) ?? "";

  public bool ValidateAdditionalData(HttpContext context, string additionalData) =>
      string.Equals(GetAdditionalData(context), additionalData, StringComparison.Ordinal);
}

internal sealed class MiniDocsCookieEvents(SessionStore sessions, IOptions<SessionLifetime> lifetime) : CookieAuthenticationEvents
{
  public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
  {
    var authority = SessionHttp.ReadAuthority(context.Principal);
    if (authority is null) { context.RejectPrincipal(); return; }
    var cancellation = context.HttpContext.RequestAborted;
    var snapshot = await sessions.ReadAsync(authority, lifetime.Value.Idle, cancellation);
    if (snapshot?.RenewalDue == true && !HttpMethods.IsDelete(context.Request.Method)
        && context.Request.Path != "/api/v1/session/login")
    {
      await sessions.RenewAsync(authority, lifetime.Value.Idle, cancellation);
      snapshot = await sessions.ReadAsync(authority, lifetime.Value.Idle, cancellation);
    }
    if (snapshot is null) { context.RejectPrincipal(); return; }
    context.HttpContext.Items[typeof(SessionSnapshot)] = snapshot;
    context.ReplacePrincipal(SessionHttp.Principal(authority,
        context.Principal!.FindFirstValue(SessionHttp.HandleClaim)!, snapshot));
  }

  public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
      SessionHttp.Unauthorized(context.HttpContext).ExecuteAsync(context.HttpContext);

  public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
      SessionHttp.Problem(context.HttpContext, 403, "forbidden", "Access is denied.").ExecuteAsync(context.HttpContext);
}
