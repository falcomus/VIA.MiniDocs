using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VIA.MiniDocs.Domain;
using Domain = VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

/// <summary>Stable PostgreSQL text contracts, independent of enum member names.</summary>
internal static class DomainTextConverters
{
  public static readonly ValueConverter<AccountKind, string> AccountKind = Create(
      (Domain.AccountKind.Regular, "regular"),
      (Domain.AccountKind.AdminOnly, "admin_only"));

  public static readonly ValueConverter<WorkspaceStatus, string> WorkspaceStatus = Create(
      (Domain.WorkspaceStatus.Pending, "pending"),
      (Domain.WorkspaceStatus.Active, "active"),
      (Domain.WorkspaceStatus.Suspended, "suspended"));

  public static readonly ValueConverter<ProvisioningStatus, string> ProvisioningStatus = Create(
      (Domain.ProvisioningStatus.PendingInvitation, "pending_invitation"),
      (Domain.ProvisioningStatus.Ready, "ready"),
      (Domain.ProvisioningStatus.Registering, "registering"),
      (Domain.ProvisioningStatus.RecoveryRequired, "recovery_required"),
      (Domain.ProvisioningStatus.Active, "active"),
      (Domain.ProvisioningStatus.Cancelled, "cancelled"));

  public static readonly ValueConverter<ProvisioningFailureCategory, string> ProvisioningFailureCategory = Create(
      (Domain.ProvisioningFailureCategory.InvitationExpired, "invitation_expired"),
      (Domain.ProvisioningFailureCategory.Authentication, "authentication"),
      (Domain.ProvisioningFailureCategory.TransportUnknown, "transport_unknown"),
      (Domain.ProvisioningFailureCategory.Conflict, "conflict"),
      (Domain.ProvisioningFailureCategory.Configuration, "configuration"),
      (Domain.ProvisioningFailureCategory.ExternalUnavailable, "external_unavailable"),
      (Domain.ProvisioningFailureCategory.VerificationFailed, "verification_failed"),
      (Domain.ProvisioningFailureCategory.ManualReview, "manual_review"));

  public static readonly ValueConverter<TopicSystemKey, string> TopicSystemKey = Create(
      (Domain.TopicSystemKey.Unassigned, "UNASSIGNED"));

  public static readonly ValueConverter<DocumentKind, string> DocumentKind = Create(
      (Domain.DocumentKind.Meeting, "meeting"),
      (Domain.DocumentKind.Note, "note"));

  public static readonly ValueConverter<PageCountSource, string> PageCountSource = Create(
      (Domain.PageCountSource.PdfImport, "pdf_import"),
      (Domain.PageCountSource.Viewer, "viewer"),
      (Domain.PageCountSource.ViaDocsVerified, "via_docs_verified"));

  public static readonly ValueConverter<DocumentAvailability, string> DocumentAvailability = Create(
      (Domain.DocumentAvailability.PendingUpload, "pending_upload"),
      (Domain.DocumentAvailability.UploadFailed, "upload_failed"),
      (Domain.DocumentAvailability.Available, "available"),
      (Domain.DocumentAvailability.Missing, "missing"),
      (Domain.DocumentAvailability.DeletePending, "delete_pending"));

  public static readonly ValueConverter<PendingTranscriptStatus, string> PendingTranscriptStatus = Create(
      (Domain.PendingTranscriptStatus.Pending, "pending"),
      (Domain.PendingTranscriptStatus.Matched, "matched"),
      (Domain.PendingTranscriptStatus.Discarded, "discarded"));

  public static readonly ValueConverter<FileOperationKind, string> FileOperationKind = Create(
      (Domain.FileOperationKind.Upload, "upload"),
      (Domain.FileOperationKind.Delete, "delete"));

  public static readonly ValueConverter<FileOperationState, string> FileOperationState = Create(
      (Domain.FileOperationState.Prepared, "prepared"),
      (Domain.FileOperationState.InFlight, "in_flight"),
      (Domain.FileOperationState.RemoteKnown, "remote_known"),
      (Domain.FileOperationState.Unknown, "unknown"),
      (Domain.FileOperationState.DeleteInFlight, "delete_in_flight"),
      (Domain.FileOperationState.DeleteUnknown, "delete_unknown"),
      (Domain.FileOperationState.Completed, "completed"),
      (Domain.FileOperationState.Failed, "failed"));

  public static readonly ValueConverter<FileOperationOutcome, string> FileOperationOutcome = Create(
      (Domain.FileOperationOutcome.RemoteDeleted, "remote_deleted"));

  public static readonly ValueConverter<FileOperationFailureCategory, string> FileOperationFailureCategory = Create(
      (Domain.FileOperationFailureCategory.NotSent, "not_sent"),
      (Domain.FileOperationFailureCategory.TransportUnknown, "transport_unknown"),
      (Domain.FileOperationFailureCategory.VerificationFailed, "verification_failed"),
      (Domain.FileOperationFailureCategory.ManualReview, "manual_review"));

  private static ValueConverter<T, string> Create<T>(params (T Value, string Text)[] pairs)
      where T : struct, Enum
  {
    var toText = pairs.ToDictionary(pair => pair.Value, pair => pair.Text);
    var fromText = pairs.ToDictionary(pair => pair.Text, pair => pair.Value, StringComparer.Ordinal);
    if (!Enum.GetValues<T>().All(toText.ContainsKey))
    {
      throw new InvalidOperationException($"Incomplete persistence mapping for {typeof(T).Name}.");
    }

    // Unknown enum values and unknown database strings fail instead of silently changing state.
    return new ValueConverter<T, string>(value => toText[value], text => fromText[text]);
  }
}
