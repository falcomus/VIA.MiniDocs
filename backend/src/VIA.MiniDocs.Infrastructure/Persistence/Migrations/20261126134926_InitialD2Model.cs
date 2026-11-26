using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VIA.MiniDocs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialD2Model : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "minidocs");

            migrationBuilder.CreateTable(
                name: "identity_roles",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identity_users",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false),
                    account_kind = table.Column<string>(type: "text", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_users", x => x.id);
                    table.CheckConstraint("ck_identity_users_account_kind", "account_kind IN ('regular','admin_only')");
                });

            migrationBuilder.CreateTable(
                name: "identity_role_claims",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_identity_role_claims_identity_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "minidocs",
                        principalTable: "identity_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_claims",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_identity_user_claims_identity_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "minidocs",
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_logins",
                schema: "minidocs",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_identity_user_logins_identity_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "minidocs",
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_roles",
                schema: "minidocs",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_identity_user_roles_identity_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "minidocs",
                        principalTable: "identity_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_identity_user_roles_identity_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "minidocs",
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_tokens",
                schema: "minidocs",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "FK_identity_user_tokens_identity_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "minidocs",
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_invitations",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemed_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_invitations", x => x.id);
                    table.CheckConstraint("ck_invitation_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_invitation_hash_len", "octet_length(token_hash)=32");
                    table.CheckConstraint("ck_invitation_redeemed_pair", "(redeemed_at IS NULL) = (redeemed_user_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_user_invitations_identity_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "minidocs",
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_invitations_identity_users_redeemed_user_id",
                        column: x => x.redeemed_user_id,
                        principalSchema: "minidocs",
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                    table.CheckConstraint("ck_workspaces_status", "status IN ('pending','active','suspended')");
                    table.ForeignKey(
                        name: "FK_workspaces_identity_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalSchema: "minidocs",
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_operation_receipts",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_operation_receipts", x => x.id);
                    table.CheckConstraint("ck_receipt_kind_outcome", "kind='delete' AND outcome='remote_deleted'");
                    table.ForeignKey(
                        name: "FK_file_operation_receipts_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pending_transcripts",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    source_name = table.Column<string>(type: "text", nullable: true),
                    meta_date = table.Column<string>(type: "text", nullable: true),
                    meta_topic = table.Column<string>(type: "text", nullable: true),
                    meta_title = table.Column<string>(type: "text", nullable: true),
                    meta_tags = table.Column<string>(type: "text", nullable: true),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_transcripts", x => x.id);
                    table.UniqueConstraint("AK_pending_transcripts_workspace_id_id", x => new { x.workspace_id, x.id });
                    table.CheckConstraint("ck_pending_transcript_status", "status IN ('pending','matched','discarded')");
                    table.ForeignKey(
                        name: "FK_pending_transcripts_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provisioning_operations",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    invitation_protected = table.Column<byte[]>(type: "bytea", nullable: true),
                    candidate_application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    failure_category = table.Column<string>(type: "text", nullable: true),
                    lease_owner = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provisioning_operations", x => x.id);
                    table.CheckConstraint("ck_provisioning_attempt", "attempt_no >= 0");
                    table.CheckConstraint("ck_provisioning_failure_category", "failure_category IS NULL OR failure_category IN ('invitation_expired','authentication','transport_unknown','conflict','configuration','external_unavailable','verification_failed','manual_review')");
                    table.CheckConstraint("ck_provisioning_lease_pair", "(lease_owner IS NULL) = (lease_until IS NULL)");
                    table.CheckConstraint("ck_provisioning_status", "status IN ('pending_invitation','ready','registering','recovery_required','active','cancelled')");
                    table.ForeignKey(
                        name: "FK_provisioning_operations_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_key = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "text", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                    table.UniqueConstraint("AK_tags_workspace_id_id", x => new { x.workspace_id, x.id });
                    table.CheckConstraint("ck_tag_color", "color ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("ck_tag_name", "length(btrim(name)) > 0");
                    table.ForeignKey(
                        name: "FK_tags_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "topics",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    system_key = table.Column<string>(type: "text", nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topics", x => x.id);
                    table.UniqueConstraint("AK_topics_workspace_id_id", x => new { x.workspace_id, x.id });
                    table.CheckConstraint("ck_topic_name", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_topic_not_self", "parent_id IS NULL OR parent_id <> id");
                    table.CheckConstraint("ck_topic_system_key", "system_key IS NULL OR system_key = 'UNASSIGNED'");
                    table.CheckConstraint("ck_topic_system_root", "system_key IS NULL OR parent_id IS NULL");
                    table.ForeignKey(
                        name: "FK_topics_topics_workspace_id_parent_id",
                        columns: x => new { x.workspace_id, x.parent_id },
                        principalSchema: "minidocs",
                        principalTable: "topics",
                        principalColumns: new[] { "workspace_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_topics_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "upload_request_tombstones",
                schema: "minidocs",
                columns: table => new
                {
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upload_request_tombstones", x => new { x.workspace_id, x.request_id });
                    table.ForeignKey(
                        name: "FK_upload_request_tombstones_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "via_docs_accounts",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_name = table.Column<string>(type: "text", nullable: false),
                    application_secret_protected = table.Column<byte[]>(type: "bytea", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_via_docs_accounts", x => x.id);
                    table.CheckConstraint("ck_via_account_verified_pair", "(application_id IS NULL) = (verified_at IS NULL)");
                    table.ForeignKey(
                        name: "FK_via_docs_accounts_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pending_transcript_pages",
                schema: "minidocs",
                columns: table => new
                {
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pending_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_no = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_transcript_pages", x => new { x.workspace_id, x.pending_id, x.page_no });
                    table.CheckConstraint("ck_pending_page_no", "page_no >= 1");
                    table.ForeignKey(
                        name: "FK_pending_transcript_pages_pending_transcripts_workspace_id_p~",
                        columns: x => new { x.workspace_id, x.pending_id },
                        principalSchema: "minidocs",
                        principalTable: "pending_transcripts",
                        principalColumns: new[] { "workspace_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pending_transcript_pages_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    via_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: true),
                    page_count = table.Column<int>(type: "integer", nullable: true),
                    page_count_source = table.Column<string>(type: "text", nullable: true),
                    availability = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    transcript_present = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    transcript_edited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    transcript_revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.UniqueConstraint("AK_documents_workspace_id_id", x => new { x.workspace_id, x.id });
                    table.CheckConstraint("ck_document_availability", "availability IN ('pending_upload','upload_failed','available','missing','delete_pending')");
                    table.CheckConstraint("ck_document_kind", "kind IN ('meeting','note')");
                    table.CheckConstraint("ck_document_page_count", "page_count IS NULL OR page_count > 0");
                    table.CheckConstraint("ck_document_page_source", "(page_count IS NULL AND page_count_source IS NULL) OR (page_count IS NOT NULL AND page_count_source IS NOT NULL AND page_count_source IN ('pdf_import','viewer','via_docs_verified'))");
                    table.CheckConstraint("ck_document_remote_pair", "(via_file_id IS NOT NULL) = (availability IN ('available','missing','delete_pending'))");
                    table.CheckConstraint("ck_document_transcript_revision", "transcript_revision >= 0");
                    table.ForeignKey(
                        name: "FK_documents_topics_workspace_id_topic_id",
                        columns: x => new { x.workspace_id, x.topic_id },
                        principalSchema: "minidocs",
                        principalTable: "topics",
                        principalColumns: new[] { "workspace_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_tags",
                schema: "minidocs",
                columns: table => new
                {
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_tags", x => new { x.workspace_id, x.document_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_document_tags_documents_workspace_id_document_id",
                        columns: x => new { x.workspace_id, x.document_id },
                        principalSchema: "minidocs",
                        principalTable: "documents",
                        principalColumns: new[] { "workspace_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_tags_tags_workspace_id_tag_id",
                        columns: x => new { x.workspace_id, x.tag_id },
                        principalSchema: "minidocs",
                        principalTable: "tags",
                        principalColumns: new[] { "workspace_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_tags_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_operations",
                schema: "minidocs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    remote_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    failure_category = table.Column<string>(type: "text", nullable: true),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    lease_owner = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_size = table.Column<long>(type: "bigint", nullable: true),
                    verified_sha256 = table.Column<byte[]>(type: "bytea", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_operations", x => x.id);
                    table.UniqueConstraint("AK_file_operations_workspace_id_id", x => new { x.workspace_id, x.id });
                    table.CheckConstraint("ck_file_attempt", "attempt_no >= 0");
                    table.CheckConstraint("ck_file_completed_at", "(state='completed') = (completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_file_digest_len", "verified_sha256 IS NULL OR octet_length(verified_sha256)=32");
                    table.CheckConstraint("ck_file_digest_pair", "(verified_size IS NULL) = (verified_sha256 IS NULL)");
                    table.CheckConstraint("ck_file_kind_state", "(kind='upload' AND state IN ('prepared','in_flight','remote_known','unknown','completed','failed')) OR (kind='delete' AND state IN ('prepared','delete_in_flight','delete_unknown','completed','failed'))");
                    table.CheckConstraint("ck_file_lease_pair", "(lease_owner IS NULL) = (lease_until IS NULL)");
                    table.CheckConstraint("ck_file_request_id", "(kind='upload' AND request_id IS NOT NULL) OR (kind='delete' AND request_id IS NULL)");
                    table.CheckConstraint("ck_file_size", "verified_size IS NULL OR verified_size >= 0");
                    table.ForeignKey(
                        name: "FK_file_operations_documents_workspace_id_document_id",
                        columns: x => new { x.workspace_id, x.document_id },
                        principalSchema: "minidocs",
                        principalTable: "documents",
                        principalColumns: new[] { "workspace_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_file_operations_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transcript_pages",
                schema: "minidocs",
                columns: table => new
                {
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_no = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    edited = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transcript_pages", x => new { x.workspace_id, x.document_id, x.page_no });
                    table.CheckConstraint("ck_transcript_page_no", "page_no >= 1");
                    table.ForeignKey(
                        name: "FK_transcript_pages_documents_workspace_id_document_id",
                        columns: x => new { x.workspace_id, x.document_id },
                        principalSchema: "minidocs",
                        principalTable: "documents",
                        principalColumns: new[] { "workspace_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transcript_pages_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "minidocs",
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_identity_role_claims_role_id",
                schema: "minidocs",
                table: "identity_role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "minidocs",
                table: "identity_roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_claims_user_id",
                schema: "minidocs",
                table: "identity_user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_logins_user_id",
                schema: "minidocs",
                table: "identity_user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_roles_role_id",
                schema: "minidocs",
                table: "identity_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_tags_workspace_id",
                schema: "minidocs",
                table: "document_tags",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_tags_workspace_id_tag_id_document_id",
                schema: "minidocs",
                table: "document_tags",
                columns: new[] { "workspace_id", "tag_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_workspace_id",
                schema: "minidocs",
                table: "documents",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_workspace_id_topic_id_document_date",
                schema: "minidocs",
                table: "documents",
                columns: new[] { "workspace_id", "topic_id", "document_date" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_workspace_id_via_file_id",
                schema: "minidocs",
                table: "documents",
                columns: new[] { "workspace_id", "via_file_id" },
                unique: true,
                filter: "via_file_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_file_operation_receipts_operation_id",
                schema: "minidocs",
                table: "file_operation_receipts",
                column: "operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_operation_receipts_workspace_id",
                schema: "minidocs",
                table: "file_operation_receipts",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_file_operations_workspace_id",
                schema: "minidocs",
                table: "file_operations",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_file_operations_workspace_id_document_id",
                schema: "minidocs",
                table: "file_operations",
                columns: new[] { "workspace_id", "document_id" },
                unique: true,
                filter: "state IN ('prepared','in_flight','remote_known','unknown','delete_in_flight','delete_unknown')");

            migrationBuilder.CreateIndex(
                name: "IX_file_operations_workspace_id_request_id",
                schema: "minidocs",
                table: "file_operations",
                columns: new[] { "workspace_id", "request_id" },
                unique: true,
                filter: "request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "minidocs",
                table: "identity_users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "minidocs",
                table: "identity_users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_transcript_pages_workspace_id",
                schema: "minidocs",
                table: "pending_transcript_pages",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_pending_transcripts_workspace_id",
                schema: "minidocs",
                table: "pending_transcripts",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_provisioning_operations_workspace_id",
                schema: "minidocs",
                table: "provisioning_operations",
                column: "workspace_id",
                unique: true,
                filter: "status <> 'active' AND status <> 'cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_tags_workspace_id",
                schema: "minidocs",
                table: "tags",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_tags_workspace_id_name_key",
                schema: "minidocs",
                table: "tags",
                columns: new[] { "workspace_id", "name_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topics_workspace_id",
                schema: "minidocs",
                table: "topics",
                column: "workspace_id",
                unique: true,
                filter: "system_key = 'UNASSIGNED'");

            migrationBuilder.CreateIndex(
                name: "IX_topics_workspace_id_parent_id",
                schema: "minidocs",
                table: "topics",
                columns: new[] { "workspace_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_transcript_pages_workspace_id",
                schema: "minidocs",
                table: "transcript_pages",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_created_by",
                schema: "minidocs",
                table: "user_invitations",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_redeemed_user_id",
                schema: "minidocs",
                table: "user_invitations",
                column: "redeemed_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_token_hash",
                schema: "minidocs",
                table: "user_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_via_docs_accounts_application_id",
                schema: "minidocs",
                table: "via_docs_accounts",
                column: "application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_via_docs_accounts_application_name",
                schema: "minidocs",
                table: "via_docs_accounts",
                column: "application_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_via_docs_accounts_workspace_id",
                schema: "minidocs",
                table: "via_docs_accounts",
                column: "workspace_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_owner_user_id",
                schema: "minidocs",
                table: "workspaces",
                column: "owner_user_id",
                unique: true);

            migrationBuilder.Sql(D2IntegritySql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(D2IntegritySql.Down);

            migrationBuilder.DropTable(
                name: "identity_role_claims",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "identity_user_claims",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "identity_user_logins",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "identity_user_roles",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "identity_user_tokens",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "document_tags",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "file_operation_receipts",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "file_operations",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "pending_transcript_pages",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "provisioning_operations",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "transcript_pages",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "upload_request_tombstones",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "user_invitations",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "via_docs_accounts",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "identity_roles",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "pending_transcripts",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "topics",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "workspaces",
                schema: "minidocs");

            migrationBuilder.DropTable(
                name: "identity_users",
                schema: "minidocs");
        }
    }
}
