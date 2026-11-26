using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class PostgresModelTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  [Fact]
  public async Task InitialMigrationCreatesIdentityAndAllModelTables()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await db.Database.MigrateAsync();

    var tables = await db.Database.SqlQueryRaw<string>("""
            SELECT table_name AS "Value"
            FROM information_schema.tables
            WHERE table_schema = 'minidocs'
            """).ToListAsync();

    var expectedTables = new[]
    {
            "identity_users",
            "identity_roles",
            "identity_user_roles",
            "identity_user_claims",
            "identity_user_logins",
            "identity_user_tokens",
            "identity_role_claims",
            "workspaces",
            "user_invitations",
            "via_docs_accounts",
            "provisioning_operations",
            "topics",
            "documents",
            "tags",
            "document_tags",
            "transcript_pages",
            "pending_transcripts",
            "pending_transcript_pages",
            "file_operations",
            "upload_request_tombstones",
            "file_operation_receipts"
        };

    Assert.Equal(expectedTables.Order(StringComparer.Ordinal), tables.Order(StringComparer.Ordinal));

    var invitationColumns = await db.Database.SqlQueryRaw<string>("""
            SELECT column_name AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'minidocs' AND table_name = 'user_invitations'
            """).ToListAsync();

    Assert.Contains("created_at", invitationColumns);
    Assert.Contains("version", invitationColumns);
    Assert.DoesNotContain("workspace_id", invitationColumns);

    var versionProperties = db.Model.GetEntityTypes()
        .SelectMany(entity => entity.GetProperties())
        .Where(property => property.Name == "Version")
        .ToArray();

    Assert.NotEmpty(versionProperties);
    Assert.All(versionProperties, property => Assert.True(property.IsConcurrencyToken));
  }

  [Fact]
  public async Task DatabaseRejectsCrossWorkspaceDocumentTopicRelation()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await db.Database.MigrateAsync();

    var ownerA = Guid.NewGuid();
    var ownerB = Guid.NewGuid();
    var workspaceA = Guid.NewGuid();
    var workspaceB = Guid.NewGuid();
    var topicId = Guid.NewGuid();
    var documentId = Guid.NewGuid();

    await PostgresSeed.InsertUserAndWorkspace(db, ownerA, workspaceA, "a");
    await PostgresSeed.InsertUserAndWorkspace(db, ownerB, workspaceB, "b");

    await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO minidocs.topics(id, workspace_id, name)
            VALUES ({topicId}, {workspaceB}, 'Other workspace topic')
            """);

    var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO minidocs.documents(
                    id, workspace_id, topic_id, title, kind, availability,
                    transcript_present, transcript_edited, transcript_revision)
                VALUES (
                    {documentId}, {workspaceA}, {topicId}, 'Cross tenant', 'meeting', 'pending_upload',
                    false, false, 0)
                """));

    Assert.Equal("FK_documents_topics_workspace_id_topic_id", exception.ConstraintName);
  }

  [Fact]
  public async Task DatabaseRejectsInvalidProvisioningStatusAndPageCountSource()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await db.Database.MigrateAsync();

    var ownerId = Guid.NewGuid();
    var workspaceId = Guid.NewGuid();
    var topicId = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, ownerId, workspaceId, "constraint-test");

    var provisioningException = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO minidocs.provisioning_operations(id, workspace_id, status, attempt_no)
                VALUES ({Guid.NewGuid()}, {workspaceId}, 'not_a_state', 0)
                """));

    Assert.Equal("ck_provisioning_status", provisioningException.ConstraintName);

    await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO minidocs.topics(id, workspace_id, name)
            VALUES ({topicId}, {workspaceId}, 'Valid topic')
            """);

    var pageCountSourceException = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO minidocs.documents(
                    id, workspace_id, topic_id, title, kind, page_count, page_count_source,
                    availability, transcript_present, transcript_edited, transcript_revision)
                VALUES (
                    {Guid.NewGuid()}, {workspaceId}, {topicId}, 'Missing source', 'meeting', 2, NULL,
                    'pending_upload', false, false, 0)
                """));

    Assert.Equal("ck_document_page_source", pageCountSourceException.ConstraintName);
  }

  [Fact]
  public async Task DatabaseDefaultsAndTombstoneContractMatchArchitecture()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await db.Database.MigrateAsync();
    var workspaceA = Guid.NewGuid();
    var workspaceB = Guid.NewGuid();
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspaceA, "defaults-a");
    await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspaceB, "defaults-b");
    var topicId = Guid.NewGuid();
    var documentId = Guid.NewGuid();
    var requestId = Guid.NewGuid();

    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name)
        VALUES ({topicId}, {workspaceA}, 'Defaults')
        """);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
        VALUES ({documentId}, {workspaceA}, {topicId}, 'Defaults', 'meeting', 'pending_upload')
        """);
    var document = await db.Documents.AsNoTracking().SingleAsync(value => value.Id == documentId);
    Assert.False(document.TranscriptPresent);
    Assert.False(document.TranscriptEdited);
    Assert.Equal(0L, document.TranscriptRevision);
    Assert.Equal(1L, document.Version);

    var columns = await db.Database.SqlQueryRaw<string>("""
        SELECT column_name AS "Value" FROM information_schema.columns
        WHERE table_schema = 'minidocs' AND table_name = 'upload_request_tombstones'
        ORDER BY column_name
        """).ToListAsync();
    Assert.Equal(new[] { "request_id", "workspace_id" }, columns);

    db.UploadRequestTombstones.AddRange(
        new UploadRequestTombstone { WorkspaceId = workspaceA, RequestId = requestId },
        new UploadRequestTombstone { WorkspaceId = workspaceB, RequestId = requestId });
    await db.SaveChangesAsync();
    Assert.Equal(2, await db.UploadRequestTombstones.CountAsync());

    var duplicate = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO minidocs.upload_request_tombstones(workspace_id, request_id)
            VALUES ({workspaceA}, {requestId})
            """));
    Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    Assert.Equal("PK_upload_request_tombstones", duplicate.ConstraintName);

    var foreignWorkspace = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO minidocs.upload_request_tombstones(workspace_id, request_id)
            VALUES ({Guid.NewGuid()}, {requestId})
            """));
    Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, foreignWorkspace.SqlState);
  }

  [Fact]
  public async Task ProtectedColumnsUseTheApprovedNames()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await db.Database.MigrateAsync();
    var columns = await db.Database.SqlQueryRaw<string>("""
        SELECT (table_name || '.' || column_name) AS "Value"
        FROM information_schema.columns WHERE table_schema = 'minidocs'
        """).ToListAsync();
    Assert.Contains("via_docs_accounts.application_secret_protected", columns);
    Assert.Contains("provisioning_operations.invitation_protected", columns);
    Assert.DoesNotContain("via_docs_accounts.protected_secret", columns);
    Assert.DoesNotContain("provisioning_operations.protected_invitation", columns);
  }
}
