using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  private static async Task<Guid> InsertMetadataDocumentAsync(MiniDocsDbContext db, Guid workspace, string title)
  {
    var id = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.documents(id, workspace_id, topic_id, via_file_id, title, kind, availability)
        SELECT {id}, {workspace}, id, {Guid.NewGuid()}, {title}, 'note', 'available'
        FROM minidocs.topics WHERE workspace_id = {workspace} AND system_key = 'UNASSIGNED'
        """);
    return id;
  }

  [Fact]
  public async Task MetadataHttpBatchesVersionUndoAndScopeAreEnforced()
  {
    await using var database = await CreateDatabaseAsync();
    await PostgresTestDatabase.VerifyPermissionsAsync(database);
    var password = Guid.NewGuid().ToString("N");
    var workspace1 = Guid.NewGuid();
    var workspace2 = Guid.NewGuid();
    await CreateUserAsync(database, "m-one", password, activeWorkspace: workspace1);
    await CreateUserAsync(database, "m-two", password, activeWorkspace: workspace2);
    var one = await InsertMetadataDocumentAsync(database, workspace1, "Own one");
    var two = await InsertMetadataDocumentAsync(database, workspace1, "Own two");
    var foreign = await InsertMetadataDocumentAsync(database, workspace2, "Foreign");
    var foreignTopic = await database.Topics.ObserveAllWorkspaces().Where(t => t.WorkspaceId == workspace2)
        .Select(t => t.Id).SingleAsync();
    var foreignTag = Guid.NewGuid();
    await database.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.tags(id, workspace_id, name, name_key, description, color)
        VALUES ({foreignTag}, {workspace2}, 'Private', 'private', '', '#111111')
        """);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    const string root = "/api/v1";
    var anon = await GetAntiforgeryAsync(client);
    var session = Cookie(await LoginAsync(client, "m-one", password, anon.Cookie, anon.Token), SessionName);
    var csrf = await GetAntiforgeryAsync(client, session);

    async Task<HttpResponseMessage> Request(HttpMethod method, string route, object? body = null, string? match = null)
    {
      using var request = new HttpRequestMessage(method, root + route);
      AddCookies(request, session, csrf.Cookie);
      if (method != HttpMethod.Get) request.Headers.Add(Header, csrf.Token);
      if (body is not null) request.Content = JsonContent.Create(body);
      if (match is not null) request.Headers.TryAddWithoutValidation("If-Match", match);
      return await client.SendAsync(request);
    }

    using var topicsResult = await Request(HttpMethod.Get, "/topics");
    topicsResult.EnsureSuccessStatusCode();
    var system = (await topicsResult.Content.ReadFromJsonAsync<JsonElement>())[0];
    var inbox = system.GetProperty("topic").GetProperty("id").GetGuid();
    using var protectedTopic = await Request(HttpMethod.Delete, $"/topics/{inbox}", match: system.GetProperty("etag").GetString());
    Assert.Equal(HttpStatusCode.Conflict, protectedTopic.StatusCode);
    using var created = await Request(HttpMethod.Post, "/topics", new { name = "  Neues Thema  ", parentId = (Guid?)null });
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var target = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    using var cycle = await Request(HttpMethod.Patch, $"/topics/{target}", new { parentId = target }, created.Headers.ETag!.Tag);
    Assert.Equal(HttpStatusCode.Conflict, cycle.StatusCode);

    using var foreignRead = await Request(HttpMethod.Get, $"/documents/{foreign}");
    Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
    using var foreignTopicRead = await Request(HttpMethod.Get, $"/topics/{foreignTopic}");
    Assert.Equal(HttpStatusCode.NotFound, foreignTopicRead.StatusCode);
    using var foreignTagRead = await Request(HttpMethod.Get, $"/tags/{foreignTag}");
    Assert.Equal(HttpStatusCode.NotFound, foreignTagRead.StatusCode);
    using var docs = await Request(HttpMethod.Get, "/documents");
    var rows = (await docs.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToArray();
    Assert.Equal(2, rows.Length);
    var versions = rows.ToDictionary(x => x.GetProperty("document").GetProperty("id").GetGuid(), x => x.GetProperty("etag").GetString()!);
    using var foreignMove = await Request(HttpMethod.Post, "/documents/batch-move", new
    {
      items = new[] { new { id = one, etag = versions[one], targetTopicId = foreignTopic } }
    });
    Assert.Equal(HttpStatusCode.NotFound, foreignMove.StatusCode);
    using var foreignTagWrite = await Request(HttpMethod.Put, $"/documents/{one}/tags/{foreignTag}", match: versions[one]);
    Assert.Equal(HttpStatusCode.NotFound, foreignTagWrite.StatusCode);
    using var missingMatch = await Request(HttpMethod.Patch, $"/documents/{one}", new { title = "new" });
    Assert.Equal((HttpStatusCode)428, missingMatch.StatusCode);
    using var badBatch = await Request(HttpMethod.Post, "/documents/batch-move", new
    {
      items = new[] { new { id = one, etag = versions[one], targetTopicId = target },
        new { id = two, etag = "\"stale\"", targetTopicId = target } }
    });
    Assert.Equal(HttpStatusCode.PreconditionFailed, badBatch.StatusCode);
    using var rolledBack = await Request(HttpMethod.Get, $"/documents/{one}");
    Assert.Equal(versions[one], rolledBack.Headers.ETag!.Tag);

    using var move = await Request(HttpMethod.Post, "/documents/batch-move", new
    {
      items = versions.Select(x => new { id = x.Key, etag = x.Value, targetTopicId = target }).ToArray()
    });
    move.EnsureSuccessStatusCode();
    var moved = (await move.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
    Assert.All(moved.EnumerateArray(), row => Assert.Equal(inbox, row.GetProperty("previousTopicId").GetGuid()));
    using var occupied = await Request(HttpMethod.Delete, $"/topics/{target}", match: created.Headers.ETag.Tag);
    Assert.Equal(HttpStatusCode.Conflict, occupied.StatusCode);
    using var undo = await Request(HttpMethod.Post, "/documents/batch-move", new
    {
      items = moved.EnumerateArray().Select(x => new
      {
        id = x.GetProperty("document").GetProperty("id").GetGuid(),
        etag = x.GetProperty("etag").GetString(),
        targetTopicId = x.GetProperty("previousTopicId").GetGuid()
      }).ToArray()
    });
    undo.EnsureSuccessStatusCode();
    using var removed = await Request(HttpMethod.Delete, $"/topics/{target}", match: created.Headers.ETag.Tag);
    Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    using var tag = await Request(HttpMethod.Post, "/tags", new { name = " ÄRGER ", description = "", color = "#112233" });
    Assert.Equal(HttpStatusCode.Created, tag.StatusCode);
    using var duplicate = await Request(HttpMethod.Post, "/tags", new { name = "ärger", description = "", color = "#112233" });
    Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    var tagId = (await tag.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    using var afterUndo = await Request(HttpMethod.Get, $"/documents/{one}");
    using var tagsRejected = await Request(HttpMethod.Post, "/documents/batch-tags", new
    {
      items = new[] { new { id = one, etag = afterUndo.Headers.ETag!.Tag }, new { id = foreign, etag = "\"0000000000000001\"" } },
      tagId, on = true
    });
    Assert.Equal(HttpStatusCode.NotFound, tagsRejected.StatusCode);
    using var stillOriginal = await Request(HttpMethod.Get, $"/documents/{one}");
    Assert.Equal(afterUndo.Headers.ETag.Tag, stillOriginal.Headers.ETag!.Tag);
    using var tagsApplied = await Request(HttpMethod.Post, "/documents/batch-tags", new
    {
      items = new[] { new { id = one, etag = afterUndo.Headers.ETag.Tag } }, tagId, on = true
    });
    tagsApplied.EnsureSuccessStatusCode();
    using var assigned = await Request(HttpMethod.Get, $"/documents/{one}");
    Assert.NotEqual(afterUndo.Headers.ETag.Tag, assigned.Headers.ETag!.Tag);
    using var renamed = await Request(HttpMethod.Patch, $"/tags/{tagId}", new { name = "Anderer Tag" }, tag.Headers.ETag!.Tag);
    renamed.EnsureSuccessStatusCode();
    using var indirectlyChanged = await Request(HttpMethod.Get, $"/documents/{one}");
    Assert.NotEqual(assigned.Headers.ETag.Tag, indirectlyChanged.Headers.ETag!.Tag);

    using var left = await Request(HttpMethod.Post, "/topics", new { name = "Left" });
    using var right = await Request(HttpMethod.Post, "/topics", new { name = "Right" });
    left.EnsureSuccessStatusCode();
    right.EnsureSuccessStatusCode();
    var leftId = (await left.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    var rightId = (await right.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    var counterMoves = await Task.WhenAll(
        Request(HttpMethod.Patch, $"/topics/{leftId}", new { parentId = rightId }, left.Headers.ETag!.Tag),
        Request(HttpMethod.Patch, $"/topics/{rightId}", new { parentId = leftId }, right.Headers.ETag!.Tag));
    using var firstMove = counterMoves[0];
    using var secondMove = counterMoves[1];
    Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
        counterMoves.Select(response => response.StatusCode).Order());
  }
}
