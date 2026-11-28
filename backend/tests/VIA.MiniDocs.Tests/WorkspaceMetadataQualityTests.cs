using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VIA.MiniDocs.Infrastructure;
using VIA.MiniDocs.Infrastructure.Identity;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  private sealed record MetadataActor(HttpClient Client, string Session, string CsrfCookie, string Token)
  {
    public Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, string? etag = null,
        bool csrf = true, string? overrideToken = null) =>
        RequestAsync(Client, method, "/api/v1" + path, Session, CsrfCookie, csrf ? overrideToken ?? Token : null, body, etag);
  }

  private static async Task<MetadataActor> MetadataLogin(HttpClient client, string name, string password)
  {
    var anon = await GetAntiforgeryAsync(client);
    using var login = await LoginAsync(client, name, password, anon.Cookie, anon.Token);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var session = Cookie(login, SessionName);
    var csrf = await GetAntiforgeryAsync(client, session);
    return new(client, session, csrf.Cookie, csrf.Token);
  }

  private static async Task<(JsonElement Body, string Etag)> MetadataRead(MetadataActor actor, string path)
  {
    using var response = await actor.Send(HttpMethod.Get, path);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    return (body, response.Headers.ETag!.Tag);
  }

  private static async Task<(Guid Id, string Etag)> MetadataCreate(MetadataActor actor, string path, object body)
  {
    using var response = await actor.Send(HttpMethod.Post, path, body);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return ((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), response.Headers.ETag!.Tag);
  }

  private static async Task<string[]> MetadataSnapshot(MetadataActor actor)
  {
    var result = new List<string>();
    foreach (var path in new[] { "/topics", "/tags", "/documents" })
    {
      using var response = await actor.Send(HttpMethod.Get, path);
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      result.Add(await response.Content.ReadAsStringAsync());
    }
    return result.ToArray();
  }

  private sealed record MetadataResources(Guid Document, string DocumentVersion, Guid Topic, string TopicVersion,
      Guid Tag, string TagVersion);

  private static async Task<MetadataResources> MetadataResourcesFor(MetadataActor actor, Guid document)
  {
    var topic = await MetadataCreate(actor, "/topics", new { name = "Private topic" });
    var tag = await MetadataCreate(actor, "/tags", new { name = "Private tag", description = "", color = "#123456" });
    var current = await MetadataRead(actor, $"/documents/{document}");
    using var assign = await actor.Send(HttpMethod.Put, $"/documents/{document}/tags/{tag.Id}", etag: current.Etag);
    Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
    return new(document, assign.Headers.ETag!.Tag, topic.Id, topic.Etag, tag.Id, tag.Etag);
  }

  [Fact]
  [Trait("Package", "D6-FIX03-A")]
  public async Task MetadataHttpIsolationIsBidirectionalAndRejectedWritesPreserveAllResources()
  {
    await using var database = await CreateDatabaseAsync();
    await PostgresTestDatabase.VerifyPermissionsAsync(database);
    var password = Guid.NewGuid().ToString("N");
    var workspaceA = Guid.NewGuid();
    var workspaceB = Guid.NewGuid();
    await CreateUserAsync(database, "matrix-a", password, activeWorkspace: workspaceA);
    await CreateUserAsync(database, "matrix-b", password, activeWorkspace: workspaceB);
    var docA = await InsertMetadataDocumentAsync(database, workspaceA, "A");
    var docB = await InsertMetadataDocumentAsync(database, workspaceB, "B");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var clientA = TestClient(host);
    using var clientB = TestClient(host);
    var a = await MetadataLogin(clientA, "matrix-a", password);
    var b = await MetadataLogin(clientB, "matrix-b", password);
    var ownA = await MetadataResourcesFor(a, docA);
    var ownB = await MetadataResourcesFor(b, docB);
    var beforeA = await MetadataSnapshot(a);
    var beforeB = await MetadataSnapshot(b);

    foreach (var (actor, own, other) in new[] { (a, ownA, ownB), (b, ownB, ownA) })
    {
      var lists = await MetadataSnapshot(actor);
      Assert.Contains(own.Topic.ToString(), lists[0]);
      Assert.Contains(own.Tag.ToString(), lists[1]);
      Assert.Contains(own.Document.ToString(), lists[2]);
      Assert.DoesNotContain(other.Topic.ToString(), lists[0]);
      Assert.DoesNotContain(other.Tag.ToString(), lists[1]);
      Assert.DoesNotContain(other.Document.ToString(), lists[2]);
      async Task Reject(HttpMethod method, string path, HttpStatusCode expected, object? body = null, string? etag = null)
      {
        using var response = await actor.Send(method, path, body, etag);
        Assert.True(response.StatusCode == expected, $"{method} {path}: expected {expected}, got {response.StatusCode}");
      }
      foreach (var path in new[] { $"/topics/{other.Topic}", $"/tags/{other.Tag}", $"/documents/{other.Document}" })
        await Reject(HttpMethod.Get, path, HttpStatusCode.NotFound);
      await Reject(HttpMethod.Patch, $"/topics/{other.Topic}", HttpStatusCode.NotFound, new { name = "Forbidden" }, other.TopicVersion);
      await Reject(HttpMethod.Delete, $"/topics/{other.Topic}", HttpStatusCode.NotFound, etag: other.TopicVersion);
      await Reject(HttpMethod.Patch, $"/tags/{other.Tag}", HttpStatusCode.NotFound, new { name = "Forbidden" }, other.TagVersion);
      await Reject(HttpMethod.Delete, $"/tags/{other.Tag}", HttpStatusCode.NotFound, etag: other.TagVersion);
      await Reject(HttpMethod.Patch, $"/documents/{other.Document}", HttpStatusCode.NotFound, new { title = "Forbidden" }, other.DocumentVersion);
      await Reject(HttpMethod.Patch, $"/documents/{own.Document}", HttpStatusCode.NotFound,
          new { title = "Forbidden", topicId = other.Topic }, own.DocumentVersion);
      await Reject(HttpMethod.Post, "/topics", HttpStatusCode.Conflict, new { name = "Forbidden", parentId = other.Topic });
      await Reject(HttpMethod.Patch, $"/topics/{own.Topic}", HttpStatusCode.Conflict, new { parentId = other.Topic }, own.TopicVersion);
      foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
      {
        await Reject(method, $"/documents/{other.Document}/tags/{own.Tag}", HttpStatusCode.NotFound, etag: other.DocumentVersion);
        await Reject(method, $"/documents/{own.Document}/tags/{other.Tag}", HttpStatusCode.NotFound, etag: own.DocumentVersion);
      }
      var moves = new[] { new { id = own.Document, etag = own.DocumentVersion, targetTopicId = own.Topic },
          new { id = other.Document, etag = other.DocumentVersion, targetTopicId = own.Topic } };
      var tags = new[] { new { id = own.Document, etag = own.DocumentVersion }, new { id = other.Document, etag = other.DocumentVersion } };
      foreach (var reverse in new[] { false, true })
      {
        await Reject(HttpMethod.Post, "/documents/batch-move", HttpStatusCode.NotFound,
            new { items = reverse ? moves.Reverse().ToArray() : moves });
        await Reject(HttpMethod.Post, "/documents/batch-tags", HttpStatusCode.NotFound,
            new { items = reverse ? tags.Reverse().ToArray() : tags, tagId = own.Tag, on = false });
      }
      Assert.Equal(beforeA, await MetadataSnapshot(a));
      Assert.Equal(beforeB, await MetadataSnapshot(b));
    }
  }

  [Fact]
  [Trait("Package", "D6-FIX03-A")]
  public async Task MetadataHttpRequiresWorkspaceAuthorityAndAntiforgeryOnEveryMutation()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var workspace = Guid.NewGuid();
    var user = await CreateUserAsync(database, "metadata-member", password, activeWorkspace: workspace);
    await CreateUserAsync(database, "metadata-admin", password, admin: true);
    var document = await InsertMetadataDocumentAsync(database, workspace, "Own");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var memberClient = TestClient(host);
    using var adminClient = TestClient(host);
    var member = await MetadataLogin(memberClient, "metadata-member", password);
    var admin = await MetadataLogin(adminClient, "metadata-admin", password);
    var resource = await MetadataResourcesFor(member, document);
    var before = await MetadataSnapshot(member);
    var writes = new (HttpMethod Method, string Path, object? Body, string? Version)[]
    {
      (HttpMethod.Post, "/topics", new { name = "Denied" }, null),
      (HttpMethod.Patch, $"/topics/{resource.Topic}", new { name = "Denied" }, resource.TopicVersion),
      (HttpMethod.Delete, $"/topics/{resource.Topic}", null, resource.TopicVersion),
      (HttpMethod.Post, "/tags", new { name = "Denied", description = "", color = "#123456" }, null),
      (HttpMethod.Patch, $"/tags/{resource.Tag}", new { name = "Denied" }, resource.TagVersion),
      (HttpMethod.Delete, $"/tags/{resource.Tag}", null, resource.TagVersion),
      (HttpMethod.Patch, $"/documents/{document}", new { title = "Denied" }, resource.DocumentVersion),
      (HttpMethod.Post, "/documents/batch-move", new { items = new[] { new { id = document, etag = resource.DocumentVersion, targetTopicId = resource.Topic } } }, null),
      (HttpMethod.Post, "/documents/batch-tags", new { items = new[] { new { id = document, etag = resource.DocumentVersion } }, tagId = resource.Tag, on = false }, null),
      (HttpMethod.Put, $"/documents/{document}/tags/{resource.Tag}", null, resource.DocumentVersion),
      (HttpMethod.Delete, $"/documents/{document}/tags/{resource.Tag}", null, resource.DocumentVersion)
    };
    var reads = new[] { "/topics", "/tags", "/documents", $"/topics/{resource.Topic}", $"/tags/{resource.Tag}", $"/documents/{document}" };
    foreach (var path in reads)
    {
      using var denied = await admin.Send(HttpMethod.Get, path);
      await AssertProblem(denied, HttpStatusCode.Forbidden, "forbidden");
    }
    foreach (var (method, path, body, version) in writes)
    {
      using var noCsrf = await member.Send(method, path, body, version, csrf: false);
      await AssertProblem(noCsrf, HttpStatusCode.Forbidden, "antiforgery_invalid");
      using var badCsrf = await member.Send(method, path, body, version, overrideToken: "invalid");
      await AssertProblem(badCsrf, HttpStatusCode.Forbidden, "antiforgery_invalid");
      using var denied = await admin.Send(method, path, body, version);
      await AssertProblem(denied, HttpStatusCode.Forbidden, "forbidden");
    }
    Assert.Equal(before, await MetadataSnapshot(member));
    var userVersion = (await new AdministratorAccounts(runtime).ReadVersionAsync(user, default))!.ETag;
    using var disabled = await admin.Send(HttpMethod.Patch, $"/admin/users/{user}", new { isDisabled = true }, userVersion);
    Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
    foreach (var path in reads)
    {
      using var denied = await member.Send(HttpMethod.Get, path);
      await AssertProblem(denied, HttpStatusCode.Unauthorized, "authentication_required");
    }
    foreach (var (method, path, body, version) in writes)
    {
      using var denied = await member.Send(method, path, body, version);
      await AssertProblem(denied, HttpStatusCode.Unauthorized, "authentication_required");
    }
    var enabledVersion = disabled.Headers.ETag!.Tag;
    using var enabled = await admin.Send(HttpMethod.Patch, $"/admin/users/{user}", new { isDisabled = false }, enabledVersion);
    Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
    var fresh = await MetadataLogin(memberClient, "metadata-member", password);
    Assert.Equal(before, await MetadataSnapshot(fresh));
  }

  private sealed class FailMetadataCommand : DbCommandInterceptor
  {
    public bool Enabled { get; set; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
      if (Enabled && command.CommandText.Contains("minidocs.command_d6_document(", StringComparison.Ordinal))
        throw new TimeoutException("Injected failure after topic move.");
      return ValueTask.FromResult(result);
    }
  }

  [Fact]
  [Trait("Package", "D6-FIX03-A")]
  public async Task MetadataHttpCombinedDocumentPatchCommitsOrRollsBackAndRejectsStaleVersion()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var workspace = Guid.NewGuid();
    await CreateUserAsync(database, "metadata-patch", password, activeWorkspace: workspace);
    var id = await InsertMetadataDocumentAsync(database, workspace, "Before");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    var failure = new FailMetadataCommand();
    using var host = CreateHost(runtime, keys, failure);
    using var client = TestClient(host);
    var actor = await MetadataLogin(client, "metadata-patch", password);
    var target = await MetadataCreate(actor, "/topics", new { name = "Target" });
    var path = $"/documents/{id}";
    var before = await MetadataRead(actor, path);
    var patch = new { title = "After", kind = "meeting", documentDate = "2026-11-28", topicId = target.Id };
    failure.Enabled = true;
    using (var rejected = await actor.Send(HttpMethod.Patch, path, patch, before.Etag))
      await AssertProblem(rejected, HttpStatusCode.ServiceUnavailable, "dependency_unavailable");
    failure.Enabled = false;
    var rolledBack = await MetadataRead(actor, path);
    Assert.Equal(before.Etag, rolledBack.Etag);
    Assert.Equal(before.Body.GetRawText(), rolledBack.Body.GetRawText());
    using var changed = await actor.Send(HttpMethod.Patch, path, patch, before.Etag);
    Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
    var after = await MetadataRead(actor, path);
    var doc = after.Body.GetProperty("document");
    Assert.Equal("After", doc.GetProperty("title").GetString());
    Assert.Equal("meeting", doc.GetProperty("kind").GetString());
    Assert.Equal("2026-11-28", doc.GetProperty("documentDate").GetString());
    Assert.Equal(target.Id, doc.GetProperty("topicId").GetGuid());
    Assert.Equal(changed.Headers.ETag!.Tag, after.Etag);
    Assert.NotEqual(before.Etag, after.Etag);
    using var stale = await actor.Send(HttpMethod.Patch, path, new { title = "Stale" }, before.Etag);
    Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
    var still = await MetadataRead(actor, path);
    Assert.Equal(after.Body.GetRawText(), still.Body.GetRawText());
    Assert.Equal(after.Etag, still.Etag);
    using var clear = await actor.Send(HttpMethod.Patch, path, new { documentDate = (string?)null }, after.Etag);
    Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
    var cleared = await MetadataRead(actor, path);
    Assert.Equal(JsonValueKind.Null, cleared.Body.GetProperty("document").GetProperty("documentDate").ValueKind);
    Assert.Equal("After", cleared.Body.GetProperty("document").GetProperty("title").GetString());
    var concurrent = await Task.WhenAll(
        actor.Send(HttpMethod.Patch, path, new { title = "First writer" }, cleared.Etag),
        actor.Send(HttpMethod.Patch, path, new { title = "Second writer" }, cleared.Etag));
    using var firstWrite = concurrent[0];
    using var secondWrite = concurrent[1];
    Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.PreconditionFailed }, concurrent.Select(r => r.StatusCode).Order());
    var winner = await MetadataRead(actor, path);
    Assert.Equal(firstWrite.StatusCode == HttpStatusCode.OK ? "First writer" : "Second writer",
        winner.Body.GetProperty("document").GetProperty("title").GetString());
    Assert.Equal((firstWrite.StatusCode == HttpStatusCode.OK ? firstWrite : secondWrite).Headers.ETag!.Tag, winner.Etag);
  }

  [Fact]
  [Trait("Package", "D6-FIX03-A")]
  public async Task MetadataHttpTagRenameAndDeleteInvalidateOnlyAffectedDocumentsAndStaleUndoRollsBack()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var workspace = Guid.NewGuid();
    await CreateUserAsync(database, "metadata-versions", password, activeWorkspace: workspace);
    var first = await InsertMetadataDocumentAsync(database, workspace, "First");
    var second = await InsertMetadataDocumentAsync(database, workspace, "Second");
    var unrelated = await InsertMetadataDocumentAsync(database, workspace, "Unrelated");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var actor = await MetadataLogin(client, "metadata-versions", password);
    var topic = await MetadataCreate(actor, "/topics", new { name = "Target" });
    var tag = await MetadataCreate(actor, "/tags", new { name = "Tag", description = "", color = "#123456" });
    var original = await MetadataRead(actor, $"/documents/{first}");
    var original2 = await MetadataRead(actor, $"/documents/{second}");
    var untouched = await MetadataRead(actor, $"/documents/{unrelated}");
    using var assign = await actor.Send(HttpMethod.Post, "/documents/batch-tags", new
    {
      items = new[] { new { id = first, etag = original.Etag }, new { id = second, etag = original2.Etag } }, tagId = tag.Id, on = true
    });
    Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
    var assigned1 = await MetadataRead(actor, $"/documents/{first}");
    var assigned2 = await MetadataRead(actor, $"/documents/{second}");
    using var move = await actor.Send(HttpMethod.Post, "/documents/batch-move", new
    {
      items = new[] { new { id = first, etag = assigned1.Etag, targetTopicId = topic.Id },
        new { id = second, etag = assigned2.Etag, targetTopicId = topic.Id } }
    });
    Assert.Equal(HttpStatusCode.OK, move.StatusCode);
    var moved = (await move.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
    var moved1 = await MetadataRead(actor, $"/documents/{first}");
    var moved2 = await MetadataRead(actor, $"/documents/{second}");
    // Change only the second document so undo must roll back a successful first item.
    using var intervening = await actor.Send(HttpMethod.Patch, $"/documents/{second}", new { title = "Newer" }, moved2.Etag);
    Assert.Equal(HttpStatusCode.OK, intervening.StatusCode);
    var beforeUndo = await MetadataSnapshot(actor);
    using var partialUndo = await actor.Send(HttpMethod.Post, "/documents/batch-move", new
    {
      items = moved.EnumerateArray().Select(row => new
      {
        id = row.GetProperty("document").GetProperty("id").GetGuid(),
        etag = row.GetProperty("etag").GetString(), targetTopicId = row.GetProperty("previousTopicId").GetGuid()
      }).ToArray()
    });
    Assert.Equal(HttpStatusCode.PreconditionFailed, partialUndo.StatusCode);
    Assert.Equal(beforeUndo, await MetadataSnapshot(actor));
    moved2 = await MetadataRead(actor, $"/documents/{second}");
    using var rename = await actor.Send(HttpMethod.Patch, $"/tags/{tag.Id}", new { name = "Renamed" }, tag.Etag);
    Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    var renamed1 = await MetadataRead(actor, $"/documents/{first}");
    var renamed2 = await MetadataRead(actor, $"/documents/{second}");
    Assert.NotEqual(moved1.Etag, renamed1.Etag);
    Assert.NotEqual(moved2.Etag, renamed2.Etag);
    var snapshot = await MetadataSnapshot(actor);
    using var staleUndo = await actor.Send(HttpMethod.Post, "/documents/batch-move", new
    {
      items = moved.EnumerateArray().Select(row => new
      {
        id = row.GetProperty("document").GetProperty("id").GetGuid(),
        etag = row.GetProperty("etag").GetString(), targetTopicId = row.GetProperty("previousTopicId").GetGuid()
      }).ToArray()
    });
    Assert.Equal(HttpStatusCode.PreconditionFailed, staleUndo.StatusCode);
    Assert.Equal(snapshot, await MetadataSnapshot(actor));
    using var remove = await actor.Send(HttpMethod.Delete, $"/tags/{tag.Id}", etag: rename.Headers.ETag!.Tag);
    Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
    foreach (var (id, version) in new[] { (first, renamed1.Etag), (second, renamed2.Etag) })
    {
      var saved = await MetadataRead(actor, $"/documents/{id}");
      Assert.NotEqual(version, saved.Etag);
      Assert.Empty(saved.Body.GetProperty("document").GetProperty("tagIds").EnumerateArray());
      Assert.Equal(topic.Id, saved.Body.GetProperty("document").GetProperty("topicId").GetGuid());
    }
    var still = await MetadataRead(actor, $"/documents/{unrelated}");
    Assert.Equal(untouched.Etag, still.Etag);
    Assert.Equal(untouched.Body.GetRawText(), still.Body.GetRawText());
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  [Trait("Package", "D6-FIX03-A")]
  public async Task MetadataPostgresTopicDeletionAndDocumentMoveSerializeInBothOrders(bool moveFirst)
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    var inbox = await PostgresSeed.InsertActiveWorkspaceAsync(database, Guid.NewGuid(), workspace, "metadata-race");
    var document = await InsertMetadataDocumentAsync(database, workspace, "Own");
    var topic = Guid.NewGuid();
    await using var first = fixture.CreateRuntimePeer(database);
    await using var second = fixture.CreateRuntimePeer(database);
    await using var observer = fixture.CreatePeer(database);
    var created = await LocalWriteTransaction.ExecuteAsync(first,
        ct => WorkspaceMetadataPersistence.WriteTopicAsync(first, workspace, topic, null, "Target", null, true, ct), value => value);
    Assert.Equal(LocalWriteStatus.Committed, created.Status);
    await using var blocker = await first.Database.BeginTransactionAsync();
    Assert.True(moveFirst ? await DocumentPersistence.TryMoveAsync(first, workspace, document, 1, topic)
        : await TopicPersistence.TryDeleteAsync(first, workspace, topic, 1));
    var pid = 0;
    var contender = LocalWriteTransaction.ExecuteAsync(second, async ct =>
    {
      Volatile.Write(ref pid, await second.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync(ct));
      return moveFirst ? await TopicPersistence.TryDeleteAsync(second, workspace, topic, 1, ct)
          : await DocumentPersistence.TryMoveAsync(second, workspace, document, 1, topic, ct);
    }, value => value);
    try { await PostgresWait.ForBlockedAsync(observer, () => Volatile.Read(ref pid), contender); }
    catch { await blocker.RollbackAsync(); await contender; throw; }
    await blocker.CommitAsync();
    Assert.Equal(LocalWriteStatus.Rejected, (await contender).Status);
    var saved = await observer.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync(d => d.Id == document);
    Assert.Equal(moveFirst ? topic : inbox, saved.TopicId);
    Assert.Equal(moveFirst ? 2L : 1L, saved.Version);
    Assert.Equal(moveFirst, await observer.Topics.ObserveAllWorkspaces().AnyAsync(t => t.Id == topic));
  }

  [Fact]
  [Trait("Package", "D6-FIX03-A")]
  public async Task MetadataPostgresOppositeTopicMovesWaitAndCannotCreateCycle()
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertActiveWorkspaceAsync(database, Guid.NewGuid(), workspace, "metadata-cycle");
    await using var first = fixture.CreateRuntimePeer(database);
    await using var second = fixture.CreateRuntimePeer(database);
    await using var observer = fixture.CreatePeer(database);
    var left = Guid.NewGuid();
    var right = Guid.NewGuid();
    var created = await LocalWriteTransaction.ExecuteAsync(first, async ct =>
        await WorkspaceMetadataPersistence.WriteTopicAsync(first, workspace, left, null, "Left", null, true, ct) &&
        await WorkspaceMetadataPersistence.WriteTopicAsync(first, workspace, right, null, "Right", null, true, ct), value => value);
    Assert.Equal(LocalWriteStatus.Committed, created.Status);
    await using var blocker = await first.Database.BeginTransactionAsync();
    Assert.True(await TopicPersistence.TryMoveAsync(first, workspace, left, 1, right));
    var pid = 0;
    var contender = LocalWriteTransaction.ExecuteAsync(second, async ct =>
    {
      Volatile.Write(ref pid, await second.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync(ct));
      return await TopicPersistence.TryMoveAsync(second, workspace, right, 1, left, ct);
    }, value => value);
    try { await PostgresWait.ForBlockedAsync(observer, () => Volatile.Read(ref pid), contender); }
    catch { await blocker.RollbackAsync(); await contender; throw; }
    await blocker.CommitAsync();
    Assert.Equal(LocalWriteStatus.Rejected, (await contender).Status);
    var rows = await observer.Topics.ObserveAllWorkspaces().AsNoTracking().Where(t => t.Id == left || t.Id == right).ToListAsync();
    Assert.Equal(right, rows.Single(t => t.Id == left).ParentId);
    Assert.Null(rows.Single(t => t.Id == right).ParentId);
    Assert.Equal(2L, rows.Single(t => t.Id == left).Version);
    Assert.Equal(1L, rows.Single(t => t.Id == right).Version);
  }
}
