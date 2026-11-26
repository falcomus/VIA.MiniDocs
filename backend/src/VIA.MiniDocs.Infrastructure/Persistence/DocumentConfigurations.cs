using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
  public void Configure(EntityTypeBuilder<Topic> entity)
  {
    entity.ToTable("topics", table =>
    {
      table.HasCheckConstraint(
              "ck_topic_not_self",
              "parent_id IS NULL OR parent_id <> id");
      table.HasCheckConstraint(
              "ck_topic_system_key",
              "system_key IS NULL OR system_key = 'UNASSIGNED'");
      table.HasCheckConstraint(
              "ck_topic_system_root",
              "system_key IS NULL OR parent_id IS NULL");
      table.HasCheckConstraint("ck_topic_name", "length(btrim(name)) > 0");
    });
    entity.Property(value => value.SystemKey)
        .HasConversion(DomainTextConverters.TopicSystemKey);
    entity.HasKey(topic => topic.Id);
    entity.HasAlternateKey(topic => new { topic.WorkspaceId, topic.Id });
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(topic => topic.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasOne<Topic>()
        .WithMany()
        .HasForeignKey(topic => new { topic.WorkspaceId, topic.ParentId })
        .HasPrincipalKey(topic => new { topic.WorkspaceId, topic.Id })
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(topic => topic.WorkspaceId)
        .IsUnique()
        .HasFilter("system_key = 'UNASSIGNED'");
    entity.HasIndex(topic => new { topic.WorkspaceId, topic.ParentId });
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
  public void Configure(EntityTypeBuilder<Document> entity)
  {
    entity.ToTable("documents", table =>
    {
      table.HasCheckConstraint("ck_document_kind", "kind IN ('meeting','note')");
      table.HasCheckConstraint(
              "ck_document_page_count",
              "page_count IS NULL OR page_count > 0");
      table.HasCheckConstraint(
              "ck_document_page_source",
              "(page_count IS NULL AND page_count_source IS NULL) OR (page_count IS NOT NULL AND page_count_source IS NOT NULL AND page_count_source IN ('pdf_import','viewer','via_docs_verified'))");
      table.HasCheckConstraint(
              "ck_document_availability",
              "availability IN ('pending_upload','upload_failed','available','missing','delete_pending')");
      table.HasCheckConstraint(
              "ck_document_remote_pair",
              "(via_file_id IS NOT NULL) = (availability IN ('available','missing','delete_pending'))");
      table.HasCheckConstraint(
              "ck_document_transcript_revision",
              "transcript_revision >= 0");
    });
    entity.Property(value => value.Kind)
        .HasConversion(DomainTextConverters.DocumentKind);
    entity.Property(value => value.PageCountSource)
        .HasConversion(DomainTextConverters.PageCountSource);
    entity.Property(value => value.Availability)
        .HasConversion(DomainTextConverters.DocumentAvailability);
    entity.HasKey(document => document.Id);
    entity.HasAlternateKey(document => new { document.WorkspaceId, document.Id });
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(document => document.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasOne<Topic>()
        .WithMany()
        .HasForeignKey(document => new { document.WorkspaceId, document.TopicId })
        .HasPrincipalKey(topic => new { topic.WorkspaceId, topic.Id })
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(document => new { document.WorkspaceId, document.ViaFileId })
        .IsUnique()
        .HasFilter("via_file_id IS NOT NULL");
    entity.HasIndex(document => new
    {
      document.WorkspaceId,
      document.TopicId,
      document.DocumentDate
    });
    entity.Property(value => value.TranscriptPresent).HasDefaultValue(false);
    entity.Property(value => value.TranscriptEdited).HasDefaultValue(false);
    entity.Property(value => value.TranscriptRevision).HasDefaultValue(0L);
    entity.HasIndex(value => value.WorkspaceId);
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
  public void Configure(EntityTypeBuilder<Tag> entity)
  {
    entity.ToTable("tags", table =>
    {
      table.HasCheckConstraint("ck_tag_name", "length(btrim(name)) > 0");
      table.HasCheckConstraint("ck_tag_color", "color ~ '^#[0-9A-Fa-f]{6}$'");
    });
    entity.HasKey(tag => tag.Id);
    entity.HasAlternateKey(tag => new { tag.WorkspaceId, tag.Id });
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(tag => tag.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(tag => new { tag.WorkspaceId, tag.NameKey }).IsUnique();
    entity.HasIndex(value => value.WorkspaceId);
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}

internal sealed class DocumentTagConfiguration : IEntityTypeConfiguration<DocumentTag>
{
  public void Configure(EntityTypeBuilder<DocumentTag> entity)
  {
    entity.ToTable("document_tags");
    entity.HasKey(link => new { link.WorkspaceId, link.DocumentId, link.TagId });
    entity.HasOne<Document>()
        .WithMany()
        .HasForeignKey(link => new { link.WorkspaceId, link.DocumentId })
        .HasPrincipalKey(document => new { document.WorkspaceId, document.Id })
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasOne<Tag>()
        .WithMany()
        .HasForeignKey(link => new { link.WorkspaceId, link.TagId })
        .HasPrincipalKey(tag => new { tag.WorkspaceId, tag.Id })
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(link => new { link.WorkspaceId, link.TagId, link.DocumentId });
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(value => value.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(value => value.WorkspaceId);
  }
}

internal sealed class TranscriptPageConfiguration : IEntityTypeConfiguration<TranscriptPage>
{
  public void Configure(EntityTypeBuilder<TranscriptPage> entity)
  {
    entity.ToTable("transcript_pages", table =>
        table.HasCheckConstraint("ck_transcript_page_no", "page_no >= 1"));
    entity.HasKey(page => new { page.WorkspaceId, page.DocumentId, page.PageNo });
    entity.HasOne<Document>()
        .WithMany()
        .HasForeignKey(page => new { page.WorkspaceId, page.DocumentId })
        .HasPrincipalKey(document => new { document.WorkspaceId, document.Id })
        .OnDelete(DeleteBehavior.Restrict);
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

internal sealed class PendingTranscriptConfiguration : IEntityTypeConfiguration<PendingTranscript>
{
  public void Configure(EntityTypeBuilder<PendingTranscript> entity)
  {
    entity.ToTable("pending_transcripts", table =>
        table.HasCheckConstraint(
            "ck_pending_transcript_status",
            "status IN ('pending','matched','discarded')"));
    entity.Property(value => value.Status)
        .HasConversion(DomainTextConverters.PendingTranscriptStatus);
    entity.HasKey(transcript => transcript.Id);
    entity.HasAlternateKey(transcript => new { transcript.WorkspaceId, transcript.Id });
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(transcript => transcript.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(value => value.WorkspaceId);
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}

internal sealed class PendingTranscriptPageConfiguration : IEntityTypeConfiguration<PendingTranscriptPage>
{
  public void Configure(EntityTypeBuilder<PendingTranscriptPage> entity)
  {
    entity.ToTable("pending_transcript_pages", table =>
        table.HasCheckConstraint("ck_pending_page_no", "page_no >= 1"));
    entity.HasKey(page => new { page.WorkspaceId, page.PendingId, page.PageNo });
    entity.HasOne<PendingTranscript>()
        .WithMany()
        .HasForeignKey(page => new { page.WorkspaceId, page.PendingId })
        .HasPrincipalKey(transcript => new { transcript.WorkspaceId, transcript.Id })
        .OnDelete(DeleteBehavior.Restrict);
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
