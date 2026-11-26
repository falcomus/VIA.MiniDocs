using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

internal sealed class FileOperationConfiguration : IEntityTypeConfiguration<FileOperation>
{
  public void Configure(EntityTypeBuilder<FileOperation> entity)
  {
    entity.ToTable("file_operations", table =>
    {
      table.HasCheckConstraint("ck_file_attempt", "attempt_no >= 0");
      table.HasCheckConstraint(
              "ck_file_lease_pair",
              "(lease_owner IS NULL) = (lease_until IS NULL)");
      table.HasCheckConstraint(
              "ck_file_digest_pair",
              "(verified_size IS NULL) = (verified_sha256 IS NULL)");
      table.HasCheckConstraint(
              "ck_file_digest_len",
              "verified_sha256 IS NULL OR octet_length(verified_sha256)=32");
      table.HasCheckConstraint(
              "ck_file_size",
              "verified_size IS NULL OR verified_size >= 0");
      table.HasCheckConstraint(
              "ck_file_kind_state",
              "(kind='upload' AND state IN ('prepared','in_flight','remote_known','unknown','completed','failed')) OR (kind='delete' AND state IN ('prepared','delete_in_flight','delete_unknown','completed','failed'))");
      table.HasCheckConstraint(
              "ck_file_request_id",
              "(kind='upload' AND request_id IS NOT NULL) OR (kind='delete' AND request_id IS NULL)");
      table.HasCheckConstraint(
              "ck_file_completed_at",
              "(state='completed') = (completed_at IS NOT NULL)");
    });
    entity.Property(value => value.Kind)
        .HasConversion(DomainTextConverters.FileOperationKind);
    entity.Property(value => value.State)
        .HasConversion(DomainTextConverters.FileOperationState);
    entity.Property(value => value.FailureCategory)
        .HasConversion(DomainTextConverters.FileOperationFailureCategory);
    entity.HasKey(operation => operation.Id);
    entity.HasAlternateKey(operation => new { operation.WorkspaceId, operation.Id });
    entity.HasOne<Document>()
        .WithMany()
        .HasForeignKey(operation => new { operation.WorkspaceId, operation.DocumentId })
        .HasPrincipalKey(document => new { document.WorkspaceId, document.Id })
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(operation => new { operation.WorkspaceId, operation.RequestId })
        .IsUnique()
        .HasFilter("request_id IS NOT NULL");
    entity.HasIndex(operation => new { operation.WorkspaceId, operation.DocumentId })
        .IsUnique()
        .HasFilter("state IN ('prepared','in_flight','remote_known','unknown','delete_in_flight','delete_unknown')");
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(value => value.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(value => value.WorkspaceId);
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}

internal sealed class UploadRequestTombstoneConfiguration : IEntityTypeConfiguration<UploadRequestTombstone>
{
  public void Configure(EntityTypeBuilder<UploadRequestTombstone> entity)
  {
    entity.ToTable("upload_request_tombstones");
    entity.HasKey(value => new { value.WorkspaceId, value.RequestId });
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(value => value.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
  }
}

internal sealed class FileOperationReceiptConfiguration : IEntityTypeConfiguration<FileOperationReceipt>
{
  public void Configure(EntityTypeBuilder<FileOperationReceipt> entity)
  {
    entity.ToTable("file_operation_receipts", table =>
        table.HasCheckConstraint(
            "ck_receipt_kind_outcome",
            "kind='delete' AND outcome='remote_deleted'"));
    entity.Property(value => value.Kind)
        .HasConversion(DomainTextConverters.FileOperationKind);
    entity.Property(value => value.Outcome)
        .HasConversion(DomainTextConverters.FileOperationOutcome);
    entity.HasKey(receipt => receipt.Id);
    entity.HasIndex(receipt => receipt.OperationId).IsUnique();
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(receipt => receipt.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(value => value.WorkspaceId);
  }
}
