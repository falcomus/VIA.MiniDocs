using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

internal sealed class MiniDocsUserConfiguration : IEntityTypeConfiguration<MiniDocsUser>
{
  public void Configure(EntityTypeBuilder<MiniDocsUser> entity)
  {
    entity.ToTable("identity_users", table =>
        table.HasCheckConstraint(
            "ck_identity_users_account_kind",
            "account_kind IN ('regular','admin_only')"));
    // UserStore updates the tracked row as a whole. These columns belong to the
    // administrative write path and must never be included in routine Identity updates.
    entity.Property(user => user.IsDisabled).HasColumnName("is_disabled")
        .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
    entity.Property(user => user.AccountKind)
        .HasColumnName("account_kind")
        .HasConversion(DomainTextConverters.AccountKind)
        .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
  }
}

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
  public void Configure(EntityTypeBuilder<Workspace> entity)
  {
    entity.ToTable("workspaces", table =>
        table.HasCheckConstraint(
            "ck_workspaces_status",
            "status IN ('pending','active','suspended')"));
    entity.HasKey(workspace => workspace.Id);
    entity.HasIndex(workspace => workspace.OwnerUserId).IsUnique();
    entity.HasOne<MiniDocsUser>()
        .WithMany()
        .HasForeignKey(workspace => workspace.OwnerUserId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.Property(workspace => workspace.Status)
        .HasConversion(DomainTextConverters.WorkspaceStatus);
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}

internal sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
  public void Configure(EntityTypeBuilder<UserInvitation> entity)
  {
    entity.ToTable("user_invitations", table =>
    {
      table.HasCheckConstraint("ck_invitation_hash_len", "octet_length(token_hash)=32");
      table.HasCheckConstraint("ck_invitation_expiry", "expires_at > created_at");
      table.HasCheckConstraint(
              "ck_invitation_redeemed_pair",
              "(redeemed_at IS NULL) = (redeemed_user_id IS NULL)");
    });
    entity.HasKey(invitation => invitation.Id);
    entity.HasIndex(invitation => invitation.TokenHash).IsUnique();
    entity.HasOne<MiniDocsUser>()
        .WithMany()
        .HasForeignKey(invitation => invitation.CreatedBy)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasOne<MiniDocsUser>()
        .WithMany()
        .HasForeignKey(invitation => invitation.RedeemedUserId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}

internal sealed class ViaDocsAccountConfiguration : IEntityTypeConfiguration<ViaDocsAccount>
{
  public void Configure(EntityTypeBuilder<ViaDocsAccount> entity)
  {
    entity.ToTable("via_docs_accounts", table =>
        table.HasCheckConstraint(
            "ck_via_account_verified_pair",
            "(application_id IS NULL) = (verified_at IS NULL)"));
    entity.HasKey(account => account.Id);
    entity.HasIndex(account => account.WorkspaceId).IsUnique();
    entity.HasIndex(account => account.ApplicationName).IsUnique();
    entity.HasIndex(account => account.ApplicationId).IsUnique();
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(account => account.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.Property(value => value.ProtectedSecret).HasColumnName("application_secret_protected");
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}

internal sealed class ProvisioningOperationConfiguration : IEntityTypeConfiguration<ProvisioningOperation>
{
  public void Configure(EntityTypeBuilder<ProvisioningOperation> entity)
  {
    entity.ToTable("provisioning_operations", table =>
    {
      table.HasCheckConstraint("ck_provisioning_attempt", "attempt_no >= 0");
      table.HasCheckConstraint(
              "ck_provisioning_status",
              "status IN ('pending_invitation','ready','registering','recovery_required','active','cancelled')");
      table.HasCheckConstraint(
              "ck_provisioning_failure_category",
              "failure_category IS NULL OR failure_category IN ('invitation_expired','authentication','transport_unknown','conflict','configuration','external_unavailable','verification_failed','manual_review')");
      table.HasCheckConstraint(
              "ck_provisioning_lease_pair",
              "(lease_owner IS NULL) = (lease_until IS NULL)");
    });
    entity.Property(value => value.Status)
        .HasConversion(DomainTextConverters.ProvisioningStatus);
    entity.Property(value => value.FailureCategory)
        .HasConversion(DomainTextConverters.ProvisioningFailureCategory);
    entity.HasKey(operation => operation.Id);
    entity.HasOne<Workspace>()
        .WithMany()
        .HasForeignKey(operation => operation.WorkspaceId)
        .OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(operation => operation.WorkspaceId)
        .IsUnique()
        .HasFilter("status <> 'active' AND status <> 'cancelled'");
    entity.Property(value => value.ProtectedInvitation).HasColumnName("invitation_protected");
    entity.Property(value => value.Version).IsConcurrencyToken().HasDefaultValue(1L);
    entity.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
  }
}
