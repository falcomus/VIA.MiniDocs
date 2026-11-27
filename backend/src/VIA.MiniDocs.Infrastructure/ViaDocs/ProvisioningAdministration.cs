using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VIA.MiniDocs.Infrastructure.Identity;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

public enum ProvisioningAction { Verify, RetryIfSafe }
public enum ProvisioningDecision { Accepted, Missing, Forbidden, Conflict, Uncertain, Unavailable }

public sealed class ProvisioningView
{
  public Guid WorkspaceId { get; set; }
  public string State { get; set; } = "";
  public DateTime UpdatedAt { get; set; }
}

public sealed record ProvisioningAcceptance(ProvisioningDecision Decision, ProvisioningView? View = null, int RetryAfter = 0);

public sealed class ProvisioningAdministration
{
  private readonly MiniDocsDbContext db;
  private readonly ProvisioningReadStore store;
  private readonly ProvisioningProtection protection;
  private readonly ProvisioningAvailability availability;
  private readonly IOptions<ProvisioningOptions> options;
  private readonly IOptions<ViaDocsOptions> client;
  private readonly ViaDocsAuthTransport transport;

  internal ProvisioningAdministration(MiniDocsDbContext db, ProvisioningReadStore store, ProvisioningProtection protection,
      ProvisioningAvailability availability, IOptions<ProvisioningOptions> options, IOptions<ViaDocsOptions> client,
      ViaDocsAuthTransport transport)
  {
    this.db = db;
    this.store = store;
    this.protection = protection;
    this.availability = availability;
    this.options = options;
    this.client = client;
    this.transport = transport;
  }

  private bool CanVerify => options.Value.Enabled && client.Value.Enabled && availability.Inventoried;
  private bool CanDispatch => CanVerify && client.Value.InvitationsEnabled && !transport.InvitationUnavailable;

  public Task<List<ProvisioningView>> ListAsync(Guid? workspace, Guid? after, int count, CancellationToken token)
  {
    var rows = Views();
    if (workspace is { } id) rows = rows.Where(row => row.WorkspaceId == id);
    if (after is { } cursor) rows = rows.Where(row => row.WorkspaceId.CompareTo(cursor) > 0);
    return rows.OrderBy(row => row.WorkspaceId).Take(count).ToListAsync(token);
  }

