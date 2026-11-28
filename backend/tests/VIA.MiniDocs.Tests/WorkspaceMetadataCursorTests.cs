using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  private static async Task<(Guid[] Ids, string? Next)> ReadDocumentPage(MetadataActor actor, string path)
  {
    using var response = await actor.Send(HttpMethod.Get, path);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var page = await response.Content.ReadFromJsonAsync<JsonElement>();
    return (page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("document").GetProperty("id").GetGuid()).ToArray(),
        page.GetProperty("nextCursor").GetString());
  }

  private static async Task<Guid[]> ReadDocumentContinuation(MetadataActor actor, string query, string? cursor)
  {
    var result = new List<Guid>();
    var cursors = new HashSet<string>();
    while (cursor is not null)
    {
      Assert.InRange(cursor.Length, 1, 2048);
      Assert.True(cursors.Add(cursor), "Repeated cursor");
      Assert.InRange(cursors.Count, 1, 10);
      var page = await ReadDocumentPage(actor, query + "&cursor=" + Uri.EscapeDataString(cursor));
      result.AddRange(page.Ids);
      cursor = page.Next;
    }
    return result.ToArray();
  }

  [Theory]
  [InlineData("title")]
  [InlineData("date-asc")]
  [InlineData("date-desc")]
  [Trait("Package", "D6-FIX03-B1")]
  public async Task MetadataDocumentCursorKeepsOriginalSortBoundaryAfterAnchorChanges(string sort)
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    var foreign = Guid.NewGuid();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "cursor-owner", password, activeWorkspace: workspace);
    await CreateUserAsync(database, "cursor-foreign", password, activeWorkspace: foreign);
    var titles = new[] { "Apfel", "Apfel", "Ärger", "Öl", "Zebra", "Zebra" };
    string?[] dates = [null, null, "2026-01-01", "2026-01-01", "2026-02-01", "2026-02-01"];
    var ids = new Guid[titles.Length];
    for (var i = 0; i < ids.Length; i++)
      ids[i] = await InsertMetadataDocumentAsync(database, workspace, titles[i]);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    using var foreignClient = TestClient(host);
    var actor = await MetadataLogin(client, "cursor-owner", password);
    var other = await MetadataLogin(foreignClient, "cursor-foreign", password);
    for (var i = 0; i < ids.Length; i++)
    {
      var current = await MetadataRead(actor, $"/documents/{ids[i]}");
      using var changed = await actor.Send(HttpMethod.Patch, $"/documents/{ids[i]}", new { documentDate = dates[i] }, current.Etag);
      Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
    }
    var currentFirst = await MetadataRead(actor, $"/documents/{ids[0]}");
    var inbox = currentFirst.Body.GetProperty("document").GetProperty("topicId").GetGuid();
    var query = $"/documents?sort={sort}&topicId={inbox}";
    var all = await ReadDocumentPage(actor, query + "&limit=100");
    Assert.Null(all.Next);
    Guid[] expected = sort switch
    {
      "title" => [.. ids.Take(2).Order(), ids[2], ids[3], .. ids.Skip(4).Order()],
      "date-asc" => [.. ids.Take(2).Order(), .. ids.Skip(2).Take(2).Order(), .. ids.Skip(4).Order()],
      _ => [.. ids.Skip(4).Order(), .. ids.Skip(2).Take(2).Order(), .. ids.Take(2).Order()]
    };
    Assert.Equal(expected, all.Ids);
    var first = await ReadDocumentPage(actor, query + "&limit=2");
    Assert.Equal(all.Ids.Take(2), first.Ids);
    Assert.NotNull(first.Next);
    var cursor = first.Next!;
    Assert.Equal(all.Ids.Skip(2), await ReadDocumentContinuation(actor, query + "&limit=2", cursor));
    var encoded = Uri.EscapeDataString(cursor);
    foreach (var invalid in new[]
    {
      query + "&limit=3&cursor=" + encoded,
      $"/documents?sort={sort}&limit=2&cursor=" + encoded,
      query + "&limit=2&cursor=" + Uri.EscapeDataString((cursor[0] == 'A' ? "B" : "A") + cursor[1..]),
      $"/documents?sort={(sort == "title" ? "date-asc" : "title")}&topicId={inbox}&limit=2&cursor=" + encoded
    })
    {
      using var rejected = await actor.Send(HttpMethod.Get, invalid);
      await AssertProblem(rejected, HttpStatusCode.BadRequest, "cursor_invalid");
    }
    using (var rejected = await other.Send(HttpMethod.Get, query + "&limit=2&cursor=" + encoded))
      await AssertProblem(rejected, HttpStatusCode.BadRequest, "cursor_invalid");
    var anchor = first.Ids[^1];
    var old = await MetadataRead(actor, $"/documents/{anchor}");
    using (var changed = await actor.Send(HttpMethod.Patch, $"/documents/{anchor}",
        new { title = "ZZZZ", documentDate = sort == "date-desc" ? "2025-01-01" : "2027-01-01" }, old.Etag))
      Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
    var remaining = await ReadDocumentContinuation(actor, query + "&limit=2", cursor);
    // A changed anchor itself may be repeated: this API deliberately does not promise a snapshot.
    Assert.Equal(all.Ids.Skip(2), remaining.Where(id => id != anchor));
    var target = await MetadataCreate(actor, "/topics", new { name = "Outside filter" });
    var latest = await MetadataRead(actor, $"/documents/{anchor}");
    using (var moved = await actor.Send(HttpMethod.Patch, $"/documents/{anchor}", new { topicId = target.Id }, latest.Etag))
      Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
    Assert.Equal(all.Ids.Skip(2), await ReadDocumentContinuation(actor, query + "&limit=2", cursor));
    var outside = await MetadataRead(actor, $"/documents/{anchor}");
    using (var movedBack = await actor.Send(HttpMethod.Patch, $"/documents/{anchor}", new { topicId = inbox }, outside.Etag))
      Assert.Equal(HttpStatusCode.OK, movedBack.StatusCode);
    await database.Database.ExecuteSqlInterpolatedAsync($"UPDATE minidocs.documents SET deleted_at = CURRENT_TIMESTAMP WHERE id = {anchor}");
    using (var deleted = await actor.Send(HttpMethod.Get, $"/documents/{anchor}"))
      Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    Assert.Equal(all.Ids.Skip(2), await ReadDocumentContinuation(actor, query + "&limit=2", cursor));
  }

  [Theory]
  [InlineData("漢")]
  [InlineData("😀")]
  [Trait("Package", "D6-FIX03-B1")]
  public async Task MetadataDocumentCursorSupportsMaximumUnicodeTitleLength(string character)
  {
    await using var database = await CreateDatabaseAsync();
    var workspace = Guid.NewGuid();
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(database, "cursor-long-title", password, activeWorkspace: workspace);
    var title = string.Concat(Enumerable.Repeat(character, 255));
    var first = await InsertMetadataDocumentAsync(database, workspace, title);
    var second = await InsertMetadataDocumentAsync(database, workspace, title);
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var actor = await MetadataLogin(client, "cursor-long-title", password);
    const string path = "/documents?sort=title&limit=1";
    var page = await ReadDocumentPage(actor, path);
    Assert.NotNull(page.Next);
    Assert.Equal(new[] { first, second }.Order(), page.Ids.Concat(await ReadDocumentContinuation(actor, path, page.Next)));
  }
}
