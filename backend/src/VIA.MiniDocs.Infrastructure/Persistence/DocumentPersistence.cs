using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

public static class DocumentPersistence
{
  public static Task<bool> TryRenameAsync(MiniDocsDbContext db, Guid workspace, Guid document, long version,
      string title, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_rename_document({workspace}, {document}, {version}, {title})
          """, cancellationToken);

  public static Task<bool> TryChangePageCountAsync(MiniDocsDbContext db, Guid workspace, Guid document, long version,
      int? count, PageCountSource? source, CancellationToken cancellationToken = default)
  {
    var storedSource = source is PageCountSource value
        ? (string)DomainTextConverters.PageCountSource.ConvertToProvider(value)!
        : null;
    return D2Commands.InvokeAsync<bool>(db, $"""
        SELECT minidocs.command_change_page_count({workspace}, {document}, {version}, {count}::integer, {storedSource}::text)
        """, cancellationToken);
  }

  public static Task<bool> TryMoveAsync(MiniDocsDbContext db, Guid workspace, Guid document, long version,
      Guid topic, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_move_document({workspace}, {document}, {version}, {topic})
          """, cancellationToken);

  public static Task<bool> TrySetTagAsync(MiniDocsDbContext db, Guid workspace, Guid document, long version,
      Guid tag, bool present, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_set_document_tag({workspace}, {document}, {version}, {tag}, {present})
          """, cancellationToken);

  public static Task<bool> TryDeleteTagAsync(MiniDocsDbContext db, Guid workspace, Guid tag, long version,
      CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_delete_tag({workspace}, {tag}, {version})
          """, cancellationToken);
}
