using System.Net;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  [Fact]
  [Trait("Package", "D6-FIX03-D")]
  public async Task DocumentWorkflowPreservesVersionFailurePrecedenceAndAtomicity()
  {
    await using var database = await CreateDatabaseAsync();
    var password = Guid.NewGuid().ToString("N");
    var workspace = Guid.NewGuid();
    await CreateUserAsync(database, "workflow-version", password, activeWorkspace: workspace);
    var document = await InsertMetadataDocumentAsync(database, workspace, "Own");
    await using var runtime = fixture.CreateRuntimePeer(database);
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys);
    using var client = TestClient(host);
    var actor = await MetadataLogin(client, "workflow-version", password);
    var own = await MetadataResourcesFor(actor, document);
    var before = await MetadataSnapshot(actor);
    var missing = Guid.NewGuid();

    foreach (var (id, validator, expected) in new (Guid, string?, HttpStatusCode)[]
    {
      (missing, null, (HttpStatusCode)428),
      (missing, "invalid", HttpStatusCode.BadRequest),
      (missing, "\"unknown\"", HttpStatusCode.NotFound),
      (document, "\"unknown\"", HttpStatusCode.PreconditionFailed),
      (document, "W/\"0000000000000001\"", HttpStatusCode.BadRequest),
      (document, "*", HttpStatusCode.BadRequest)
    })
    {
      using var response = await actor.Send(HttpMethod.Post, "/documents/batch-move", new
      {
        items = new[] { new { id, etag = validator, targetTopicId = own.Topic } }
      });
      Assert.Equal(expected, response.StatusCode);
    }
    // A valid first item must not survive the rejection of a later validator.
    using var mixed = await actor.Send(HttpMethod.Post, "/documents/batch-move", new
    {
      items = new[] { new { id = document, etag = own.DocumentVersion, targetTopicId = own.Topic },
          new { id = missing, etag = "invalid", targetTopicId = own.Topic } }
    });
    Assert.Equal(HttpStatusCode.BadRequest, mixed.StatusCode);
    Assert.Equal(before, await MetadataSnapshot(actor));
  }
}
