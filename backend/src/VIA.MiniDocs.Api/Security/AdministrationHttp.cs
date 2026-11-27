using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;

namespace VIA.MiniDocs.Api.Security;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record InvitationCreate(DateTime? ExpiresAt = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AdminUserPatch([property: JsonRequired] bool IsDisabled);
internal sealed record Invite(Guid Id, DateTime ExpiresAt, DateTime? RedeemedAt, DateTime? RevokedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Token = null);
internal sealed record AdminUser(Guid Id, string? UserName, bool IsDisabled, AdminAccountKind AccountKind, string[] Roles, string Etag);
internal enum AdminAccountKind
{
  [JsonStringEnumMemberName("regular")] Regular,
  [JsonStringEnumMemberName("admin_only")] AdminOnly
}
internal sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
internal sealed record WorkspaceResponse(Guid Id, SessionWorkspaceStatus Status);
internal sealed record JsonRequest(Type Body);

internal sealed class AdministratorCursor(IDataProtectionProvider protection)
{
  private sealed record Payload(Guid Actor, string List, int Limit, Guid After);
  private readonly IDataProtector protector = protection.CreateProtector("VIA.MiniDocs.Administrator.Page.v1");

  internal string Create(Guid actor, string list, int limit, Guid after) =>
      protector.Protect(JsonSerializer.Serialize(new Payload(actor, list, limit, after)));

  internal bool TryRead(HttpRequest request, Guid actor, string list, out int limit, out Guid? after)
  {
    const int defaultLimit = 50;
    limit = defaultLimit;
    after = null;
    var size = request.Query["limit"];
    if (size.Count > 1 || (size.Count == 1 &&
        (!int.TryParse(size[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100)))
      return false;
    var cursor = request.Query["cursor"];
    if (cursor.Count == 0) return true;
    if (cursor.Count != 1 || cursor[0] is not { Length: > 0 and <= 2048 } value) return false;
    try
    {
      var payload = JsonSerializer.Deserialize<Payload>(protector.Unprotect(value));
      if (payload is null || payload.Actor != actor || payload.List != list || payload.Limit != limit)
        return false;
      after = payload.After;
      return true;
    }
    catch (Exception error) when (error is CryptographicException or FormatException or JsonException) { return false; }
  }
}

internal static class AdministrationHttp
{
  internal static void MapD4B(this WebApplication app)
  {
    var routes = app.MapGroup("/api/v1").WithMetadata(new SessionRoute())
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    routes.MapPost("/registration", RegistrationHttp.RegisterAsync).AllowAnonymous()
        .WithMetadata(new ValidateSessionAntiforgery(), new JsonRequest(typeof(RegistrationInput)))
        .Produces<RegistrationResult>(201).ProducesProblem(400).ProducesProblem(403).ProducesProblem(409)
        .ProducesProblem(410).ProducesProblem(413).ProducesProblem(415).ProducesProblem(429).ProducesProblem(503);
    routes.MapGet("/workspace", GetWorkspace).RequireAuthorization(WorkspacePolicies.Session)
        .Produces<WorkspaceResponse>().ProducesProblem(401).ProducesProblem(404).ProducesProblem(503);
    var admin = routes.MapGroup("/admin").RequireAuthorization(WorkspacePolicies.Administrator);
    admin.MapGet("/invitations", ListInvitationsAsync).AdminErrors().Produces<Page<Invite>>().ProducesProblem(400);
    admin.MapGet("/invitations/{id:guid}", GetInvitationAsync).AdminErrors().Produces<Invite>().ProducesProblem(404);
    admin.MapPost("/invitations", CreateInvitationAsync)
        .AdminErrors()
        .WithMetadata(new ValidateSessionAntiforgery(), new JsonRequest(typeof(InvitationCreate)))
        .Produces<Invite>(201).ProducesProblem(400).ProducesProblem(409).ProducesProblem(413)
        .ProducesProblem(415).ProducesProblem(429);
    admin.MapDelete("/invitations/{id:guid}", RevokeInvitationAsync).WithMetadata(new ValidateSessionAntiforgery())
        .AdminErrors().Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
    admin.MapGet("/users", ListUsersAsync).AdminErrors().Produces<Page<AdminUser>>().ProducesProblem(400);
    admin.MapPatch("/users/{id:guid}", PatchUserAsync)
        .AdminErrors()
        .WithMetadata(new ValidateSessionAntiforgery(), new JsonRequest(typeof(AdminUserPatch)))
        .Produces<AdminUser>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409)
        .ProducesProblem(412).ProducesProblem(413).ProducesProblem(415).ProducesProblem(428);
  }

  private static RouteHandlerBuilder AdminErrors(this RouteHandlerBuilder route) =>
      route.ProducesProblem(401).ProducesProblem(403).ProducesProblem(503);

  private static IResult GetWorkspace(HttpContext context)
  {
    var snapshot = (SessionSnapshot)context.Items[typeof(SessionSnapshot)]!;
    return snapshot.WorkspaceId is { } id && SessionHttp.ParseWorkspaceStatus(snapshot.WorkspaceStatus) is { } status
        ? TypedResults.Ok(new WorkspaceResponse(id, status)) : Missing(context);
  }

  private static async Task<IResult> ListInvitationsAsync(HttpContext context, AdministratorInvitations invitations,
      AdministratorCursor cursor, CancellationToken token)
  {
    const string list = "invitations";
    var actor = SessionHttp.ReadAuthority(context.User)!.UserId;
    if (!cursor.TryRead(context.Request, actor, list, out var limit, out var after)) return InvalidCursor(context);
    var rows = await invitations.ReadPageAsync(after, limit + 1, token);
    var next = rows.Count > limit ? cursor.Create(actor, list, limit, rows[limit - 1].Id) : null;
    return TypedResults.Ok(new Page<Invite>(rows.Take(limit).Select(row => ToInvite(row)).ToArray(), next));
  }

  private static async Task<IResult> GetInvitationAsync(Guid id, HttpContext context,
      AdministratorInvitations invitations, CancellationToken token) =>
      await invitations.ReadAsync(id, token) is { } row ? TypedResults.Ok(ToInvite(row)) : Missing(context);

  private static async Task<IResult> CreateInvitationAsync(HttpContext context, AdministratorInvitations invitations,
      InvitationIssueLimits limits, CancellationToken token)
  {
    var (input, error) = await BoundedJson.ReadAsync<InvitationCreate>(context, token);
    if (error is not null) return error;
    if (input?.ExpiresAt is { Kind: not DateTimeKind.Utc }) return BoundedJson.Invalid(context);
    var actor = SessionHttp.ReadAuthority(context.User)!;
    if (!limits.Admit(actor.UserId, out var retryAfter))
    {
      context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
      return SessionHttp.Problem(context, 429, "rate_limited", "Too many invitation requests.");
    }
    var bytes = RandomNumberGenerator.GetBytes(32);
    try
    {
      var result = await invitations.CreateAsync(actor, Guid.NewGuid(), SHA256.HashData(bytes), input?.ExpiresAt, token);
      return result.Invitation is { } row
          ? TypedResults.Created($"/api/v1/admin/invitations/{row.Id:D}", ToInvite(row, WebEncoders.Base64UrlEncode(bytes)))
          : InvitationError(context, result.Status);
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
  }

  private static async Task<IResult> RevokeInvitationAsync(Guid id, HttpContext context,
      AdministratorInvitations invitations, CancellationToken token)
  {
    if (id == Guid.Empty) return BoundedJson.Invalid(context);
    var status = await invitations.RevokeAsync(SessionHttp.ReadAuthority(context.User)!, id, token);
    return status == InvitationWrite.Applied ? TypedResults.NoContent() : InvitationError(context, status);
  }

  private static async Task<IResult> ListUsersAsync(HttpContext context, AdministratorAccounts users,
      AdministratorCursor cursor, CancellationToken token)
  {
    const string list = "users";
    var actor = SessionHttp.ReadAuthority(context.User)!.UserId;
    if (!cursor.TryRead(context.Request, actor, list, out var limit, out var after)) return InvalidCursor(context);
    var rows = await users.ReadPageAsync(after, limit + 1, token);
    var next = rows.Count > limit ? cursor.Create(actor, list, limit, rows[limit - 1].Id) : null;
    return TypedResults.Ok(new Page<AdminUser>(rows.Take(limit).Select(ToAdminUser).ToArray(), next));
  }

  private static async Task<IResult> PatchUserAsync(Guid id, HttpContext context, AdministratorAccounts accounts,
      MiniDocsDbContext db, CancellationToken token)
  {
    if (id == Guid.Empty) return BoundedJson.Invalid(context);
    var values = context.Request.Headers.IfMatch;
    if (values.Count == 0)
      return SessionHttp.Problem(context, 428, "precondition_required", "A strong If-Match value is required.");
    if (values.Count != 1 || !EntityTagHeaderValue.TryParse(values[0], out var tag) ||
        tag.IsWeak || tag == EntityTagHeaderValue.Any) return BoundedJson.Invalid(context);
    var (input, error) = await BoundedJson.ReadAsync<AdminUserPatch>(context, token);
    if (error is not null) return error;
    var status = DeactivationResult.Missing;
    var result = await LocalWriteTransaction.ExecuteAsync(db, async ct =>
    {
      status = await accounts.SetDisabledAsync(SessionHttp.ReadAuthority(context.User)!, id, tag.ToString(), input!.IsDisabled, ct);
      return status == DeactivationResult.Applied ? await accounts.ReadVersionAsync(id, ct) : null;
    }, value => value is not null, cancellationToken: token);
    if (result.Status == LocalWriteStatus.ConcurrencyConflict) return Conflict(context);
    if (result.Value is { } row)
    {
      context.Response.Headers.ETag = row.ETag;
      return TypedResults.Ok(ToAdminUser(row));
    }
    return status switch
    {
      DeactivationResult.Forbidden => Forbidden(context),
      DeactivationResult.Missing => Missing(context),
      DeactivationResult.VersionMismatch => SessionHttp.Problem(context, 412, "version_mismatch", "The resource version changed."),
      _ => Conflict(context)
    };
  }

  private static Invite ToInvite(InvitationView row, string? token = null) =>
      new(row.Id, row.ExpiresAt, row.RedeemedAt, row.RevokedAt, token);

  private static AdminUser ToAdminUser(AdministratorAccountVersion row) =>
      new(row.Id, row.UserName, row.IsDisabled,
          row.AccountKind == AccountKind.AdminOnly ? AdminAccountKind.AdminOnly : AdminAccountKind.Regular, row.Roles, row.ETag);

  private static IResult InvitationError(HttpContext context, InvitationWrite status) => status switch
  {
    InvitationWrite.Forbidden => Forbidden(context),
    InvitationWrite.Missing => Missing(context),
    InvitationWrite.InvalidExpiry => BoundedJson.Invalid(context),
    _ => Conflict(context)
  };

  private static IResult Forbidden(HttpContext ctx) => SessionHttp.Problem(ctx, 403, "forbidden", "Access is denied.");
  private static IResult Missing(HttpContext ctx) => SessionHttp.Problem(ctx, 404, "resource_not_found", "Resource not found.");
  private static IResult Conflict(HttpContext ctx) => SessionHttp.Problem(ctx, 409, "state_conflict", "The resource state changed.");
  private static IResult InvalidCursor(HttpContext ctx) => SessionHttp.Problem(ctx, 400, "cursor_invalid", "Invalid page parameters.");
}

internal sealed class InvitationIssueLimits(TimeProvider clock)
{
  private readonly LoginRateLimits limits = new(clock);
  internal bool Admit(Guid actor, out int retryAfter)
  {
    var id = actor.ToString("D");
    return limits.TryAcquire(id, id, out retryAfter);
  }
}
