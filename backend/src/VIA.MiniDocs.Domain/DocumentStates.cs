namespace VIA.MiniDocs.Domain;

public enum ProvisioningStatus
{
  PendingInvitation,
  Ready,
  Registering,
  RecoveryRequired,
  Active,
  Cancelled
}

public enum ProvisioningFailureCategory
{
  InvitationExpired,
  Authentication,
  TransportUnknown,
  Conflict,
  Configuration,
  ExternalUnavailable,
  VerificationFailed,
  ManualReview
}

public enum TopicSystemKey
{
  Unassigned
}

public enum DocumentKind
{
  Meeting,
  Note
}

public enum PageCountSource
{
  PdfImport,
  Viewer,
  ViaDocsVerified
}

public enum DocumentAvailability
{
  PendingUpload,
  UploadFailed,
  Available,
  Missing,
  DeletePending
}

public enum PendingTranscriptStatus
{
  Pending,
  Matched,
  Discarded
}

public enum FileOperationKind
{
  Upload,
  Delete
}

public enum FileOperationState
{
  Prepared,
  InFlight,
  RemoteKnown,
  Unknown,
  DeleteInFlight,
  DeleteUnknown,
  Completed,
  Failed
}

public enum FileOperationOutcome
{
  RemoteDeleted
}

public enum FileOperationFailureCategory
{
  NotSent,
  TransportUnknown,
  VerificationFailed,
  ManualReview
}
