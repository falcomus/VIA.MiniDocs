using Microsoft.AspNetCore.Authorization;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;

namespace VIA.MiniDocs.Api.Security;

internal sealed class ActiveWorkspaceRequirement : IAuthorizationRequirement;

internal sealed class ActiveWorkspaceHandler(MiniDocsDbContext db) : AuthorizationHandler<ActiveWorkspaceRequirement>
{
  protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveWorkspaceRequirement requirement)
  {
    if (context.Resource is HttpContext http &&
        http.Items[typeof(SessionSnapshot)] is SessionSnapshot { WorkspaceId: { } workspace, WorkspaceStatus: "active" } session &&
        SessionHttp.ReadAuthority(context.User)?.UserId == session.UserId)
    {
      db.BindActiveWorkspace(workspace);
      context.Succeed(requirement);
    }
    return Task.CompletedTask;
  }
}

internal static class WorkspacePolicies
{
  internal const string Session = "Session";
  internal const string Administrator = "Administrator";
  internal const string ActiveWorkspace = "ActiveWorkspace";
}
