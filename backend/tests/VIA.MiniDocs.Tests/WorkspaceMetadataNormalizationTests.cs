using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  [Fact]
  public async Task TopicSiblingNamesUseFinalParentAndNameAndReturnSpecificConflicts()
  {
    await using var database = await CreateDatabaseAsync();
    await PostgresTestDatabase.VerifyPermissionsAsync(database);
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "topic-names", password, activeWorkspace: Guid.NewGuid());
    await CreateUserAsync(database, "other-topic-names", password, activeWorkspace: Guid.NewGuid());
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    using var otherClient = TestClient(host);
    var actor = await MetadataLogin(client, "topic-names", password);
    var other = await MetadataLogin(otherClient, "other-topic-names", password);
    var root = await MetadataCreate(actor, "/topics", new { name = "Ärger" });
    await MetadataCreate(other, "/topics", new { name = "ÄRGER" });
    using (var duplicate = await actor.Send(HttpMethod.Post, "/topics", new { name = "\uFEFFÄRGER " }))
      await AssertProblem(duplicate, HttpStatusCode.Conflict, "topic_name_conflict");
    foreach (var reserved in new[] { " NICHT ZUGEORDNET ", "\u00A0Alle Zugeordnet" })
    {
      using var response = await actor.Send(HttpMethod.Post, "/topics", new { name = reserved });
      await AssertProblem(response, HttpStatusCode.Conflict, "topic_name_reserved");
    }
    var child = await MetadataCreate(actor, "/topics", new { name = "ärger", parentId = root.Id });
    await MetadataCreate(actor, "/topics", new { name = "Alle zugeordnet", parentId = root.Id });
    using (var move = await actor.Send(HttpMethod.Patch, $"/topics/{child.Id}",
        new { parentId = (Guid?)null }, child.Etag))
      await AssertProblem(move, HttpStatusCode.Conflict, "topic_name_conflict");
    Assert.Equal(child.Etag, (await MetadataRead(actor, $"/topics/{child.Id}")).Etag);
    using (var patch = await actor.Send(HttpMethod.Patch, $"/topics/{child.Id}",
        new { name = "Unbeschwert", parentId = (Guid?)null }, child.Etag))
      Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
    var moved = await MetadataRead(actor, $"/topics/{child.Id}");
    Assert.Equal("Unbeschwert", moved.Body.GetProperty("name").GetString());
    Assert.Equal(System.Text.Json.JsonValueKind.Null, moved.Body.GetProperty("parentId").ValueKind);
    using (var response = await actor.Send(HttpMethod.Patch, $"/topics/{root.Id}",
        new { name = "ÄRGER" }, root.Etag))
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("ÄRGER", (await MetadataRead(actor, $"/topics/{root.Id}")).Body.GetProperty("name").GetString());
  }

  [Fact]
  public async Task TopicSiblingNamesRejectSecondConcurrentRootCreation()
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertActiveWorkspaceAsync(database, Guid.NewGuid(), workspace, "topic-names-race");
    await using var first = fixture.CreateRuntimePeer(database);
    await using var second = fixture.CreateRuntimePeer(database);
    await using var observer = fixture.CreatePeer(database);
    await using var blocker = await first.Database.BeginTransactionAsync();
    Assert.True(await WorkspaceMetadataPersistence.WriteTopicAsync(first, workspace, Guid.NewGuid(), null, "Ärger", null, true, CancellationToken.None));
    var pid = 0;
    var contender = LocalWriteTransaction.ExecuteAsync(second, async ct =>
    {
      Volatile.Write(ref pid, await second.Database.SqlQueryRaw<int>("""SELECT pg_backend_pid() AS "Value" """).SingleAsync(ct));
      return await WorkspaceMetadataPersistence.WriteTopicAsync(second, workspace, Guid.NewGuid(), null, "ÄRGER", null, true, ct);
    }, value => value);
    try { await PostgresWait.ForBlockedAsync(observer, () => Volatile.Read(ref pid), contender); }
    catch { await blocker.RollbackAsync(); await contender; throw; }
    await blocker.CommitAsync();
    var failure = await Assert.ThrowsAnyAsync<Exception>(async () => { await contender; });
    var postgres = Assert.IsType<PostgresException>(failure.GetBaseException());
    Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    Assert.Equal("ux_topics_sibling_name", postgres.ConstraintName);
    Assert.Single(await observer.Topics.ObserveAllWorkspaces()
        .Where(topic => topic.WorkspaceId == workspace && topic.SystemKey == null).ToListAsync());
  }

  // Same normalization v1 corpus as tagOperations.test.ts, including contextual Unicode lowercase.
  private const string NormalizationWhitespace =
      "\u0009\u000A\u000B\u000C\u000D\u0020\u00A0\u1680\u2000\u2001\u2002\u2003" +
      "\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF";

  [Fact]
  [Trait("Package", "D6-FIX03-B2B")]
  public async Task MetadataNormalizationMatchesFrontendOnCreateAndRename()
  {
    await using var database = await CreateDatabaseAsync();
    await PostgresTestDatabase.VerifyPermissionsAsync(database);
    var workspace = Guid.NewGuid();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "metadata-normalization", password, activeWorkspace: workspace);
    var document = await InsertMetadataDocumentAsync(database, workspace, "Before");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var actor = await MetadataLogin(client, "metadata-normalization", password);
    var vectors = new (string Input, string Name, string Key)[]
    {
      ("\uFEFF\u00A0ÄRGER\u2003\t", "ÄRGER", "ärger"),
      ("\u0085ÄRGER\u0085", "\u0085ÄRGER\u0085", "\u0085ärger\u0085"),
      ("İSTANBUL", "İSTANBUL", "i\u0307stanbul"),
      ("ΟΣ", "ΟΣ", "ος"),
      ("ẞ", "ẞ", "ß"),
      ("A\u0308", "A\u0308", "a\u0308"),
      ("Ä", "Ä", "ä"),
      ("\u1680\u202FGrüN\u3000", "GrüN", "grün"),
    };
    foreach (var (input, name, key) in vectors)
    {
      var tag = await MetadataCreate(actor, "/tags", new { name = input, description = "", color = "#123456" });
      var saved = await database.Tags.ObserveAllWorkspaces().AsNoTracking().SingleAsync(t => t.Id == tag.Id);
      Assert.Equal(name, saved.Name);
      Assert.Equal(key, saved.NameKey);
      using var duplicate = await actor.Send(HttpMethod.Post, "/tags", new { name = key, description = "", color = "#123456" });
      Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
      var temporary = await MetadataCreate(actor, "/tags", new { name = $"Temporary {tag.Id}", description = "", color = "#123456" });
      using var conflictingRename = await actor.Send(HttpMethod.Patch, $"/tags/{temporary.Id}", new { name = input }, temporary.Etag);
      Assert.Equal(HttpStatusCode.Conflict, conflictingRename.StatusCode);
      var unchanged = await MetadataRead(actor, $"/tags/{temporary.Id}");
      Assert.Equal(temporary.Etag, unchanged.Etag);
      Assert.Equal($"Temporary {tag.Id}", unchanged.Body.GetProperty("name").GetString());
      using var remove = await actor.Send(HttpMethod.Delete, $"/tags/{tag.Id}", etag: tag.Etag);
      Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
      using var rename = await actor.Send(HttpMethod.Patch, $"/tags/{temporary.Id}", new { name = input }, temporary.Etag);
      Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
      var renamed = await database.Tags.ObserveAllWorkspaces().AsNoTracking().SingleAsync(t => t.Id == temporary.Id);
      Assert.Equal(name, renamed.Name);
      Assert.Equal(key, renamed.NameKey);
    }
    // Composed/decomposed and accent-distinct names are not folded together.
    Assert.Equal(vectors.Length, await database.Tags.ObserveAllWorkspaces().CountAsync(t => t.WorkspaceId == workspace));
    await MetadataCreate(actor, "/tags", new { name = "A", description = "", color = "#123456" });
    await MetadataCreate(actor, "/tags", new { name = "\u0085", description = "", color = "#123456" });
    var topic = await MetadataCreate(actor, "/topics", new { name = NormalizationWhitespace + "Topic" + NormalizationWhitespace });
    Assert.Equal("Topic", (await MetadataRead(actor, $"/topics/{topic.Id}")).Body.GetProperty("name").GetString());
    var existing = await MetadataRead(actor, $"/documents/{document}");
    using (var update = await actor.Send(HttpMethod.Patch, $"/documents/{document}",
        new { title = NormalizationWhitespace + "Title" + NormalizationWhitespace }, existing.Etag))
      Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    var titled = await MetadataRead(actor, $"/documents/{document}");
    Assert.Equal("Title", titled.Body.GetProperty("document").GetProperty("title").GetString());
    var before = await MetadataSnapshot(actor);
    using (var invalid = await actor.Send(HttpMethod.Post, "/topics", new { name = NormalizationWhitespace }))
      await AssertProblem(invalid, HttpStatusCode.BadRequest, "validation_failed");
    using (var invalid = await actor.Send(HttpMethod.Post, "/tags", new { name = NormalizationWhitespace, description = "", color = "#123456" }))
      await AssertProblem(invalid, HttpStatusCode.BadRequest, "validation_failed");
    using (var invalid = await actor.Send(HttpMethod.Patch, $"/documents/{document}", new { title = NormalizationWhitespace }, titled.Etag))
      await AssertProblem(invalid, HttpStatusCode.BadRequest, "validation_failed");
    Assert.Equal(before, await MetadataSnapshot(actor));
  }

  [Fact]
  [Trait("Package", "D6-FIX03-B2B")]
  public async Task MetadataSqlBoundaryRejectsNoncanonicalNamesEvenWithoutHttp()
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    await PostgresSeed.InsertActiveWorkspaceAsync(database, Guid.NewGuid(), workspace, "metadata-trim-boundary");
    var document = await InsertMetadataDocumentAsync(database, workspace, "Before");
    var topic = Guid.NewGuid();
    var tag = Guid.NewGuid();
    await using var runtime = fixture.CreateRuntimePeer(database);
    var created = await LocalWriteTransaction.ExecuteAsync(runtime, async ct =>
        await WorkspaceMetadataPersistence.WriteTopicAsync(runtime, workspace, topic, null, "Topic", null, true, ct) &&
        await WorkspaceMetadataPersistence.WriteTagAsync(runtime, workspace, tag, null, "Tag", "", "#123456", ct), value => value);
    Assert.Equal(LocalWriteStatus.Committed, created.Status);
    // Reject every ECMAScript trim character at either edge and as an otherwise empty name.
    foreach (var whitespace in NormalizationWhitespace)
    foreach (var name in new[] { whitespace.ToString(), whitespace + "Name", "Name" + whitespace })
    {
      var result = await LocalWriteTransaction.ExecuteAsync(runtime, async ct =>
      {
        Assert.False(await WorkspaceMetadataPersistence.WriteTopicAsync(runtime, workspace, Guid.NewGuid(), null, name, null, true, ct));
        Assert.False(await WorkspaceMetadataPersistence.WriteTopicAsync(runtime, workspace, topic, 1, name, null, false, ct));
        Assert.False(await WorkspaceMetadataPersistence.WriteTagAsync(runtime, workspace, Guid.NewGuid(), null, name, "", "#123456", ct));
        Assert.False(await WorkspaceMetadataPersistence.WriteTagAsync(runtime, workspace, tag, 1, name, "", "#123456", ct));
        Assert.False(await WorkspaceMetadataPersistence.WriteDocumentAsync(runtime, workspace, document, 1, name, null, null, false, ct));
        return true;
      }, value => value);
      Assert.Equal(LocalWriteStatus.Committed, result.Status);
    }
    var savedTopic = await database.Topics.ObserveAllWorkspaces().AsNoTracking().SingleAsync(t => t.Id == topic);
    var savedTag = await database.Tags.ObserveAllWorkspaces().AsNoTracking().SingleAsync(t => t.Id == tag);
    var savedDoc = await database.Documents.ObserveAllWorkspaces().AsNoTracking().SingleAsync(d => d.Id == document);
    Assert.Equal("Topic", savedTopic.Name);
    Assert.Equal("Tag", savedTag.Name);
    Assert.Equal("tag", savedTag.NameKey);
    Assert.Equal("Before", savedDoc.Title);
    Assert.Equal(1L, savedTopic.Version);
    Assert.Equal(1L, savedTag.Version);
    Assert.Equal(1L, savedDoc.Version);
    Assert.Single(await database.Topics.ObserveAllWorkspaces().Where(t => t.WorkspaceId == workspace && t.SystemKey == null).ToListAsync());
    Assert.Single(await database.Tags.ObserveAllWorkspaces().Where(t => t.WorkspaceId == workspace).ToListAsync());
  }
}
