using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

public sealed class MiniDocsDbContext(DbContextOptions<MiniDocsDbContext> options)
    : IdentityDbContext<MiniDocsUser, IdentityRole<Guid>, Guid>(options)
{
  public const string WorkspaceIsolationFilter = "WorkspaceIsolation";
  public Guid? AuthorizedWorkspaceId { get; private set; }

  public void BindActiveWorkspace(Guid workspaceId)
  {
    if (workspaceId == Guid.Empty || (AuthorizedWorkspaceId.HasValue && AuthorizedWorkspaceId != workspaceId))
      throw new InvalidOperationException("A context can only be bound to one active workspace.");
    AuthorizedWorkspaceId = workspaceId;
  }

  public DbSet<Workspace> Workspaces => Set<Workspace>();
  public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();
  public DbSet<ViaDocsAccount> ViaDocsAccounts => Set<ViaDocsAccount>();
  public DbSet<ProvisioningOperation> ProvisioningOperations => Set<ProvisioningOperation>();
  public DbSet<Topic> Topics => Set<Topic>();
  public DbSet<Document> Documents => Set<Document>();
  public DbSet<Tag> Tags => Set<Tag>();
  public DbSet<DocumentTag> DocumentTags => Set<DocumentTag>();
  public DbSet<TranscriptPage> TranscriptPages => Set<TranscriptPage>();
  public DbSet<PendingTranscript> PendingTranscripts => Set<PendingTranscript>();
  public DbSet<PendingTranscriptPage> PendingTranscriptPages => Set<PendingTranscriptPage>();
  public DbSet<FileOperation> FileOperations => Set<FileOperation>();
  public DbSet<UploadRequestTombstone> UploadRequestTombstones => Set<UploadRequestTombstone>();
  public DbSet<FileOperationReceipt> FileOperationReceipts => Set<FileOperationReceipt>();

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
    builder.HasDefaultSchema("minidocs");

    // Naming is the only shared convention applied to Identity and domain properties.
    // Explicit column overrides in the configurations below take precedence.
    foreach (var entity in builder.Model.GetEntityTypes())
    {
      foreach (var property in entity.GetProperties())
      {
        property.SetColumnName(ToSnakeCase(property.Name));
      }
    }

    builder.Entity<IdentityRole<Guid>>().ToTable("identity_roles");
    builder.Entity<IdentityRoleClaim<Guid>>().ToTable("identity_role_claims");
    builder.Entity<IdentityUserClaim<Guid>>().ToTable("identity_user_claims");
    builder.Entity<IdentityUserLogin<Guid>>().ToTable("identity_user_logins");
    builder.Entity<IdentityUserRole<Guid>>().ToTable("identity_user_roles");
    builder.Entity<IdentityUserToken<Guid>>().ToTable("identity_user_tokens");

    builder.ApplyConfiguration(new MiniDocsUserConfiguration());
    builder.ApplyConfiguration(new WorkspaceConfiguration());
    builder.ApplyConfiguration(new UserInvitationConfiguration());
    builder.ApplyConfiguration(new ViaDocsAccountConfiguration());
    builder.ApplyConfiguration(new ProvisioningOperationConfiguration());
    builder.ApplyConfiguration(new TopicConfiguration());
    builder.ApplyConfiguration(new DocumentConfiguration());
    builder.ApplyConfiguration(new TagConfiguration());
    builder.ApplyConfiguration(new DocumentTagConfiguration());
    builder.ApplyConfiguration(new TranscriptPageConfiguration());
    builder.ApplyConfiguration(new PendingTranscriptConfiguration());
    builder.ApplyConfiguration(new PendingTranscriptPageConfiguration());
    builder.ApplyConfiguration(new FileOperationConfiguration());
    builder.ApplyConfiguration(new UploadRequestTombstoneConfiguration());
    builder.ApplyConfiguration(new FileOperationReceiptConfiguration());

    // EF parameterizes instance members at execution time. The nullable
    // parameter is deliberately unset for anonymous and administrative scopes.
    builder.Entity<Topic>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<Document>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<Tag>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<DocumentTag>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<TranscriptPage>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<PendingTranscript>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<PendingTranscriptPage>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<FileOperation>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<FileOperationReceipt>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
    builder.Entity<UploadRequestTombstone>().HasQueryFilter(WorkspaceIsolationFilter, row => row.WorkspaceId == AuthorizedWorkspaceId);
  }

  private static string ToSnakeCase(string value) => string.Concat(
      value.Select((character, index) =>
          index > 0 && char.IsUpper(character)
              ? "_" + char.ToLowerInvariant(character)
              : char.ToLowerInvariant(character).ToString()));
}
