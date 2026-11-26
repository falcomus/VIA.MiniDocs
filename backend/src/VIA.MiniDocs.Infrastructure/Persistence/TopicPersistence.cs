namespace VIA.MiniDocs.Infrastructure;

public static class TopicPersistence
{
  public static Task<bool> TryMoveAsync(MiniDocsDbContext db, Guid workspace, Guid topic, long version,
      Guid? parent, CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_move_topic({workspace}, {topic}, {version}, {parent}::uuid)
          """, cancellationToken);

  public static Task<bool> TryDeleteAsync(MiniDocsDbContext db, Guid workspace, Guid topic, long version,
      CancellationToken cancellationToken = default) =>
      D2Commands.InvokeAsync<bool>(db, $"""
          SELECT minidocs.command_delete_topic({workspace}, {topic}, {version})
          """, cancellationToken);
}
