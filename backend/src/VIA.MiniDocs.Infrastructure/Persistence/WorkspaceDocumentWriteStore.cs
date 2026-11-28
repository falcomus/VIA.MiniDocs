using System.Data;
using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Application;
using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

/// <summary>Executes application document workflows through the existing D6 local write boundary.</summary>
public sealed class WorkspaceDocumentWriteStore(MiniDocsDbContext db) : IWorkspaceDocumentWriteStore
{
  private Guid Workspace => db.AuthorizedWorkspaceId ?? throw new InvalidOperationException("An active workspace is required.");

  public async Task<DocumentWriteResult<T>> ExecuteAsync<T>(Func<CancellationToken, Task<DocumentWriteResult<T>>> work,
      CancellationToken ct) where T : class
  {
    var workspace = Workspace;
    DocumentWriteResult<T>? rejected = null;
    var result = await LocalWriteTransaction.ExecuteAsync(db, async token =>
        await WorkspaceMetadataPersistence.LockAsync(db, workspace, token) ? await work(token) :
            DocumentWriteResult<T>.Reject(DocumentWriteFailure.Conflict),
        outcome =>
        {
          if (outcome.Failure is null) return true;
          rejected = outcome;
          return false;
        }, IsolationLevel.ReadCommitted, ct);
    return result.Status == LocalWriteStatus.Committed ? result.Value! :
        rejected ?? DocumentWriteResult<T>.Reject(DocumentWriteFailure.Conflict);
  }

  public static IQueryable<Document> Visible(MiniDocsDbContext db) =>
      db.Documents.AsNoTracking().Where(row => row.DeletedAt == null && row.Availability != DocumentAvailability.DeletePending);

  public static IQueryable<DocumentSnapshot> WithTags(MiniDocsDbContext db, IQueryable<Document> rows) =>
      rows.Select(row => new DocumentSnapshot(row, db.DocumentTags.Where(link => link.DocumentId == row.Id)
          .OrderBy(link => link.TagId).Select(link => link.TagId).ToArray()));

  public Task<Document?> FindAsync(Guid id, CancellationToken ct) => Visible(db).SingleOrDefaultAsync(d => d.Id == id, ct);
  public Task<DocumentSnapshot?> ReadAsync(Guid id, CancellationToken ct) => WithTags(db, Visible(db).Where(d => d.Id == id)).SingleOrDefaultAsync(ct);
  public Task<bool> TopicExistsAsync(Guid id, CancellationToken ct) => db.Topics.AnyAsync(t => t.Id == id, ct);
  public Task<bool> TagExistsAsync(Guid id, CancellationToken ct) => db.Tags.AnyAsync(t => t.Id == id, ct);
  public Task<bool> MoveAsync(Guid id, long version, Guid topic, CancellationToken ct) =>
      DocumentPersistence.TryMoveAsync(db, Workspace, id, version, topic, ct);
  public Task<bool> SetTagAsync(Guid id, long version, Guid tag, bool on, CancellationToken ct) =>
      DocumentPersistence.TrySetTagAsync(db, Workspace, id, version, tag, on, ct);
  public Task<bool> WriteMetadataAsync(Guid id, long version, DocumentMetadataChange change, CancellationToken ct) =>
      WorkspaceMetadataPersistence.WriteDocumentAsync(db, Workspace, id, version, change.Title, change.Kind, change.Date, change.SetDate, ct);
}
