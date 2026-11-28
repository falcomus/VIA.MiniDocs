using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Npgsql;
using VIA.MiniDocs.Application;
using VIA.MiniDocs.Domain;
using VIA.MiniDocs.Infrastructure;

namespace VIA.MiniDocs.Api.Security;

internal sealed record TopicDto(Guid Id, string Name, Guid? ParentId, bool IsSystem);
internal sealed record TagDto(Guid Id, string Name, string Description, string Color);
internal sealed record DocumentDto(Guid Id, string Title, string? FileName, string Kind, DateOnly? DocumentDate,
    Guid TopicId, Guid[] TagIds, int? PageCount, string Availability, DateTime? DeletedAt);
internal sealed record TopicItem(TopicDto Topic, string Etag);
internal sealed record TagItem(TagDto Tag, string Etag);
internal sealed record DocumentItem(DocumentDto Document, string Etag);
internal sealed record MovedDocument(DocumentDto Document, string Etag, Guid PreviousTopicId);
internal sealed record BatchMoveResult(IReadOnlyList<MovedDocument> Items);
internal sealed record BatchDocuments(IReadOnlyList<DocumentItem> Items);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record TopicCreate(string Name, Guid? ParentId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TopicPatch
{
  private Guid? parentId;
  private string? name;
  // Omission is a no-op; an explicit null violates the PATCH contract.
  public string? Name { get => name; init => name = value ?? throw new JsonException(); }
  public Guid? ParentId { get => parentId; init { parentId = value; HasParent = true; } }
  [JsonIgnore] public bool HasParent { get; private set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record TagCreate([property: JsonRequired] string Name,
    [property: JsonRequired] string Description, [property: JsonRequired] string Color);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TagPatch
{
  private string? name;
  private string? description;
  private string? color;
  public string? Name { get => name; init => name = value ?? throw new JsonException(); }
  public string? Description { get => description; init => description = value ?? throw new JsonException(); }
  public string? Color { get => color; init => color = value ?? throw new JsonException(); }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DocumentPatch
{
  private DateOnly? date;
  private string? title;
  private string? kind;
  private Guid? topicId;
  public string? Title { get => title; init => title = value ?? throw new JsonException(); }
  public string? Kind { get => kind; init => kind = value ?? throw new JsonException(); }
  public DateOnly? DocumentDate { get => date; init { date = value; HasDate = true; } }
  [JsonIgnore] public bool HasDate { get; private set; }
  public Guid? TopicId { get => topicId; init => topicId = value ?? throw new JsonException(); }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MoveItem(Guid Id, string Etag, Guid TargetTopicId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record VersionedDocument(Guid Id, string Etag);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BatchMove(MoveItem[] Items);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BatchTags(VersionedDocument[] Items, Guid TagId, [property: JsonRequired] bool On);
internal sealed class WorkspaceMetadataRoute;

internal static class WorkspaceMetadataHttp
{
  internal static void MapD6(this WebApplication app)
  {
    var routes = app.MapGroup("/api/v1").WithMetadata(new SessionRoute(), new WorkspaceMetadataRoute())
        .RequireAuthorization(WorkspacePolicies.ActiveWorkspace)
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(500).ProducesProblem(503);
    var topics = routes.MapGroup("/topics");
    topics.MapGet("", GetTopics).Produces<TopicItem[]>();
    topics.MapGet("/{id:guid}", GetTopic).Produces<TopicDto>().ProducesProblem(404);
    topics.MapPost("", CreateTopic).D6Write<TopicCreate>().Produces<TopicDto>(201).ProducesProblem(409);
    topics.MapPatch("/{id:guid}", UpdateTopic).D6Write<TopicPatch>().D6Versioned().Produces<TopicDto>();
    topics.MapDelete("/{id:guid}", DeleteTopic).WithMetadata(new ValidateSessionAntiforgery()).D6Versioned().Produces(204);
    var tags = routes.MapGroup("/tags");
    tags.MapGet("", GetTags).Produces<Page<TagItem>>();
    tags.MapGet("/{id:guid}", GetTag).Produces<TagDto>().ProducesProblem(404);
    tags.MapPost("", CreateTag).D6Write<TagCreate>().Produces<TagDto>(201).ProducesProblem(409);
    tags.MapPatch("/{id:guid}", UpdateTag).D6Write<TagPatch>().D6Versioned().Produces<TagDto>();
    tags.MapDelete("/{id:guid}", DeleteTag).WithMetadata(new ValidateSessionAntiforgery()).D6Versioned().Produces(204);
    var docs = routes.MapGroup("/documents");
    docs.MapGet("", GetDocuments).Produces<Page<DocumentItem>>();
    docs.MapGet("/{id:guid}", GetDocument).Produces<DocumentItem>().ProducesProblem(404);
    docs.MapPatch("/{id:guid}", UpdateDocument).D6Write<DocumentPatch>().D6Versioned().Produces<DocumentItem>();
    docs.MapPost("/batch-move", MoveDocuments).D6Write<BatchMove>().D6Versioned().Produces<BatchMoveResult>();
    docs.MapPost("/batch-tags", TagDocuments).D6Write<BatchTags>().D6Versioned().Produces<BatchDocuments>();
    docs.MapPut("/{id:guid}/tags/{tagId:guid}", AddDocumentTag).WithMetadata(new ValidateSessionAntiforgery()).D6Versioned().Produces<DocumentItem>();
    docs.MapDelete("/{id:guid}/tags/{tagId:guid}", RemoveDocumentTag).WithMetadata(new ValidateSessionAntiforgery()).D6Versioned().Produces<DocumentItem>();
  }

  private static RouteHandlerBuilder D6Write<T>(this RouteHandlerBuilder route) =>
      route.WithMetadata(new ValidateSessionAntiforgery(), new JsonRequest(typeof(T))).ProducesProblem(413).ProducesProblem(415);
  private static RouteHandlerBuilder D6Versioned(this RouteHandlerBuilder route) =>
      route.ProducesProblem(404).ProducesProblem(409).ProducesProblem(412).ProducesProblem(428);

  private static async Task<IResult> GetTopics(MiniDocsDbContext db, CancellationToken ct)
  {
    var rows = await db.Topics.AsNoTracking().OrderBy(t => t.Name).ThenBy(t => t.Id).ToListAsync(ct);
    var children = rows.ToLookup(t => t.ParentId ?? Guid.Empty);
    var stack = new Stack<Topic>(children[Guid.Empty].Reverse());
    var result = new List<TopicItem>(rows.Count);
    while (stack.TryPop(out var row))
    {
      result.Add(new(ToDto(row), Etag(row.Version)));
      foreach (var child in children[row.Id].Reverse()) stack.Push(child);
    }
    return Results.Ok(result);
  }

  private static async Task<IResult> GetTopic(Guid id, HttpContext ctx, MiniDocsDbContext db, CancellationToken ct) =>
      await db.Topics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct) is { } row
          ? WithTag(ToDto(row), row.Version) : Missing(ctx);

  private static async Task<IResult> CreateTopic(HttpContext ctx, MiniDocsDbContext db, CancellationToken ct)
  {
    var (input, error) = await BoundedJson.ReadAsync<TopicCreate>(ctx, ct);
    if (error is not null) return error;
    var name = input!.Name?.Trim(MetadataWhitespace);
    if (name is null || CodePointLength(name) is < 1 or > 255 || ContainsNul(name) || input.ParentId == Guid.Empty) return BoundedJson.Invalid(ctx);
    var id = Guid.NewGuid();
    return await WriteTopic(ctx, db, async (workspace, token) =>
        await WorkspaceMetadataPersistence.WriteTopicAsync(db, workspace, id, null, name, input.ParentId, true, token)
            ? WithTag(new TopicDto(id, name, input.ParentId, false), 1, $"/api/v1/topics/{id:D}") : Conflict(ctx), ct);
  }

  private static async Task<IResult> UpdateTopic(Guid id, HttpContext ctx, MiniDocsDbContext db, CancellationToken ct)
  {
    var (match, precondition) = ReadMatch(ctx);
    if (precondition is not null) return precondition;
    var (input, error) = await BoundedJson.ReadAsync<TopicPatch>(ctx, ct);
    if (error is not null) return error;
    var name = input!.Name?.Trim(MetadataWhitespace);
    if ((name is not null && (CodePointLength(name) is < 1 or > 255)) || ContainsNul(name) || input.ParentId == Guid.Empty) return BoundedJson.Invalid(ctx);
    return await WriteTopic(ctx, db, async (workspace, token) =>
    {
      var row = await db.Topics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, token);
      if (CheckVersion(ctx, row, match) is { } failure) return failure;
      if (!await WorkspaceMetadataPersistence.WriteTopicAsync(db, workspace, id, row!.Version, name, input.ParentId, input.HasParent, token))
        return Conflict(ctx);
      return await GetTopic(id, ctx, db, token);
    }, ct);
  }

  private static Task<IResult> DeleteTopic(Guid id, HttpContext ctx, MiniDocsDbContext db, CancellationToken ct) =>
      DeleteEntity(id, ctx, db, db.Topics, async (workspace, row, token) =>
      {
        if (row.SystemKey is not null) return Conflict(ctx);
        return await TopicPersistence.TryDeleteAsync(db, workspace, id, row.Version, token)
            ? Results.NoContent() : SessionHttp.Problem(ctx, 409, "topic_not_empty", "The topic is not empty.");
      }, ct);

  private static async Task<IResult> GetTags(HttpContext ctx, MiniDocsDbContext db, AdministratorCursor cursor, CancellationToken ct)
  {
    const string list = "workspace-tags";
    var workspace = db.AuthorizedWorkspaceId!.Value;
    if (!cursor.TryRead(ctx.Request, workspace, list, out var limit, out var after)) return InvalidCursor(ctx);
    var tags = db.Tags.AsNoTracking();
    if (after is { } id) tags = tags.Where(t => t.Id.CompareTo(id) > 0);
    var rows = await tags.OrderBy(t => t.Id).Take(limit + 1).ToListAsync(ct);
    return Results.Ok(new Page<TagItem>(rows.Take(limit).Select(t => new TagItem(ToDto(t), Etag(t.Version))).ToArray(),
        rows.Count > limit ? cursor.Create(workspace, list, limit, rows[limit - 1].Id) : null));
  }

  private static async Task<IResult> GetTag(Guid id, HttpContext ctx, MiniDocsDbContext db, CancellationToken ct) =>
      await db.Tags.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct) is { } row
          ? WithTag(ToDto(row), row.Version) : Missing(ctx);

  private static async Task<IResult> CreateTag(HttpContext ctx, MiniDocsDbContext db, CancellationToken ct)
  {
    var (input, error) = await BoundedJson.ReadAsync<TagCreate>(ctx, ct);
    if (error is not null) return error;
    if (input!.Name is null || input.Description is null || input.Color is null) return BoundedJson.Invalid(ctx);
    return await SaveTag(null, null, input.Name, input.Description, input.Color, ctx, db, ct);
  }

  private static async Task<IResult> UpdateTag(Guid id, HttpContext ctx, MiniDocsDbContext db, CancellationToken ct)
  {
    var (match, precondition) = ReadMatch(ctx);
    if (precondition is not null) return precondition;
    var (input, error) = await BoundedJson.ReadAsync<TagPatch>(ctx, ct);
    if (error is not null) return error;
    return await SaveTag(id, match, input!.Name, input.Description, input.Color, ctx, db, ct);
  }

  private static async Task<IResult> SaveTag(Guid? id, string? match, string? requestedName,
      string? requestedDescription, string? requestedColor, HttpContext ctx, MiniDocsDbContext db, CancellationToken ct)
  {
    if (ContainsNul(requestedName) || ContainsNul(requestedDescription) || ContainsNul(requestedColor))
      return BoundedJson.Invalid(ctx);
    return await Write(ctx, db, async (workspace, token) =>
    {
      var row = id is null ? null : await db.Tags.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, token);
      if (id is not null && CheckVersion(ctx, row, match) is { } failure) return failure;
      var name = requestedName?.Trim(MetadataWhitespace) ?? row?.Name;
      var description = requestedDescription ?? row?.Description;
      var color = requestedColor ?? row?.Color;
      if (name is null || CodePointLength(name) is < 1 or > 100 || description is null || CodePointLength(description) > 1000 ||
          color is not { Length: 7 } || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit))
        return BoundedJson.Invalid(ctx);
      var target = id ?? Guid.NewGuid();
      if (!await WorkspaceMetadataPersistence.WriteTagAsync(db, workspace, target, row?.Version, name, description, color, token))
        return Conflict(ctx);
      return id is null ? WithTag(new TagDto(target, name, description, color), 1, $"/api/v1/tags/{target:D}")
          : await GetTag(target, ctx, db, token);
    }, ct);
  }

  private static Task<IResult> DeleteTag(Guid id, HttpContext ctx, MiniDocsDbContext db, CancellationToken ct) =>
      DeleteEntity(id, ctx, db, db.Tags, async (workspace, row, token) =>
          await DocumentPersistence.TryDeleteTagAsync(db, workspace, id, row.Version, token)
              ? Results.NoContent() : Conflict(ctx), ct);

  private static async Task<IResult> DeleteEntity<T>(Guid id, HttpContext ctx, MiniDocsDbContext db, IQueryable<T> source,
      Func<Guid, T, CancellationToken, Task<IResult>> remove, CancellationToken ct) where T : WorkspaceRecord
  {
    var (match, error) = ReadMatch(ctx);
    if (error is not null) return error;
    return await Write(ctx, db, async (workspace, token) =>
    {
      var row = await source.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, token);
      return CheckVersion(ctx, row, match) is { } failure ? failure : await remove(workspace, row!, token);
    }, ct);
  }

  private static async Task<IResult> GetDocuments(HttpContext ctx, MiniDocsDbContext db, DocumentCursor cursor, CancellationToken ct)
  {
    var query = ctx.Request.Query;
    var sort = query["sort"].Count == 0 ? "date-desc" : query["sort"].ToString();
    if (sort is not ("date-desc" or "date-asc" or "title")) return BoundedJson.Invalid(ctx);
    Guid? topicId = null;
    if (query["topicId"].Count != 0)
    {
      if (!Guid.TryParse(query["topicId"], out var topic) || topic == Guid.Empty) return BoundedJson.Invalid(ctx);
      topicId = topic;
    }
    var tags = new List<Guid>();
    foreach (var value in query["tagIds"])
    {
      if (!Guid.TryParse(value, out var tag) || tag == Guid.Empty || tags.Count == 100)
        return BoundedJson.Invalid(ctx);
      tags.Add(tag);
    }
    var from = ReadDate(query["from"]);
    var to = ReadDate(query["to"]);
    if (query["from"].Count != 0 && from is null || query["to"].Count != 0 && to is null || from > to)
      return BoundedJson.Invalid(ctx);
    var criteria = $"{sort}:{topicId:D}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{string.Join(',', tags.Distinct().Order())}";
    var list = "workspace-documents:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(criteria)));
    var workspace = db.AuthorizedWorkspaceId!.Value;
    if (!cursor.TryRead(ctx.Request, workspace, list, sort, out var limit, out var after)) return InvalidCursor(ctx);
    var documents = WorkspaceDocumentWriteStore.Visible(db);
    if (topicId is { } target) documents = documents.Where(d => d.TopicId == target);
    if (from is { } first) documents = documents.Where(d => d.DocumentDate >= first);
    if (to is { } last) documents = documents.Where(d => d.DocumentDate <= last);
    foreach (var tag in tags.Distinct())
      documents = documents.Where(d => db.DocumentTags.Any(link => link.DocumentId == d.Id && link.TagId == tag));
    if (after is { } anchor)
    {
      // Continue from the original sort key, even if the anchor was renamed, moved or removed.
      var lastId = anchor.Id;
      if (sort == "title")
      {
        var title = anchor.Title!;
        documents = documents.Where(d => string.Compare(EF.Functions.Collate(d.Title, "de-x-icu"), title) > 0 ||
            EF.Functions.Collate(d.Title, "de-x-icu") == title && d.Id.CompareTo(lastId) > 0);
      }
      else if (sort == "date-asc")
      {
        var date = anchor.Date;
        documents = date is null
            ? documents.Where(d => d.DocumentDate != null || d.DocumentDate == null && d.Id.CompareTo(lastId) > 0)
            : documents.Where(d => d.DocumentDate > date || d.DocumentDate == date && d.Id.CompareTo(lastId) > 0);
      }
      else
      {
        var date = anchor.Date;
        documents = date is null
            ? documents.Where(d => d.DocumentDate == null && d.Id.CompareTo(lastId) > 0)
            : documents.Where(d => d.DocumentDate == null || d.DocumentDate < date || d.DocumentDate == date && d.Id.CompareTo(lastId) > 0);
      }
    }
    var ordered = sort switch
    {
      "title" => documents.OrderBy(d => EF.Functions.Collate(d.Title, "de-x-icu")),
      "date-asc" => documents.OrderBy(d => d.DocumentDate != null).ThenBy(d => d.DocumentDate),
      _ => documents.OrderBy(d => d.DocumentDate == null).ThenByDescending(d => d.DocumentDate)
    };
    var rows = await WorkspaceDocumentWriteStore.WithTags(db, ordered.ThenBy(d => d.Id).Take(limit + 1)).ToListAsync(ct);
    string? next = null;
    if (rows.Count > limit)
    {
      var lastRow = rows[limit - 1].Row;
      next = cursor.Create(workspace, list, limit, sort, lastRow.Id, lastRow.Title, lastRow.DocumentDate);
    }
    return Results.Ok(new Page<DocumentItem>(rows.Take(limit).Select(Item).ToArray(), next));
  }

  private static DateOnly? ReadDate(string? value) =>
      DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
          ? date : null;

  private static async Task<IResult> GetDocument(Guid id, HttpContext ctx, MiniDocsDbContext db, CancellationToken ct) =>
      await WorkspaceDocumentWriteStore.WithTags(db, WorkspaceDocumentWriteStore.Visible(db).Where(d => d.Id == id)).SingleOrDefaultAsync(ct) is { } data
          ? WithTag(Item(data), data.Row.Version) : Missing(ctx);

  private static async Task<IResult> UpdateDocument(Guid id, HttpContext ctx, WorkspaceDocumentWrites writes, CancellationToken ct)
  {
    var (match, precondition) = ReadMatch(ctx);
    if (precondition is not null) return precondition;
    var (input, error) = await BoundedJson.ReadAsync<DocumentPatch>(ctx, ct);
    if (error is not null) return error;
    var title = input!.Title?.Trim(MetadataWhitespace);
    if ((title is not null && (CodePointLength(title) is < 1 or > 255)) || ContainsNul(title) || input.Kind is not (null or "meeting" or "note") || input.TopicId == Guid.Empty)
      return BoundedJson.Invalid(ctx);
    var result = await writes.UpdateAsync(id, DecodeVersion(match),
        new(title, input.Kind, input.DocumentDate, input.HasDate, input.TopicId), ct);
    return result.Failure is { } failure ? DocumentFailure(ctx, failure) : WithTag(Item(result.Value!), result.Value!.Row.Version);
  }

  private static async Task<IResult> MoveDocuments(HttpContext ctx, WorkspaceDocumentWrites writes, CancellationToken ct)
  {
    var (input, error) = await BoundedJson.ReadAsync<BatchMove>(ctx, ct);
    if (error is not null) return error;
    if (input!.Items is not { Length: >= 1 and <= 100 } ||
        input.Items.Any(i => i is null || i.TargetTopicId == Guid.Empty || i.Id == Guid.Empty) ||
        input.Items.Select(i => i.Id).Distinct().Count() != input.Items.Length) return BoundedJson.Invalid(ctx);
    var items = input.Items.Select(i => new DocumentMove(i.Id, DecodeVersion(i.Etag), i.TargetTopicId)).ToArray();
    var result = await writes.MoveAsync(items, ct);
    if (result.Failure is { } failure) return DocumentFailure(ctx, failure);
    return Results.Ok(new BatchMoveResult(result.Value!.Select(movement =>
    {
      var changed = Item(movement.Snapshot);
      return new MovedDocument(changed.Document, changed.Etag, movement.PreviousTopicId);
    }).ToArray()));
  }

  private static async Task<IResult> TagDocuments(HttpContext ctx, WorkspaceDocumentWrites writes, CancellationToken ct)
  {
    var (input, error) = await BoundedJson.ReadAsync<BatchTags>(ctx, ct);
    if (error is not null) return error;
    if (input!.Items is not { Length: >= 1 and <= 100 } || input.TagId == Guid.Empty ||
        input.Items.Any(i => i is null || i.Id == Guid.Empty) ||
        input.Items.Select(i => i.Id).Distinct().Count() != input.Items.Length) return BoundedJson.Invalid(ctx);
    return await ChangeTags(ctx, writes, input, single: false, ct);
  }

  private static Task<IResult> AddDocumentTag(Guid id, Guid tagId, HttpContext ctx, WorkspaceDocumentWrites writes, CancellationToken ct) =>
      ChangeSingleTag(id, tagId, true, ctx, writes, ct);
  private static Task<IResult> RemoveDocumentTag(Guid id, Guid tagId, HttpContext ctx, WorkspaceDocumentWrites writes, CancellationToken ct) =>
      ChangeSingleTag(id, tagId, false, ctx, writes, ct);
  private static Task<IResult> ChangeSingleTag(Guid id, Guid tagId, bool on, HttpContext ctx, WorkspaceDocumentWrites writes, CancellationToken ct)
  {
    var (match, error) = ReadMatch(ctx);
    return error is not null ? Task.FromResult(error) : ChangeTags(ctx, writes, new([new(id, match!)], tagId, on), single: true, ct);
  }

  private static async Task<IResult> ChangeTags(HttpContext ctx, WorkspaceDocumentWrites writes, BatchTags input, bool single, CancellationToken ct)
  {
    var items = input.Items.Select(i => new DocumentVersionedId(i.Id, DecodeVersion(i.Etag))).ToArray();
    var result = await writes.ChangeTagsAsync(items, input.TagId, input.On, ct);
    if (result.Failure is { } failure) return DocumentFailure(ctx, failure);
    var changes = result.Value!;
    if (single) return WithTag(Item(changes[0]), changes[0].Row.Version);
    return Results.Ok(new BatchDocuments(changes.Select(Item).ToArray()));
  }

  // Preserve the strong-validator semantics and precedence used by the original D6 routes.
  // A well-formed but unknown opaque ETag is a mismatch only if the resource exists.
  private static DocumentVersionCondition DecodeVersion(string? value)
  {
    if (value is null) return new(null, DocumentWriteFailure.RequiredVersion);
    if (!Strong(value)) return new(null, DocumentWriteFailure.InvalidVersion);
    return value.Length == 18 && long.TryParse(value.AsSpan(1, 16), NumberStyles.AllowHexSpecifier,
        CultureInfo.InvariantCulture, out var version) && Etag(version) == value ? new(version, null) : new(null, null);
  }

  private static IResult DocumentFailure(HttpContext ctx, DocumentWriteFailure failure) => failure switch
  {
    DocumentWriteFailure.RequiredVersion => Required(ctx),
    DocumentWriteFailure.InvalidVersion => BoundedJson.Invalid(ctx),
    DocumentWriteFailure.Missing => Missing(ctx),
    DocumentWriteFailure.VersionMismatch => SessionHttp.Problem(ctx, 412, "version_mismatch", "The resource version changed."),
    DocumentWriteFailure.Conflict => Conflict(ctx),
    _ => throw new InvalidOperationException("Unexpected document write failure.")
  };

  private static async Task<IResult> WriteTopic(HttpContext ctx, MiniDocsDbContext db,
      Func<Guid, CancellationToken, Task<IResult>> action, CancellationToken ct)
  {
    try { return await Write(ctx, db, action, ct); }
    catch (Exception ex) when (ex.GetBaseException() is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_topics_sibling_name" })
    {
      return SessionHttp.Problem(ctx, 409, "topic_name_conflict", "A sibling topic already has that name.");
    }
    catch (Exception ex) when (ex.GetBaseException() is PostgresException
        { SqlState: PostgresErrorCodes.CheckViolation, ConstraintName: "ck_topics_reserved_root_name" })
    {
      return SessionHttp.Problem(ctx, 409, "topic_name_reserved", "That root topic name is reserved.");
    }
  }

  private static async Task<IResult> Write(HttpContext ctx, MiniDocsDbContext db,
      Func<Guid, CancellationToken, Task<IResult>> action, CancellationToken ct)
  {
    var workspace = db.AuthorizedWorkspaceId!.Value;
    IResult? rejected = null;
    var result = await LocalWriteTransaction.ExecuteAsync(db, async token =>
        await WorkspaceMetadataPersistence.LockAsync(db, workspace, token) ? await action(workspace, token) : Conflict(ctx),
        outcome =>
        {
          if (outcome is not IStatusCodeHttpResult { StatusCode: >= 400 }) return true;
          rejected = outcome;
          return false;
        }, IsolationLevel.ReadCommitted, ct);
    return result.Status == LocalWriteStatus.Committed ? result.Value! : rejected ?? Conflict(ctx);
  }

  private static (string? Value, IResult? Error) ReadMatch(HttpContext ctx)
  {
    var values = ctx.Request.Headers.IfMatch;
    return values.Count == 0 ? (null, Required(ctx)) :
        values.Count != 1 || !Strong(values[0]) ? (null, BoundedJson.Invalid(ctx)) : (values[0], null);
  }

  private static bool Strong(string? value) =>
      EntityTagHeaderValue.TryParse(value, out var tag) && !tag.IsWeak && tag != EntityTagHeaderValue.Any;
  private static IResult? CheckVersion(HttpContext ctx, WorkspaceRecord? row, string? value) =>
      value is null ? Required(ctx) : !Strong(value) ? BoundedJson.Invalid(ctx) : row is null ? Missing(ctx) :
      value != Etag(row.Version) ? SessionHttp.Problem(ctx, 412, "version_mismatch", "The resource version changed.") : null;
  private static IResult Required(HttpContext ctx) =>
      SessionHttp.Problem(ctx, 428, "precondition_required", "A strong version is required.");
  private static IResult Missing(HttpContext ctx) =>
      SessionHttp.Problem(ctx, 404, "resource_not_found", "The resource was not found.");
  private static IResult Conflict(HttpContext ctx) =>
      SessionHttp.Problem(ctx, 409, "state_conflict", "The resource cannot be changed in its current state.");
  private static IResult InvalidCursor(HttpContext ctx) =>
      SessionHttp.Problem(ctx, 400, "cursor_invalid", "Invalid page parameters.");
  // D6 normalization v1 follows ECMAScript trim, not .NET's Unicode whitespace set.
  // In particular FEFF is trimmed and NEL (0085) is not. Keep the SQL boundary in sync.
  private static readonly char[] MetadataWhitespace =
      ("\u0009\u000A\u000B\u000C\u000D\u0020\u00A0\u1680\u2000\u2001\u2002\u2003" +
      "\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF").ToCharArray();

  // PostgreSQL text cannot represent U+0000; reject it before any database call.
  // PostgreSQL char_length and OpenAPI maxLength count Unicode code points, not UTF-16 units.
  private static int CodePointLength(string value)
  {
    var length = 0;
    foreach (var _ in value.EnumerateRunes()) length++;
    return length;
  }

  private static bool ContainsNul(string? value) => value?.Contains('\0') == true;
  private static string Etag(long version) => $"\"{version:x16}\"";
  private static TopicDto ToDto(Topic t) => new(t.Id, t.Name, t.ParentId, t.SystemKey is not null);
  private static TagDto ToDto(Tag t) => new(t.Id, t.Name, t.Description, t.Color);
  private static DocumentItem Item(DocumentSnapshot data) => new(new(data.Row.Id, data.Row.Title, data.Row.FileName,
      data.Row.Kind == DocumentKind.Meeting ? "meeting" : "note", data.Row.DocumentDate, data.Row.TopicId, data.Tags,
      data.Row.PageCount, data.Row.Availability switch
      {
        DocumentAvailability.PendingUpload => "pending_upload",
        DocumentAvailability.UploadFailed => "upload_failed",
        DocumentAvailability.Available => "available",
        DocumentAvailability.Missing => "missing",
        _ => throw new InvalidOperationException("Unexpected visible availability.")
      }, data.Row.DeletedAt), Etag(data.Row.Version));

  private static IResult WithTag<T>(T value, long version, string? location = null) =>
      new TaggedResult(location is null ? Results.Ok(value) : Results.Created(location, value), Etag(version));
  private sealed record TaggedResult(IResult Inner, string Etag) : IResult
  {
    public Task ExecuteAsync(HttpContext context)
    {
      context.Response.Headers.ETag = Etag;
      return Inner.ExecuteAsync(context);
    }
  }
}
