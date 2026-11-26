using Microsoft.EntityFrameworkCore;
using Npgsql;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class PostgresFinalHardeningTests
{
  [Fact]
  public async Task PendingConcurrentAssignmentKeepsOneCopyAndOneRevision()
  {
    await using var db = await OpenAsync();
    await using var first = Fixture.CreateRuntimePeer(db);
    await using var second = Fixture.CreateRuntimePeer(db);
    var (workspace, topic) = await ActiveWorkspaceAsync(db, first);
    var upload = await ReserveUploadAsync(first, workspace, topic);
    Assert.True(await ChangeAsync(first, ct => DocumentPersistence.TryChangePageCountAsync(
        first, workspace, upload.Document, 1, 2, PageCountSource.Viewer, ct)));
    var pending = await SeedPendingAsync(db, workspace, multiplePages: true);

    Assert.False(await ChangeAsync(first, ct => TranscriptPersistence.TryAssignPendingAsync(
        first, workspace, upload.Document, 0, pending, 2, false, ct)));
    Assert.False(await ChangeAsync(first, ct => TranscriptPersistence.TryAssignPendingAsync(
        first, workspace, upload.Document, 1, pending, 1, false, ct)));
    Assert.Equal(2, await db.PendingTranscriptPages.CountAsync());

    await using var held = await first.Database.BeginTransactionAsync();
    Assert.True(await TranscriptPersistence.TryAssignPendingAsync(first, workspace, upload.Document, 0, pending, 1, false));
    var pid = 0;
    var competing = LocalWriteTransaction.ExecuteAsync(second, async ct =>
    {
      Volatile.Write(ref pid, await second.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync(ct));
      return await TranscriptPersistence.TryAssignPendingAsync(second, workspace, upload.Document, 0, pending, 1, false, ct);
    }, value => value);
    await PostgresWait.ForBlockedAsync(db, () => Volatile.Read(ref pid), competing);
    await held.CommitAsync();

    Assert.Equal(LocalWriteStatus.Rejected, (await competing).Status);
    var head = await db.PendingTranscripts.AsNoTracking().SingleAsync();
    Assert.Equal(PendingTranscriptStatus.Matched, head.Status);
    Assert.Equal(2L, head.Version);
    Assert.Equal("pending.pdf", head.FileName);
    Assert.Null(head.SourceName);
    Assert.Null(head.MetaTitle);
    Assert.Null(head.MetaTags);
    Assert.Empty(await db.PendingTranscriptPages.AsNoTracking().ToListAsync());
    var pages = await db.TranscriptPages.AsNoTracking().OrderBy(p => p.PageNo).ToArrayAsync();
    Assert.Equal(new[] { "One", "Two" }, pages.Select(p => p.Text));
    var doc = await db.Documents.AsNoTracking().SingleAsync();
    Assert.Equal(1L, doc.TranscriptRevision);
    Assert.Equal(3L, doc.Version);
    Assert.True(doc.TranscriptPresent);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task FailedPendingAssignmentRetainsOriginalPagesAndDocument(bool failAtHeadUpdate)
  {
    await using var db = await OpenAsync();
    await using var runtime = Fixture.CreateRuntimePeer(db);
    var (workspace, topic) = await ActiveWorkspaceAsync(db, runtime);
    var upload = await ReserveUploadAsync(runtime, workspace, topic);
    Assert.True(await ChangeAsync(runtime, ct => TranscriptPersistence.TryReplaceAsync(
        runtime, workspace, upload.Document, 0, true, true, [new(1, "Original", true)], ct)));
    var pending = await SeedPendingAsync(db, workspace, multiplePages: !failAtHeadUpdate);

    if (failAtHeadUpdate)
    {
      await db.Database.ExecuteSqlRawAsync("""
          CREATE FUNCTION minidocs.test_pending_head_fault() RETURNS trigger
          LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
          BEGIN
            IF EXISTS (SELECT 1 FROM minidocs.pending_transcript_pages WHERE pending_id = OLD.id)
            THEN RAISE EXCEPTION 'Expected pages to have been deleted before this fault'; END IF;
            RAISE EXCEPTION 'Injected failure after deleting pending pages';
          END;
          $$;
          CREATE TRIGGER test_pending_head_fault BEFORE UPDATE ON minidocs.pending_transcripts
            FOR EACH ROW EXECUTE FUNCTION minidocs.test_pending_head_fault()
          """);
      var error = await Assert.ThrowsAsync<PostgresException>(() => ChangeAsync(runtime, ct =>
          TranscriptPersistence.TryAssignPendingAsync(runtime, workspace, upload.Document, 1, pending, 1, true, ct)));
      Assert.Equal("Injected failure after deleting pending pages", error.MessageText);
    }
    else
    {
      await Assert.ThrowsAsync<ArgumentException>(() => ChangeAsync(runtime, ct =>
          TranscriptPersistence.TryAssignPendingAsync(runtime, workspace, upload.Document, 1, pending, 1, true, ct)));
    }

    var head = await db.PendingTranscripts.AsNoTracking().SingleAsync();
    Assert.Equal(PendingTranscriptStatus.Pending, head.Status);
    Assert.Equal(1L, head.Version);
    Assert.Equal("source.md", head.SourceName);
    Assert.Equal(failAtHeadUpdate ? 1 : 2, await db.PendingTranscriptPages.CountAsync());
    var page = await db.TranscriptPages.AsNoTracking().SingleAsync();
    Assert.Equal("Original", page.Text);
    Assert.Equal(1L, page.Version);
    Assert.Equal(1L, (await db.Documents.AsNoTracking().SingleAsync()).TranscriptRevision);
    var forbidden = await Assert.ThrowsAsync<PostgresException>(() => runtime.Database.ExecuteSqlRawAsync(
        "DELETE FROM minidocs.pending_transcript_pages"));
    Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, forbidden.SqlState);
  }

  private static async Task<Guid> SeedPendingAsync(MiniDocsDbContext db, Guid workspace, bool multiplePages)
  {
    // Pending creation/import is still D7. Seed its valid, unassigned state here.
    var id = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.pending_transcripts(id, workspace_id, file_name, source_name, meta_title, meta_tags, imported_at, status)
        VALUES ({id}, {workspace}, 'pending.pdf', 'source.md', 'Source title', 'Example', clock_timestamp(), 'pending');
        INSERT INTO minidocs.pending_transcript_pages(workspace_id, pending_id, page_no, text)
        VALUES ({workspace}, {id}, 1, 'One');
        """);
    if (multiplePages)
      await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO minidocs.pending_transcript_pages(workspace_id, pending_id, page_no, text)
          VALUES ({workspace}, {id}, 2, 'Two')
          """);
    return id;
  }
}
