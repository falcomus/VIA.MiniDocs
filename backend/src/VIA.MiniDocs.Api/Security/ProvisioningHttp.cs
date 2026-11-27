using System.Globalization;
using System.Text.Json.Serialization;
using VIA.MiniDocs.Infrastructure.ViaDocs;

namespace VIA.MiniDocs.Api.Security;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record TechnicalInvitation(string Token)
{
  public override string ToString() => "[technical invitation]";
}

[JsonConverter(typeof(RecoveryActionConverter))]
internal enum RecoveryAction
{
  [JsonStringEnumMemberName("verify")] Verify,
  [JsonStringEnumMemberName("retry_if_safe")] RetryIfSafe
}

internal sealed class RecoveryActionConverter() : JsonStringEnumConverter<RecoveryAction>(namingPolicy: null, allowIntegerValues: false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Recovery([property: JsonRequired] RecoveryAction Action);

internal enum ProvisioningState
{
  [JsonStringEnumMemberName("pending_invitation")] PendingInvitation,
  [JsonStringEnumMemberName("ready")] Ready,
  [JsonStringEnumMemberName("registering")] Registering,
  [JsonStringEnumMemberName("recovery_required")] RecoveryRequired,
  [JsonStringEnumMemberName("active")] Active,
  [JsonStringEnumMemberName("cancelled")] Cancelled
}

internal sealed record Provisioning(Guid WorkspaceId, ProvisioningState State, DateTime UpdatedAt);

internal static class ProvisioningHttp
{
  private const string StatusPath = "/api/v1/admin/provisioning";

  internal static void MapD4C(this WebApplication app)
  {
    var admin = app.MapGroup("/api/v1/admin").WithMetadata(new SessionRoute())
        .RequireAuthorization(WorkspacePolicies.Administrator);
    admin.MapGet("/provisioning", ListAsync).Produces<Page<Provisioning>>()
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503);
    admin.MapPost("/workspaces/{id:guid}/technical-invitation", SupplyAsync)
        .WithMetadata(new ValidateSessionAntiforgery(), new JsonRequest(typeof(TechnicalInvitation)))
        .Produces<Provisioning>(202).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
        .ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415)
        .ProducesProblem(429).ProducesProblem(503);
    admin.MapPost("/workspaces/{id:guid}/recovery", RecoverAsync)
        .WithMetadata(new ValidateSessionAntiforgery(), new JsonRequest(typeof(Recovery)))
        .Produces<Provisioning>(202).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
        .ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415).ProducesProblem(503);
  }

  private static async Task<IResult> ListAsync(HttpContext context, AdministratorCursor cursor,
      ProvisioningAdministration service, CancellationToken token)
  {
    Guid? workspace = null;
    var filter = context.Request.Query["workspaceId"];
    if (filter.Count > 0)
    {
      if (filter.Count != 1 || !Guid.TryParseExact(filter[0], "D", out var id) || id == Guid.Empty)
        return BoundedJson.Invalid(context);
      workspace = id;
    }
    var actor = SessionHttp.ReadAuthority(context.User)!.UserId;
    var list = workspace is { } key ? $"provisioning/{key:D}" : "provisioning";
    if (!cursor.TryRead(context.Request, actor, list, out var limit, out var after))
      return SessionHttp.Problem(context, 400, "cursor_invalid", "Invalid page parameters.");
    var rows = await service.ListAsync(workspace, after, limit + 1, token);
    var next = rows.Count > limit ? cursor.Create(actor, list, limit, rows[limit - 1].WorkspaceId) : null;
    return TypedResults.Ok(new Page<Provisioning>(rows.Take(limit).Select(ToResponse).ToArray(), next));
  }

  private static async Task<IResult> SupplyAsync(Guid id, HttpContext context,
      ProvisioningAdministration service, InvitationIssueLimits limits, CancellationToken token)
  {
    var (input, error) = await BoundedJson.ReadAsync<TechnicalInvitation>(context, token);
    if (error is not null) return error;
    if (id == Guid.Empty || !ProvisioningProtection.IsInvitation(input?.Token))
      return BoundedJson.Invalid(context);
    var actor = SessionHttp.ReadAuthority(context.User)!;
    if (!limits.Admit(actor.UserId, out var retryAfter))
    {
      context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
      return SessionHttp.Problem(context, 429, "rate_limited", "Too many invitation requests.");
    }
    return ToResult(context, await service.SupplyAsync(actor, id, input!.Token, token));
  }

  private static async Task<IResult> RecoverAsync(Guid id, HttpContext context,
      ProvisioningAdministration service, CancellationToken token)
  {
    var (input, error) = await BoundedJson.ReadAsync<Recovery>(context, token);
    if (error is not null) return error;
    if (id == Guid.Empty || input is null) return BoundedJson.Invalid(context);
    var action = input.Action == RecoveryAction.Verify ? ProvisioningAction.Verify : ProvisioningAction.RetryIfSafe;
    return ToResult(context, await service.RecoverAsync(SessionHttp.ReadAuthority(context.User)!, id, action, token));
  }

  private static IResult ToResult(HttpContext context, ProvisioningAcceptance result)
  {
    if (result.Decision == ProvisioningDecision.Accepted && result.View is { } view)
    {
      context.Response.Headers.RetryAfter = result.RetryAfter.ToString(CultureInfo.InvariantCulture);
      return TypedResults.Accepted($"{StatusPath}?workspaceId={view.WorkspaceId:D}", ToResponse(view));
    }
    return result.Decision switch
    {
      ProvisioningDecision.Missing => SessionHttp.Problem(context, 404, "resource_not_found", "Resource not found."),
      ProvisioningDecision.Forbidden => SessionHttp.Problem(context, 403, "forbidden", "Access is denied."),
      ProvisioningDecision.Unavailable => SessionHttp.Problem(context, 503, "dependency_unavailable", "Provisioning is unavailable."),
      ProvisioningDecision.Uncertain => SessionHttp.Problem(context, 409, "operation_uncertain", "The operation requires verification."),
      _ => SessionHttp.Problem(context, 409, "state_conflict", "The resource state changed.")
    };
  }

  private static Provisioning ToResponse(ProvisioningView row) => new(row.WorkspaceId, row.State switch
  {
    "pending_invitation" => ProvisioningState.PendingInvitation,
    "ready" => ProvisioningState.Ready,
    "registering" => ProvisioningState.Registering,
    "recovery_required" => ProvisioningState.RecoveryRequired,
    "active" => ProvisioningState.Active,
    "cancelled" => ProvisioningState.Cancelled,
    _ => throw new InvalidOperationException("Unknown technical provisioning status.")
  }, row.UpdatedAt);
}
