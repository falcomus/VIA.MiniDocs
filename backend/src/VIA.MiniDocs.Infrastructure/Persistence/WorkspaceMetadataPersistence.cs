namespace VIA.MiniDocs.Infrastructure;

public static class WorkspaceMetadataPersistence
{
  public static Task<bool> LockAsync(MiniDocsDbContext db, Guid workspace, CancellationToken token) =>
      D2Commands.InvokeAsync<bool>(db, $"SELECT minidocs.command_d6_lock({workspace})", token);

  public static Task<bool> WriteTopicAsync(MiniDocsDbContext db, Guid workspace, Guid id, long? version,
      string? name, Guid? parent, bool move, CancellationToken token) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_d6_topic({workspace}, {id}, {version}::bigint, {name}::text, {parent}::uuid, {move})
          """, token);

  public static Task<bool> WriteTagAsync(MiniDocsDbContext db, Guid workspace, Guid id, long? version,
      string name, string description, string color, CancellationToken token) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_d6_tag({workspace}, {id}, {version}::bigint, {name}, {description}, {color})
          """, token);

  public static Task<bool> WriteDocumentAsync(MiniDocsDbContext db, Guid workspace, Guid id, long version,
      string? title, string? kind, DateOnly? date, bool setDate, CancellationToken token) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_d6_document({workspace}, {id}, {version}, {title}::text, {kind}::text, {date}::date, {setDate})
          """, token);
}
