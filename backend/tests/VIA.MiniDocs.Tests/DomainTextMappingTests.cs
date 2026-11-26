using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed class DomainTextMappingTests
{
  [Theory]
  [InlineData(typeof(MiniDocsUser), nameof(MiniDocsUser.AccountKind), AccountKind.Regular, "regular")]
  [InlineData(typeof(MiniDocsUser), nameof(MiniDocsUser.AccountKind), AccountKind.AdminOnly, "admin_only")]
  [InlineData(typeof(Workspace), nameof(Workspace.Status), WorkspaceStatus.Pending, "pending")]
  [InlineData(typeof(Workspace), nameof(Workspace.Status), WorkspaceStatus.Active, "active")]
  [InlineData(typeof(Workspace), nameof(Workspace.Status), WorkspaceStatus.Suspended, "suspended")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.Status), ProvisioningStatus.PendingInvitation, "pending_invitation")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.Status), ProvisioningStatus.Ready, "ready")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.Status), ProvisioningStatus.Registering, "registering")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.Status), ProvisioningStatus.RecoveryRequired, "recovery_required")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.Status), ProvisioningStatus.Active, "active")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.Status), ProvisioningStatus.Cancelled, "cancelled")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.FailureCategory), ProvisioningFailureCategory.InvitationExpired, "invitation_expired")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.FailureCategory), ProvisioningFailureCategory.Authentication, "authentication")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.FailureCategory), ProvisioningFailureCategory.TransportUnknown, "transport_unknown")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.FailureCategory), ProvisioningFailureCategory.Conflict, "conflict")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.FailureCategory), ProvisioningFailureCategory.Configuration, "configuration")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.FailureCategory), ProvisioningFailureCategory.ExternalUnavailable, "external_unavailable")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.FailureCategory), ProvisioningFailureCategory.VerificationFailed, "verification_failed")]
  [InlineData(typeof(ProvisioningOperation), nameof(ProvisioningOperation.FailureCategory), ProvisioningFailureCategory.ManualReview, "manual_review")]
  [InlineData(typeof(Topic), nameof(Topic.SystemKey), TopicSystemKey.Unassigned, "UNASSIGNED")]
  [InlineData(typeof(Document), nameof(Document.Kind), DocumentKind.Meeting, "meeting")]
  [InlineData(typeof(Document), nameof(Document.Kind), DocumentKind.Note, "note")]
  [InlineData(typeof(Document), nameof(Document.PageCountSource), PageCountSource.PdfImport, "pdf_import")]
  [InlineData(typeof(Document), nameof(Document.PageCountSource), PageCountSource.Viewer, "viewer")]
  [InlineData(typeof(Document), nameof(Document.PageCountSource), PageCountSource.ViaDocsVerified, "via_docs_verified")]
  [InlineData(typeof(Document), nameof(Document.Availability), DocumentAvailability.PendingUpload, "pending_upload")]
  [InlineData(typeof(Document), nameof(Document.Availability), DocumentAvailability.UploadFailed, "upload_failed")]
  [InlineData(typeof(Document), nameof(Document.Availability), DocumentAvailability.Available, "available")]
  [InlineData(typeof(Document), nameof(Document.Availability), DocumentAvailability.Missing, "missing")]
  [InlineData(typeof(Document), nameof(Document.Availability), DocumentAvailability.DeletePending, "delete_pending")]
  [InlineData(typeof(PendingTranscript), nameof(PendingTranscript.Status), PendingTranscriptStatus.Pending, "pending")]
  [InlineData(typeof(PendingTranscript), nameof(PendingTranscript.Status), PendingTranscriptStatus.Matched, "matched")]
  [InlineData(typeof(PendingTranscript), nameof(PendingTranscript.Status), PendingTranscriptStatus.Discarded, "discarded")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.Kind), FileOperationKind.Upload, "upload")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.Kind), FileOperationKind.Delete, "delete")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.State), FileOperationState.Prepared, "prepared")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.State), FileOperationState.InFlight, "in_flight")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.State), FileOperationState.RemoteKnown, "remote_known")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.State), FileOperationState.Unknown, "unknown")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.State), FileOperationState.DeleteInFlight, "delete_in_flight")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.State), FileOperationState.DeleteUnknown, "delete_unknown")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.State), FileOperationState.Completed, "completed")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.State), FileOperationState.Failed, "failed")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.FailureCategory), FileOperationFailureCategory.NotSent, "not_sent")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.FailureCategory), FileOperationFailureCategory.TransportUnknown, "transport_unknown")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.FailureCategory), FileOperationFailureCategory.VerificationFailed, "verification_failed")]
  [InlineData(typeof(FileOperation), nameof(FileOperation.FailureCategory), FileOperationFailureCategory.ManualReview, "manual_review")]
  [InlineData(typeof(FileOperationReceipt), nameof(FileOperationReceipt.Kind), FileOperationKind.Upload, "upload")]
  [InlineData(typeof(FileOperationReceipt), nameof(FileOperationReceipt.Kind), FileOperationKind.Delete, "delete")]
  [InlineData(typeof(FileOperationReceipt), nameof(FileOperationReceipt.Outcome), FileOperationOutcome.RemoteDeleted, "remote_deleted")]
  public void StableTextRoundTrips(Type entityType, string propertyName, object value, string expected)
  {
    using var db = CreateModelContext();
    var converter = db.Model.FindEntityType(entityType)!.FindProperty(propertyName)!.GetTypeMapping().Converter!;

    Assert.Equal(expected, converter.ConvertToProvider(value));
    Assert.Equal(value, converter.ConvertFromProvider(expected));
    Assert.Throws<KeyNotFoundException>(() => converter.ConvertFromProvider("invalid-value"));
    var invalid = Enum.ToObject(value.GetType(), int.MaxValue);
    Assert.Throws<KeyNotFoundException>(() => converter.ConvertToProvider(invalid));
  }

  [Fact]
  public void OptionalStatesKeepNull()
  {
    using var db = CreateModelContext();
    var converter = db.Model.FindEntityType(typeof(Document))!
        .FindProperty(nameof(Document.PageCountSource))!.GetTypeMapping().Converter!;
    Assert.Null(converter.ConvertToProvider(null));
    Assert.Null(converter.ConvertFromProvider(null));
  }

  private static MiniDocsDbContext CreateModelContext()
  {
    // Building the EF model does not open a database connection.
    var options = new DbContextOptionsBuilder<MiniDocsDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=minidocs_model_only;Username=model_only",
            postgres => postgres.SetPostgresVersion(18, 7))
        .Options;
    return new MiniDocsDbContext(options);
  }
}
