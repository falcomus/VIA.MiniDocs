using Microsoft.EntityFrameworkCore;

namespace VIA.MiniDocs.Infrastructure.Identity;

public sealed record SessionAuthority(Guid UserId, byte[] HandleHash, string Stamp);

public sealed class SessionSnapshot
{
  public Guid UserId { get; set; }
  public string UserName { get; set; } = "";
  public string[] Roles { get; set; } = [];
  public Guid? WorkspaceId { get; set; }
  public string? WorkspaceStatus { get; set; }
  public bool RenewalDue { get; set; }
}

public sealed class SessionStore(MiniDocsDbContext db)
{
  public async Task<SessionSnapshot?> CreateAsync(SessionAuthority session, SessionAuthority? previous,
      TimeSpan idle, TimeSpan absolute, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    var created = await db.Database.SqlQuery<bool>($"""
        SELECT minidocs.command_create_session(
          {session.HandleHash}, {session.UserId}, {session.Stamp}, {idle}, {absolute}) AS "Value"
        """).SingleAsync(cancellationToken);
    if (!created) return null;

    var snapshot = await ReadAsync(session, idle, cancellationToken);
    if (snapshot is null) return null;
    if (previous is not null) await RevokeAsync(previous, cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return snapshot;
  }

  public Task<SessionSnapshot?> ReadAsync(SessionAuthority session, TimeSpan idle, CancellationToken cancellationToken) =>
      db.Database.SqlQuery<SessionSnapshot>($"""
          SELECT u.id AS "UserId", u.user_name AS "UserName", ARRAY(
            SELECT r.normalized_name::text
            FROM minidocs.identity_user_roles ur JOIN minidocs.identity_roles r ON r.id = ur.role_id
            WHERE ur.user_id = u.id AND r.normalized_name IS NOT NULL ORDER BY r.normalized_name
          ) AS "Roles", w.id AS "WorkspaceId", w.status::text AS "WorkspaceStatus",
          (s.absolute_until > s.idle_until AND
            s.idle_until <= statement_timestamp() + {idle} / 2) AS "RenewalDue"
          FROM minidocs.identity_sessions s
          JOIN minidocs.identity_users u ON u.id = s.user_id
          LEFT JOIN minidocs.workspaces w ON w.owner_user_id = u.id AND u.account_kind = 'regular'
          WHERE s.handle_hash = {session.HandleHash} AND s.user_id = {session.UserId}
            AND s.security_stamp = {session.Stamp} AND u.security_stamp = s.security_stamp
            AND NOT u.is_disabled AND u.user_name IS NOT NULL AND s.revoked_at IS NULL
            AND s.idle_until > statement_timestamp()
          """).SingleOrDefaultAsync(cancellationToken);

  public Task<bool> RenewAsync(SessionAuthority session, TimeSpan idle, CancellationToken cancellationToken) =>
      db.Database.SqlQuery<bool>($"""
          SELECT minidocs.command_renew_session({session.HandleHash}, {session.UserId}, {idle}) AS "Value"
          """).SingleAsync(cancellationToken);

  public async Task RevokeAsync(SessionAuthority session, CancellationToken cancellationToken) =>
      _ = await db.Database.SqlQuery<bool>($"""
          SELECT minidocs.command_revoke_session({session.HandleHash}, {session.UserId}) AS "Value"
          """).SingleAsync(cancellationToken);

  public Task<int> CleanupAsync(int batch, CancellationToken cancellationToken) =>
      db.Database.SqlQuery<int>($"""
          SELECT minidocs.command_cleanup_sessions({batch}) AS "Value"
          """).SingleAsync(cancellationToken);
}
