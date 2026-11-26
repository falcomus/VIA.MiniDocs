using Microsoft.EntityFrameworkCore;
using Npgsql;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class PostgresIsolationTests(PostgresTestDatabase fixture) : IClassFixture<PostgresTestDatabase>
{
  [Fact]
  public async Task ForeignTagDocumentTranscriptAndPendingReferencesAreRejected()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await db.Database.MigrateAsync();
    var a = Guid.NewGuid();
    var b = Guid.NewGuid();
    var topicA = await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), a, "isolation-a");
    var topicB = await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), b, "isolation-b");
    var docA = Guid.NewGuid();
    var docB = Guid.NewGuid();
    var tagA = Guid.NewGuid();
    var tagB = Guid.NewGuid();
    var pending = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
        VALUES ({docA}, {a}, {topicA}, 'A', 'meeting', 'pending_upload'),
               ({docB}, {b}, {topicB}, 'B', 'meeting', 'pending_upload');
        INSERT INTO minidocs.tags(id, workspace_id, name, name_key, description, color)
        VALUES ({tagA}, {a}, 'A', 'a', '', '#123456'), ({tagB}, {b}, 'B', 'b', '', '#123456');
        INSERT INTO minidocs.pending_transcripts(id, workspace_id, file_name, imported_at, status)
        VALUES ({pending}, {b}, 'fixture.pdf', clock_timestamp(), 'pending');
        """);

    await ForeignKeyAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.document_tags(workspace_id, document_id, tag_id)
        VALUES ({a}, {docA}, {tagB})
        """), "FK_document_tags_tags_workspace_id_tag_id");

    await ForeignKeyAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.document_tags(workspace_id, document_id, tag_id)
        VALUES ({a}, {docB}, {tagA})
        """), "FK_document_tags_documents_workspace_id_document_id");

    await ForeignKeyAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.transcript_pages(workspace_id, document_id, page_no, text, edited)
        VALUES ({a}, {docB}, 1, 'Foreign page', false)
        """), "FK_transcript_pages_documents_workspace_id_document_id");

    await ForeignKeyAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.pending_transcript_pages(workspace_id, pending_id, page_no, text)
        VALUES ({a}, {pending}, 1, 'Foreign pending page')
        """), "FK_pending_transcript_pages_pending_transcripts_workspace_id_p~");

    await ForeignKeyAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.file_operations(id, workspace_id, document_id, kind, state, request_id, attempt_no)
        VALUES ({Guid.NewGuid()}, {a}, {docB}, 'upload', 'prepared', {Guid.NewGuid()}, 0)
        """), "FK_file_operations_documents_workspace_id_document_id");

    // Valid same-workspace links still work after negative attempts.
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.document_tags(workspace_id, document_id, tag_id)
        VALUES ({a}, {docA}, {tagA});
        UPDATE minidocs.documents SET transcript_present = true WHERE id = {docA};
        INSERT INTO minidocs.transcript_pages(workspace_id, document_id, page_no, text, edited)
        VALUES ({a}, {docA}, 1, 'Own page', false);
        INSERT INTO minidocs.pending_transcript_pages(workspace_id, pending_id, page_no, text)
        VALUES ({b}, {pending}, 1, 'Own pending page');
        """);
    Assert.Single(await db.DocumentTags.ToListAsync());
    Assert.Single(await db.TranscriptPages.ToListAsync());
    Assert.Single(await db.PendingTranscriptPages.ToListAsync());
    Assert.Empty(await db.FileOperations.ToListAsync());
  }

  [Fact]
  public async Task EfConcurrencyTokenRejectsStaleTrackedDocumentUpdate()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await db.Database.MigrateAsync();
    var workspace = Guid.NewGuid();
    var topic = await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "version");
    var document = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
        VALUES ({document}, {workspace}, {topic}, 'Before', 'meeting', 'pending_upload')
        """);
    await using var peer = fixture.CreatePeer(db);
    var first = await db.Documents.SingleAsync(d => d.WorkspaceId == workspace && d.Id == document);
    var stale = await peer.Documents.SingleAsync(d => d.WorkspaceId == workspace && d.Id == document);
    Assert.Equal(first.Version, stale.Version);
    first.Title = "First writer";
    db.Entry(first).Property(d => d.Version).CurrentValue++;
    await db.SaveChangesAsync();
    stale.Title = "Stale writer";
    peer.Entry(stale).Property(d => d.Version).CurrentValue++;
    await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => peer.SaveChangesAsync());
    await using var observer = fixture.CreatePeer(db);
    var saved = await observer.Documents.AsNoTracking().SingleAsync();
    Assert.Equal("First writer", saved.Title);
    Assert.Equal(2L, saved.Version);
  }

  [Fact]
  public async Task ConcurrentTopicDeletionCannotRemoveNewDocumentAssignment()
  {
    await using var db = await fixture.CreateDatabaseAsync();
    await db.Database.MigrateAsync();
    var workspace = Guid.NewGuid();
    var systemTopic = await PostgresSeed.InsertUserAndWorkspace(db, Guid.NewGuid(), workspace, "assignment");
    var topic = Guid.NewGuid();
    var document = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name) VALUES ({topic}, {workspace}, 'Destination');
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, title, kind, availability)
        VALUES ({document}, {workspace}, {systemTopic}, 'Document', 'meeting', 'pending_upload')
        """);
    await using var peer = fixture.CreatePeer(db);
    await using var observer = fixture.CreatePeer(db);
    await using var first = await db.Database.BeginTransactionAsync();
    await using var second = await peer.Database.BeginTransactionAsync();
    var pid = await peer.Database.SqlQueryRaw<int>(
        """SELECT pg_backend_pid() AS "Value" """).SingleAsync();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE minidocs.documents SET topic_id = {topic}, version = version + 1
        WHERE workspace_id = {workspace} AND id = {document} AND version = 1
        """);
    var deletion = peer.Database.ExecuteSqlInterpolatedAsync($"""
        DELETE FROM minidocs.topics WHERE workspace_id = {workspace} AND id = {topic}
        """);
    try
    {
      await PostgresWait.ForBlockedAsync(observer, pid, deletion);
      await first.CommitAsync();
    }
    catch
    {
      await first.RollbackAsync();
      try { await deletion; } catch (PostgresException) { }
      throw;
    }
    var error = await Assert.ThrowsAsync<PostgresException>(() => deletion);
    Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
    Assert.Equal("FK_documents_topics_workspace_id_topic_id", error.ConstraintName);
    await second.RollbackAsync();
    Assert.True(await observer.Topics.AnyAsync(t => t.Id == topic));
    Assert.Equal(topic, (await observer.Documents.AsNoTracking().SingleAsync()).TopicId);
  }

  private static async Task ForeignKeyAsync(Func<Task> write, string constraint)
  {
    var error = await Assert.ThrowsAsync<PostgresException>(write);
    Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    Assert.Equal(constraint, error.ConstraintName);
  }

}
