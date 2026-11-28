using System.Net;
using System.Text.Json;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  [Trait("Package", "D6-FIX03-B2A")]
  public async Task MetadataRejectsExplicitNullAndUnstorableTextWithoutChanges(bool nulCharacter)
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "metadata-validation", password, activeWorkspace: workspace);
    var document = await InsertMetadataDocumentAsync(database, workspace, "Before");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var actor = await MetadataLogin(client, "metadata-validation", password);
    var resource = await MetadataResourcesFor(actor, document);
    var before = await MetadataSnapshot(actor);
    object? bad = nulCharacter ? "bad\0text" : null;
    var requests = new List<(string Path, string Version, string Field)>
    {
      ($"/topics/{resource.Topic}", resource.TopicVersion, "name"),
      ($"/tags/{resource.Tag}", resource.TagVersion, "name"),
      ($"/tags/{resource.Tag}", resource.TagVersion, "description"),
      ($"/tags/{resource.Tag}", resource.TagVersion, "color"),
      ($"/documents/{document}", resource.DocumentVersion, "title"),
      ($"/documents/{document}", resource.DocumentVersion, "kind")
    };
    if (!nulCharacter) requests.Add(($"/documents/{document}", resource.DocumentVersion, "topicId"));
    foreach (var (path, version, field) in requests)
    {
      using var response = await actor.Send(HttpMethod.Patch, path, new Dictionary<string, object?> { [field] = bad }, version);
      await AssertProblem(response, HttpStatusCode.BadRequest, "validation_failed");
    }
    using (var response = await actor.Send(HttpMethod.Post, "/topics", new { name = bad }))
      await AssertProblem(response, HttpStatusCode.BadRequest, "validation_failed");
    foreach (var field in new[] { "name", "description", "color" })
    {
      var tag = new Dictionary<string, object?> { ["name"] = "New tag", ["description"] = "", ["color"] = "#123456" };
      tag[field] = bad;
      using var response = await actor.Send(HttpMethod.Post, "/tags", tag);
      await AssertProblem(response, HttpStatusCode.BadRequest, "validation_failed");
      tag.Remove(field);
      using var omitted = await actor.Send(HttpMethod.Post, "/tags", tag);
      await AssertProblem(omitted, HttpStatusCode.BadRequest, "validation_failed");
    }
    Assert.Equal(before, await MetadataSnapshot(actor));
  }

  [Fact]
  [Trait("Package", "D6-FIX03-B2A")]
  public async Task MetadataPatchAllowsOmittedFieldsAndExplicitNullableFields()
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "metadata-optional", password, activeWorkspace: workspace);
    var document = await InsertMetadataDocumentAsync(database, workspace, "Before");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var actor = await MetadataLogin(client, "metadata-optional", password);
    var resource = await MetadataResourcesFor(actor, document);
    var before = await MetadataSnapshot(actor);
    foreach (var (path, version) in new[]
    {
      ($"/topics/{resource.Topic}", resource.TopicVersion), ($"/tags/{resource.Tag}", resource.TagVersion),
      ($"/documents/{document}", resource.DocumentVersion)
    })
    {
      using var response = await actor.Send(HttpMethod.Patch, path, new Dictionary<string, object?>(), version);
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      Assert.Equal(version, response.Headers.ETag!.Tag);
    }
    Assert.Equal(before, await MetadataSnapshot(actor));
    var child = await MetadataCreate(actor, "/topics", new { name = "Child", parentId = resource.Topic });
    using (var detach = await actor.Send(HttpMethod.Patch, $"/topics/{child.Id}", new { parentId = (Guid?)null }, child.Etag))
      Assert.Equal(HttpStatusCode.OK, detach.StatusCode);
    var root = await MetadataRead(actor, $"/topics/{child.Id}");
    Assert.Equal(JsonValueKind.Null, root.Body.GetProperty("parentId").ValueKind);
    Assert.Equal("Child", root.Body.GetProperty("name").GetString());
    var oldTag = await MetadataRead(actor, $"/tags/{resource.Tag}");
    using (var change = await actor.Send(HttpMethod.Patch, $"/tags/{resource.Tag}", new { description = "Changed" }, oldTag.Etag))
      Assert.Equal(HttpStatusCode.OK, change.StatusCode);
    var tag = await MetadataRead(actor, $"/tags/{resource.Tag}");
    Assert.Equal(oldTag.Body.GetProperty("name").GetString(), tag.Body.GetProperty("name").GetString());
    Assert.Equal(oldTag.Body.GetProperty("color").GetString(), tag.Body.GetProperty("color").GetString());
    Assert.Equal("Changed", tag.Body.GetProperty("description").GetString());
    var old = await MetadataRead(actor, $"/documents/{document}");
    using (var date = await actor.Send(HttpMethod.Patch, $"/documents/{document}", new { documentDate = "2026-11-28" }, old.Etag))
      Assert.Equal(HttpStatusCode.OK, date.StatusCode);
    var dated = await MetadataRead(actor, $"/documents/{document}");
    using (var clear = await actor.Send(HttpMethod.Patch, $"/documents/{document}", new { documentDate = (string?)null }, dated.Etag))
      Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
    var result = await MetadataRead(actor, $"/documents/{document}");
    var row = result.Body.GetProperty("document");
    Assert.Equal(JsonValueKind.Null, row.GetProperty("documentDate").ValueKind);
    Assert.Equal("Before", row.GetProperty("title").GetString());
    Assert.Equal(old.Body.GetProperty("document").GetProperty("topicId").GetGuid(), row.GetProperty("topicId").GetGuid());
  }

  [Fact]
  [Trait("Package", "D6-FINAL-LOW")]
  public async Task MetadataLengthsCountUnicodeCodePointsAndRejectOverflow()
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "metadata-lengths", password, activeWorkspace: workspace);
    var document = await InsertMetadataDocumentAsync(database, workspace, "Before");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var actor = await MetadataLogin(client, "metadata-lengths", password);
    static string Emoji(int count) => string.Concat(Enumerable.Repeat("\U0001F600", count));
    var topicName = Emoji(255);
    var tagName = Emoji(100);
    var description = Emoji(1000);
    var topic = await MetadataCreate(actor, "/topics", new { name = topicName });
    var tag = await MetadataCreate(actor, "/tags", new { name = tagName, description, color = "#123456" });
    Assert.Equal(topicName, (await MetadataRead(actor, $"/topics/{topic.Id}")).Body.GetProperty("name").GetString());
    var createdTag = (await MetadataRead(actor, $"/tags/{tag.Id}")).Body;
    Assert.Equal(tagName, createdTag.GetProperty("name").GetString());
    Assert.Equal(description, createdTag.GetProperty("description").GetString());
    var renamedTopic = "A" + Emoji(254);
    var renamedTag = "A" + Emoji(99);
    var updatedDescription = "A" + Emoji(999);
    using (var response = await actor.Send(HttpMethod.Patch, $"/topics/{topic.Id}", new { name = renamedTopic }, topic.Etag))
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    using (var response = await actor.Send(HttpMethod.Patch, $"/tags/{tag.Id}", new { name = renamedTag, description = updatedDescription }, tag.Etag))
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var oldDocument = await MetadataRead(actor, $"/documents/{document}");
    using (var response = await actor.Send(HttpMethod.Patch, $"/documents/{document}", new { title = topicName }, oldDocument.Etag))
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var currentTopic = await MetadataRead(actor, $"/topics/{topic.Id}");
    var currentTag = await MetadataRead(actor, $"/tags/{tag.Id}");
    var currentDocument = await MetadataRead(actor, $"/documents/{document}");
    Assert.Equal(renamedTopic, currentTopic.Body.GetProperty("name").GetString());
    Assert.Equal(renamedTag, currentTag.Body.GetProperty("name").GetString());
    Assert.Equal(updatedDescription, currentTag.Body.GetProperty("description").GetString());
    Assert.Equal(topicName, currentDocument.Body.GetProperty("document").GetProperty("title").GetString());
    var before = await MetadataSnapshot(actor);
    var invalid = new (HttpMethod Method, string Path, object Body, string? Version)[]
    {
      (HttpMethod.Post, "/topics", new { name = Emoji(256) }, null),
      (HttpMethod.Patch, $"/topics/{topic.Id}", new { name = Emoji(256) }, currentTopic.Etag),
      (HttpMethod.Post, "/tags", new { name = Emoji(101), description = "", color = "#123456" }, null),
      (HttpMethod.Post, "/tags", new { name = "Other", description = Emoji(1001), color = "#123456" }, null),
      (HttpMethod.Patch, $"/tags/{tag.Id}", new { name = Emoji(101) }, currentTag.Etag),
      (HttpMethod.Patch, $"/tags/{tag.Id}", new { description = Emoji(1001) }, currentTag.Etag),
      (HttpMethod.Patch, $"/documents/{document}", new { title = Emoji(256) }, currentDocument.Etag)
    };
    foreach (var (method, path, body, version) in invalid)
    {
      using var response = await actor.Send(method, path, body, version);
      await AssertProblem(response, HttpStatusCode.BadRequest, "validation_failed");
    }
    Assert.Equal(before, await MetadataSnapshot(actor));
  }
}
