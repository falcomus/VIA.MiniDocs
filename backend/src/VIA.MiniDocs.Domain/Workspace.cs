namespace VIA.MiniDocs.Domain;

public enum AccountKind
{
  Regular,
  AdminOnly
}

public enum WorkspaceStatus
{
  Pending,
  Active,
  Suspended
}

/// <summary>A private workspace owned by a regular account.</summary>
public sealed class Workspace
{
  private Workspace() { }

  public Workspace(Guid id, Guid ownerUserId)
  {
    if (id == Guid.Empty) throw new ArgumentException("Workspace ID is required.", nameof(id));
    if (ownerUserId == Guid.Empty) throw new ArgumentException("Owner ID is required.", nameof(ownerUserId));
    Id = id;
    OwnerUserId = ownerUserId;
  }

  public Guid Id { get; private set; }
  public Guid OwnerUserId { get; private set; }
  public WorkspaceStatus Status { get; private set; } = WorkspaceStatus.Pending;
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }
  public long Version { get; private set; } = 1;
}
