using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed class ProvisioningSnapshot
{
  public Guid WorkspaceId { get; set; }
  public string WorkspaceStatus { get; set; } = "";
  public bool OwnerDisabled { get; set; }
  public Guid? OperationId { get; set; }
  public string? Status { get; set; }
  public long Version { get; set; }
  public int AttemptNo { get; set; }
  public DateTime? LeaseUntil { get; set; }
  public Guid? LeaseOwner { get; set; }
  public double? LeaseSeconds { get; set; }
  public Guid? Candidate { get; set; }
  public Guid? AccountId { get; set; }
  public string? Name { get; set; }
  public Guid? ApplicationId { get; set; }
  public DateTime? VerifiedAt { get; set; }
  public long Generation { get; set; }
  public byte[]? Secret { get; set; }
  public byte[]? Invitation { get; set; }

  internal bool Pending => WorkspaceStatus == "pending";
  internal bool RecoveryWorkspace => WorkspaceStatus is "pending" or "suspended";
  internal bool Unverified => AccountId is not null && ApplicationId is null && VerifiedAt is null;
  internal bool Ready => Pending && !OwnerDisabled && Status == "ready" && AttemptNo == 0 &&
      Candidate is null && LeaseOwner is null && LeaseUntil is null && Unverified;
  internal bool Recovery => !OwnerDisabled && RecoveryWorkspace && Status == "recovery_required" && Unverified;
  internal bool Bound => !OwnerDisabled && WorkspaceStatus == "active" && Status == "active" &&
      ApplicationId is not null && ApplicationId == Candidate && VerifiedAt is not null;
  internal bool Owns(OperationFence fence, double reserve) =>
      !OwnerDisabled && LeaseOwner == fence.Owner && Version == fence.Version &&
      LeaseSeconds > reserve && Unverified &&
      (Status == "registering" && Pending || Recovery);
  internal ViaDocsPreparedIdentity Identity() => new(WorkspaceId, Name!, new(AccountId!.Value));
}

internal sealed class ProvisioningReadStore(MiniDocsDbContext db)
{
  private IQueryable<ProvisioningSnapshot> Rows() => db.Database.SqlQueryRaw<ProvisioningSnapshot>("""
      SELECT w.id AS "WorkspaceId", w.status AS "WorkspaceStatus", u.is_disabled AS "OwnerDisabled",
        op.id AS "OperationId", op.status AS "Status", coalesce(op.version, 0) AS "Version",
        coalesce(op.attempt_no, 0) AS "AttemptNo", op.lease_until AS "LeaseUntil", op.lease_owner AS "LeaseOwner",
        EXTRACT(EPOCH FROM (op.lease_until - clock_timestamp()))::double precision AS "LeaseSeconds",
        op.candidate_application_id AS "Candidate", a.id AS "AccountId", a.application_name AS "Name",
        a.application_id AS "ApplicationId", a.verified_at AS "VerifiedAt",
        coalesce(a.credential_generation, 0) AS "Generation", a.application_secret_protected AS "Secret",
        op.invitation_protected AS "Invitation"
      FROM minidocs.workspaces w
      JOIN minidocs.identity_users u ON u.id = w.owner_user_id AND u.account_kind = 'regular'
      LEFT JOIN minidocs.via_docs_accounts a ON a.workspace_id = w.id
      LEFT JOIN minidocs.provisioning_operations op ON op.workspace_id = w.id AND op.status <> 'cancelled'
      """);

  internal Task<ProvisioningSnapshot?> ReadAsync(Guid workspace, CancellationToken token) =>
      Rows().Where(row => row.WorkspaceId == workspace).SingleOrDefaultAsync(token);

  internal Task<ProvisioningSnapshot?> ReadCredentialAsync(ViaDocsPreparedIdentity identity, CancellationToken token) =>
      Rows().Where(row => row.WorkspaceId == identity.WorkspaceId && row.AccountId == identity.Credential.Value &&
          row.Name == identity.Name && row.Generation > 0 && !row.OwnerDisabled).SingleOrDefaultAsync(token);

  internal Task<List<ProvisioningSnapshot>> PollAsync(Guid? after, int batch, bool dispatch, CancellationToken token)
  {
    var rows = Rows().Where(row =>
        row.Status == "registering" && row.LeaseSeconds <= 0 ||
        !row.OwnerDisabled && (row.Status == "recovery_required" &&
            (row.WorkspaceStatus == "pending" || row.WorkspaceStatus == "suspended") &&
            (row.LeaseUntil == null || row.LeaseSeconds <= 0) ||
          dispatch && row.Status == "ready" && row.WorkspaceStatus == "pending"));
    if (after is { } id) rows = rows.Where(row => row.WorkspaceId.CompareTo(id) > 0);
    return rows.OrderBy(row => row.WorkspaceId).Take(batch).ToListAsync(token);
  }

  internal Task<List<ProvisioningSnapshot>> InventoryAsync(Guid? after, int count, CancellationToken token)
  {
    var rows = Rows().Where(row => row.AccountId != null);
    if (after is { } id) rows = rows.Where(row => row.WorkspaceId.CompareTo(id) > 0);
    return rows.OrderBy(row => row.WorkspaceId).Take(count).ToListAsync(token);
  }

  internal static string TechnicalName(Guid workspace) => $"minidocs-{workspace:N}";
}