  public async Task<ProvisioningAcceptance> SupplyAsync(SessionAuthority actor, Guid workspace, string tokenValue,
      CancellationToken token)
  {
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
    deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TransactionSeconds));
    try
    {
      var ct = deadline.Token;
      var previous = await store.ReadAsync(workspace, ct);
      if (previous is null) return new(ProvisioningDecision.Missing);
      var operation = previous.OperationId ?? Guid.NewGuid();
      var account = previous.AccountId ?? Guid.NewGuid();
      var secret = previous.OperationId is null && previous.AccountId is null
          ? protection.CreateCredential(workspace, account, 1) : null;
      var invitation = protection.ProtectInvitation(workspace, operation, tokenValue);
      return await MutateAsync(actor, workspace, async (row, t) =>
      {
        if (!CanDispatch) return ProvisioningDecision.Unavailable;
        if (row.OperationId != previous.OperationId) return ProvisioningDecision.Conflict;
        if (!row.Pending || row.OwnerDisabled) return ProvisioningDecision.Conflict;
        if (row.OperationId is null && row.AccountId is null && secret is not null)
        {
          var prepared = await ProvisioningPersistence.TryPrepareAsync(db, workspace, account, operation,
              ProvisioningReadStore.TechnicalName(workspace), secret, invitation, t);
          return prepared ? ProvisioningDecision.Accepted : ProvisioningDecision.Conflict;
        }
        if (row.Status is "registering" or "recovery_required") return ProvisioningDecision.Uncertain;
        if (!row.Ready || row.AccountId != account || row.Version != previous.Version || !HasCredential(row))
          return ProvisioningDecision.Conflict;
        return await ProvisioningPersistence.TryReplaceInvitationAsync(db, workspace, operation, previous.Version, invitation, t)
            ? ProvisioningDecision.Accepted : ProvisioningDecision.Conflict;
      }, ct);
    }
    catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(ProvisioningDecision.Unavailable); }
    catch (CryptographicException) { return new(ProvisioningDecision.Unavailable); }
  }

  public async Task<ProvisioningAcceptance> RecoverAsync(SessionAuthority actor, Guid workspace, ProvisioningAction action,
      CancellationToken token)
  {
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
    deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TransactionSeconds));
    try
    {
      return await MutateAsync(actor, workspace, (row, _) => Task.FromResult(CheckRecovery(row, action)), deadline.Token);
    }
    catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(ProvisioningDecision.Unavailable); }
    catch (CryptographicException) { return new(ProvisioningDecision.Unavailable); }
  }

  private ProvisioningDecision CheckRecovery(ProvisioningSnapshot row, ProvisioningAction action)
  {
    if (row.OperationId is null) return ProvisioningDecision.Missing;
    if (action == ProvisioningAction.RetryIfSafe && row.Status is "registering" or "recovery_required")
      return ProvisioningDecision.Uncertain;
    if (action == ProvisioningAction.Verify && row.Status == "registering") return ProvisioningDecision.Uncertain;
    if (!(action == ProvisioningAction.Verify ? row.Recovery : row.Ready))
      return ProvisioningDecision.Conflict;
    if (!(action == ProvisioningAction.Verify ? CanVerify : CanDispatch) || !HasCredential(row) ||
        (action == ProvisioningAction.RetryIfSafe && !ProvisioningProtection.HasInvitationFormat(row.Invitation)))
      return ProvisioningDecision.Unavailable;
    return ProvisioningDecision.Accepted;
  }

  private bool HasCredential(ProvisioningSnapshot row)
  {
    if (row.AccountId is not { } account || row.Generation <= 0 || row.Secret is null ||
        row.Name != ProvisioningReadStore.TechnicalName(row.WorkspaceId)) return false;
    _ = protection.ReadCredential(row.WorkspaceId, account, row.Generation, row.Secret);
    return true;
  }

  private async Task<ProvisioningAcceptance> MutateAsync(SessionAuthority actor, Guid workspace,
      Func<ProvisioningSnapshot, CancellationToken, Task<ProvisioningDecision>> action, CancellationToken token)
  {
    var result = new ProvisioningAcceptance(ProvisioningDecision.Conflict);
    var write = await LocalWriteTransaction.ExecuteAsync(db, async ct =>
    {
      var owner = await db.Workspaces.Where(w => w.Id == workspace).Select(w => (Guid?)w.OwnerUserId).SingleOrDefaultAsync(ct);
      if (owner is null) return result = new(ProvisioningDecision.Missing);
      if (!await db.Database.SqlQuery<bool>($"""
          SELECT minidocs.lock_admin_authority(
            {actor.UserId}, {actor.Stamp}, {actor.HandleHash}, {owner.Value}, {workspace}) AS "Value"
          """).SingleAsync(ct))
        return result = new(ProvisioningDecision.Forbidden);

      var row = await store.ReadAsync(workspace, ct);
      if (row is null) return result = new(ProvisioningDecision.Missing);
      var status = await action(row, ct);
      if (status != ProvisioningDecision.Accepted) return result = new(status);
      var view = await Views().SingleAsync(item => item.WorkspaceId == workspace, ct);
      var remaining = (int)Math.Clamp(Math.Ceiling(row.LeaseSeconds ?? 0), 0, D2LeasePolicy.MaximumSeconds);
      return result = new(status, view, Math.Max(options.Value.PollSeconds, remaining));
    }, response => response.Decision == ProvisioningDecision.Accepted, cancellationToken: token);
    return write.Status == LocalWriteStatus.ConcurrencyConflict ? new(ProvisioningDecision.Conflict) : result;
  }

  private IQueryable<ProvisioningView> Views() => db.Database.SqlQueryRaw<ProvisioningView>("""
      SELECT w.id AS "WorkspaceId", coalesce(op.status, 'pending_invitation') AS "State",
        coalesce(op.updated_at, w.updated_at) AS "UpdatedAt"
      FROM minidocs.workspaces w
      JOIN minidocs.identity_users u ON u.id = w.owner_user_id AND u.account_kind = 'regular'
      LEFT JOIN LATERAL (
        SELECT id, status, updated_at FROM minidocs.provisioning_operations
        WHERE workspace_id = w.id
        ORDER BY (status = 'cancelled'), updated_at DESC, id
        LIMIT 1
      ) op ON true
      WHERE op.id IS NOT NULL OR (w.status = 'pending' AND NOT u.is_disabled AND NOT EXISTS (
        SELECT 1 FROM minidocs.via_docs_accounts a WHERE a.workspace_id = w.id))
      """);
}
