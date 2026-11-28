using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Application;

public enum DocumentWriteFailure { RequiredVersion, InvalidVersion, Missing, VersionMismatch, Conflict }

/// <summary>A version condition decoded at the transport boundary; an opaque, well-formed
/// but noncanonical validator has no matching numeric version.</summary>
public readonly record struct DocumentVersionCondition(long? Version, DocumentWriteFailure? Invalid);

public sealed record DocumentSnapshot(Document Row, Guid[] Tags);
public sealed record DocumentMovement(DocumentSnapshot Snapshot, Guid PreviousTopicId);
public sealed record DocumentMetadataChange(string? Title, string? Kind, DateOnly? Date, bool SetDate, Guid? TopicId);
public sealed record DocumentMove(Guid Id, DocumentVersionCondition Version, Guid TargetTopicId);
public sealed record DocumentVersionedId(Guid Id, DocumentVersionCondition Version);

public sealed record DocumentWriteResult<T>(T? Value, DocumentWriteFailure? Failure) where T : class
{
  public static DocumentWriteResult<T> Success(T value) => new(value, null);
  public static DocumentWriteResult<T> Reject(DocumentWriteFailure failure) => new(null, failure);
}

/// <summary>Local persistence port bound to the authenticated workspace. ExecuteAsync owns the
/// transaction and workspace lock, commits only success, and exposes success only after commit.</summary>
public interface IWorkspaceDocumentWriteStore
{
  Task<DocumentWriteResult<T>> ExecuteAsync<T>(Func<CancellationToken, Task<DocumentWriteResult<T>>> work,
      CancellationToken ct) where T : class;
  Task<Document?> FindAsync(Guid id, CancellationToken ct);
  Task<DocumentSnapshot?> ReadAsync(Guid id, CancellationToken ct);
  Task<bool> TopicExistsAsync(Guid id, CancellationToken ct);
  Task<bool> TagExistsAsync(Guid id, CancellationToken ct);
  Task<bool> MoveAsync(Guid id, long version, Guid topic, CancellationToken ct);
  Task<bool> SetTagAsync(Guid id, long version, Guid tag, bool on, CancellationToken ct);
  Task<bool> WriteMetadataAsync(Guid id, long version, DocumentMetadataChange change, CancellationToken ct);
}

/// <summary>Coordinates compound metadata changes and atomic batches without HTTP or EF dependencies.</summary>
public sealed class WorkspaceDocumentWrites(IWorkspaceDocumentWriteStore store)
{
  public Task<DocumentWriteResult<DocumentSnapshot>> UpdateAsync(Guid id, DocumentVersionCondition version,
      DocumentMetadataChange change, CancellationToken ct) => store.ExecuteAsync(async token =>
  {
    var row = await store.FindAsync(id, token);
    if (CheckVersion(row, version) is { } failure) return DocumentWriteResult<DocumentSnapshot>.Reject(failure);
    if (change.TopicId is { } topic && row!.TopicId != topic)
    {
      if (!await store.TopicExistsAsync(topic, token)) return DocumentWriteResult<DocumentSnapshot>.Reject(DocumentWriteFailure.Missing);
      if (!await store.MoveAsync(id, row.Version, topic, token)) return DocumentWriteResult<DocumentSnapshot>.Reject(DocumentWriteFailure.Conflict);
      row = await store.FindAsync(id, token) ?? throw new InvalidOperationException("Moved document is missing.");
    }
    if (!await store.WriteMetadataAsync(id, row!.Version, change, token))
      return DocumentWriteResult<DocumentSnapshot>.Reject(DocumentWriteFailure.Conflict);
    return await store.ReadAsync(id, token) is { } updated
        ? DocumentWriteResult<DocumentSnapshot>.Success(updated) : DocumentWriteResult<DocumentSnapshot>.Reject(DocumentWriteFailure.Missing);
  }, ct);

  public Task<DocumentWriteResult<IReadOnlyList<DocumentMovement>>> MoveAsync(IReadOnlyList<DocumentMove> items,
      CancellationToken ct) => store.ExecuteAsync(async token =>
  {
    var movements = new List<DocumentMovement>(items.Count);
    foreach (var item in items)
    {
      var row = await store.FindAsync(item.Id, token);
      if (CheckVersion(row, item.Version) is { } failure) return DocumentWriteResult<IReadOnlyList<DocumentMovement>>.Reject(failure);
      if (!await store.TopicExistsAsync(item.TargetTopicId, token)) return DocumentWriteResult<IReadOnlyList<DocumentMovement>>.Reject(DocumentWriteFailure.Missing);
      if (!await store.MoveAsync(item.Id, row!.Version, item.TargetTopicId, token))
        return DocumentWriteResult<IReadOnlyList<DocumentMovement>>.Reject(DocumentWriteFailure.Conflict);
      var changed = await store.ReadAsync(item.Id, token) ?? throw new InvalidOperationException("Moved document is missing.");
      movements.Add(new(changed, row.TopicId));
    }
    return DocumentWriteResult<IReadOnlyList<DocumentMovement>>.Success(movements);
  }, ct);

  public Task<DocumentWriteResult<IReadOnlyList<DocumentSnapshot>>> ChangeTagsAsync(IReadOnlyList<DocumentVersionedId> items,
      Guid tag, bool on, CancellationToken ct) => store.ExecuteAsync(async token =>
  {
    if (!await store.TagExistsAsync(tag, token)) return DocumentWriteResult<IReadOnlyList<DocumentSnapshot>>.Reject(DocumentWriteFailure.Missing);
    var changes = new List<DocumentSnapshot>(items.Count);
    foreach (var item in items)
    {
      var row = await store.FindAsync(item.Id, token);
      if (CheckVersion(row, item.Version) is { } failure) return DocumentWriteResult<IReadOnlyList<DocumentSnapshot>>.Reject(failure);
      if (!await store.SetTagAsync(item.Id, row!.Version, tag, on, token))
        return DocumentWriteResult<IReadOnlyList<DocumentSnapshot>>.Reject(DocumentWriteFailure.Conflict);
      changes.Add(await store.ReadAsync(item.Id, token) ?? throw new InvalidOperationException("Changed document is missing."));
    }
    return DocumentWriteResult<IReadOnlyList<DocumentSnapshot>>.Success(changes);
  }, ct);

  private static DocumentWriteFailure? CheckVersion(Document? row, DocumentVersionCondition version) =>
      version.Invalid ?? (row is null ? DocumentWriteFailure.Missing :
          version.Version != row.Version ? DocumentWriteFailure.VersionMismatch : (DocumentWriteFailure?)null);
}
