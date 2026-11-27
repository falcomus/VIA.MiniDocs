using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure.Identity;

public sealed record InvitationView(Guid Id, DateTime ExpiresAt, DateTime? RedeemedAt, DateTime? RevokedAt, long Version);
public enum InvitationWrite { Applied, Forbidden, Missing, Conflict, InvalidExpiry }

public sealed class AdministratorInvitations(MiniDocsDbContext db)
{
  private static IQueryable<InvitationView> Project(IQueryable<UserInvitation> query) => query
      .Select(row => new InvitationView(row.Id, row.ExpiresAt, row.RedeemedAt, row.RevokedAt, row.Version));

  public Task<InvitationView?> ReadAsync(Guid id, CancellationToken token) =>
      Project(db.UserInvitations.AsNoTracking().Where(row => row.Id == id)).SingleOrDefaultAsync(token);

  public Task<List<InvitationView>> ReadPageAsync(Guid? after, int count, CancellationToken token)
  {
    var query = db.UserInvitations.AsNoTracking();
    if (after is { } id) query = query.Where(row => row.Id.CompareTo(id) > 0);
    return Project(query.OrderBy(row => row.Id).Take(count)).ToListAsync(token);
  }

  public async Task<(InvitationWrite Status, InvitationView? Invitation)> CreateAsync(
      SessionAuthority actor, Guid id, byte[] hash, DateTime? expiresAt, CancellationToken token)
  {
    var status = InvitationWrite.Conflict;
    InvitationView? created = null;
    var result = await LocalWriteTransaction.ExecuteAsync(db, async ct =>
    {
      if (!await LockAuthorityAsync(actor, ct)) return status = InvitationWrite.Forbidden;
      var now = await db.Database.SqlQueryRaw<DateTime>("""SELECT clock_timestamp() AS "Value" """).SingleAsync(ct);
      var expiry = expiresAt ?? now.AddHours(24);
      if (expiry <= now || expiry > now.AddDays(30)) return status = InvitationWrite.InvalidExpiry;
      try
      {
        if (!await InvitationPersistence.TryCreateAsync(db, id, hash, actor.UserId, expiry, ct))
          return status = InvitationWrite.Conflict;
      }
      catch (ArgumentException) when (id != Guid.Empty && actor.UserId != Guid.Empty && hash.Length == 32)
      {
        // PostgreSQL rechecks expiry at the time of insert.
        return status = InvitationWrite.InvalidExpiry;
      }
      created = await ReadAsync(id, ct) ?? throw new InvalidOperationException("Created invitation was not visible.");
      return status = InvitationWrite.Applied;
    }, value => value == InvitationWrite.Applied, cancellationToken: token);
    return result.Status == LocalWriteStatus.Committed ? (status, created) :
        (result.Status == LocalWriteStatus.ConcurrencyConflict ? InvitationWrite.Conflict : status, null);
  }

  public async Task<InvitationWrite> RevokeAsync(SessionAuthority actor, Guid id, CancellationToken token)
  {
    var status = InvitationWrite.Conflict;
    var result = await LocalWriteTransaction.ExecuteAsync(db, async ct =>
    {
      if (!await LockAuthorityAsync(actor, ct)) return status = InvitationWrite.Forbidden;
      var current = await ReadAsync(id, ct);
      if (current is null) return status = InvitationWrite.Missing;
      return status = await InvitationPersistence.TryRevokeAsync(db, id, current.Version, actor.UserId, ct)
          ? InvitationWrite.Applied : InvitationWrite.Conflict;
    }, value => value == InvitationWrite.Applied, cancellationToken: token);
    return result.Status == LocalWriteStatus.ConcurrencyConflict ? InvitationWrite.Conflict : status;
  }

  private Task<bool> LockAuthorityAsync(SessionAuthority actor, CancellationToken token) =>
      db.Database.SqlQuery<bool>($"""
          SELECT minidocs.lock_admin_authority(
            {actor.UserId}, {actor.Stamp}, {actor.HandleHash}, NULL::uuid, NULL::uuid) AS "Value"
          """).SingleAsync(token);
}
