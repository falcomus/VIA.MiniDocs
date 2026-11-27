namespace VIA.MiniDocs.Domain;

public abstract class WorkspaceRecord
{
  public Guid Id { get; private set; }
  public Guid WorkspaceId { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }
  public long Version { get; private set; } = 1;
}

/// <summary>An invitation is global until it is redeemed and therefore has no workspace key.</summary>
public sealed class UserInvitation
{
  public Guid Id { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }
  public long Version { get; private set; } = 1;
  public byte[] TokenHash { get; private set; } = [];
  public Guid CreatedBy { get; set; }
  public DateTime ExpiresAt { get; set; }
  public DateTime? RevokedAt { get; private set; }
  public DateTime? RedeemedAt { get; private set; }
  public Guid? RedeemedUserId { get; private set; }
}

public sealed class ViaDocsAccount : WorkspaceRecord
{
  public long CredentialGeneration { get; private set; } = 1;
  public string ApplicationName { get; private set; } = "";
  public byte[] ProtectedSecret { get; private set; } = [];
  public Guid? ApplicationId { get; private set; }
  public DateTime? VerifiedAt { get; private set; }
}

public sealed class ProvisioningOperation : WorkspaceRecord
{
  public ProvisioningStatus Status { get; private set; } = ProvisioningStatus.PendingInvitation;
  public byte[]? ProtectedInvitation { get; private set; }
  public Guid? CandidateApplicationId { get; private set; }
  public int AttemptNo { get; private set; }
  public ProvisioningFailureCategory? FailureCategory { get; private set; }
  public Guid? LeaseOwner { get; private set; }
  public DateTime? LeaseUntil { get; private set; }
}

public sealed class Topic : WorkspaceRecord
{
  public Guid? ParentId { get; set; }
  public string Name { get; set; } = "";
  public TopicSystemKey? SystemKey { get; set; }
}

public sealed class Document : WorkspaceRecord
{
  public Guid TopicId { get; private set; }
  public Guid? ViaFileId { get; private set; }
  public string Title { get; set; } = "";
  public string? FileName { get; set; }
  public DocumentKind Kind { get; set; } = DocumentKind.Meeting;
  public DateOnly? DocumentDate { get; set; }
  public int? PageCount { get; private set; }
  public PageCountSource? PageCountSource { get; private set; }
  public DocumentAvailability Availability { get; private set; } = DocumentAvailability.PendingUpload;
  public DateTime? DeletedAt { get; private set; }
  public bool TranscriptPresent { get; private set; }
  public bool TranscriptEdited { get; private set; }
  public long TranscriptRevision { get; private set; }
}

public sealed class Tag : WorkspaceRecord
{
  public string Name { get; set; } = "";
  public string NameKey { get; private set; } = "";
  public string Description { get; set; } = "";
  public string Color { get; set; } = "#000000";
}

public sealed class DocumentTag
{
  public Guid WorkspaceId { get; private set; }
  public Guid DocumentId { get; private set; }
  public Guid TagId { get; set; }
}

public sealed class TranscriptPage
{
  public Guid WorkspaceId { get; private set; }
  public Guid DocumentId { get; set; }
  public int PageNo { get; private set; }
  public string Text { get; set; } = "";
  public bool Edited { get; set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }
  public long Version { get; private set; } = 1;
}

public sealed class PendingTranscript : WorkspaceRecord
{
  public string FileName { get; set; } = "";
  public string? SourceName { get; set; }
  public string? MetaDate { get; set; }
  public string? MetaTopic { get; set; }
  public string? MetaTitle { get; set; }
  public string? MetaTags { get; set; }
  public DateTime ImportedAt { get; set; }
  public PendingTranscriptStatus Status { get; private set; } = PendingTranscriptStatus.Pending;
}

public sealed class PendingTranscriptPage
{
  public Guid WorkspaceId { get; private set; }
  public Guid PendingId { get; private set; }
  public int PageNo { get; private set; }
  public string Text { get; set; } = "";
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }
  public long Version { get; private set; } = 1;
}

public sealed class FileOperation : WorkspaceRecord
{
  public Guid DocumentId { get; private set; }
  public FileOperationKind Kind { get; private set; } = FileOperationKind.Upload;
  public FileOperationState State { get; private set; } = FileOperationState.Prepared;
  public Guid? RemoteFileId { get; private set; }
  public FileOperationFailureCategory? FailureCategory { get; private set; }
  public int AttemptNo { get; private set; }
  public Guid? LeaseOwner { get; private set; }
  public DateTime? LeaseUntil { get; private set; }
  public Guid? RequestId { get; private set; }
  public long? VerifiedSize { get; private set; }
  public byte[]? VerifiedSha256 { get; private set; }
  public DateTime? CompletedAt { get; private set; }
}

/// <summary>Minimal permanent request reservation; no document metadata is retained.</summary>
public sealed class UploadRequestTombstone
{
  public Guid WorkspaceId { get; set; }
  public Guid RequestId { get; set; }
}

public sealed class FileOperationReceipt
{
  public Guid Id { get; private set; }
  public Guid WorkspaceId { get; private set; }
  public Guid OperationId { get; private set; }
  public FileOperationKind Kind { get; private set; } = FileOperationKind.Delete;
  public FileOperationOutcome Outcome { get; private set; } = FileOperationOutcome.RemoteDeleted;
  public DateTime CompletedAt { get; private set; }
}
