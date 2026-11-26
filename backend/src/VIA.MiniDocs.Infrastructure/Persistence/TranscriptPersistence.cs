namespace VIA.MiniDocs.Infrastructure;

public sealed record TranscriptPageValue(int PageNo, string Text, bool Edited);

/// <summary>Calls the versioned transcript commands inside the caller's local transaction.</summary>
public static class TranscriptPersistence
{
  public static Task<bool> TryAssignPendingAsync(MiniDocsDbContext db, Guid workspace, Guid document,
      long revision, Guid pending, long pendingVersion, bool replace, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_assign_pending_transcript(
            {workspace}, {document}, {revision}, {pending}, {pendingVersion}, {replace})
          """, cancellationToken);

  public static async Task<bool> TryReplaceAsync(MiniDocsDbContext db, Guid workspaceId,
      Guid documentId, long expectedRevision, bool present, bool edited,
      IReadOnlyCollection<TranscriptPageValue> pages, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(pages);
    var replacement = pages.OrderBy(page => page.PageNo).ToArray();
    if (expectedRevision < 0 || (!present && (replacement.Length != 0 || edited)) ||
        replacement.Any(page => page.PageNo < 1 || page.Text is null) ||
        replacement.Select(page => page.PageNo).Distinct().Count() != replacement.Length)
      throw new ArgumentException("Invalid transcript replacement.");

    var numbers = replacement.Select(page => page.PageNo).ToArray();
    var texts = replacement.Select(page => page.Text).ToArray();
    var edits = replacement.Select(page => page.Edited).ToArray();
    return await D2Commands.InvokeAsync<bool>(db, $"""
        SELECT minidocs.command_replace_transcript(
          {workspaceId}, {documentId}, {expectedRevision}, {present}, {edited}, {numbers}, {texts}, {edits})
        """, cancellationToken);
  }

  public static Task<bool> TryEditPageAsync(MiniDocsDbContext db, Guid workspaceId,
      Guid documentId, long expectedRevision, int pageNo, long expectedPageVersion,
      string text, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(text);
    if (expectedRevision < 0 || expectedPageVersion < 1 || pageNo < 1)
      throw new ArgumentException("Invalid transcript page or version.");
    return D2Commands.InvokeAsync<bool>(db, $"""
        SELECT minidocs.command_edit_transcript_page(
          {workspaceId}, {documentId}, {expectedRevision}, {pageNo}, {expectedPageVersion}, {text})
        """, cancellationToken);
  }
}
