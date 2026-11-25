using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics;
using Xunit;

namespace VIA.MiniDocs.Tests;

public class HostTests
{
    private static WebApplicationFactory<Program> Host(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment(environment));

    [Fact]
    public void FixtureJsonPreservesNullAndStringEnum()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var value = JsonSerializer.Serialize(new ContractFixture(null, FixtureState.Pending), options);
        Assert.Equal("{\"note\":null,\"state\":\"Pending\"}", value);
    }

    [Fact]
    public async Task ProductionDoesNotExposeFixtures()
    {
        using var host = Host("Production");
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/_contract-fixtures/nullable")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public void ErrorHandlerRegisteredInDependencyInjection()
    {
        using var host = Host("Production");
        Assert.Contains(host.Services.GetServices<IExceptionHandler>(), x => x is SafeExceptionHandler);
    }

    [Fact]
    public async Task ErrorDoesNotDiscloseException()
    {
        using var host = Host("ContractTests");
        using var client = host.CreateClient();
        var response = await client.GetAsync("/api/v1/_contract-fixtures/error");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("sensitive fixture detail", body);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("internal_error", problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ErrorResponseMatchesOpenApiContract()
    {
        using var host = Host("ContractTests");
        using var client = host.CreateClient();
        var response = await client.GetAsync("/api/v1/_contract-fixtures/error");
        var openApi = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var responses = openApi.GetProperty("paths").GetProperty("/api/v1/_contract-fixtures/error")
            .GetProperty("get").GetProperty("responses");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(responses.TryGetProperty(((int)response.StatusCode).ToString(), out var documented));
        Assert.False(responses.TryGetProperty("200", out _));
        Assert.True(documented.GetProperty("content").TryGetProperty("application/problem+json", out _));
    }

    [Fact]
    public async Task NullableEnumAndEtagAreReturned()
    {
        using var host = Host("ContractTests");
        using var client = host.CreateClient();
        var response = await client.GetAsync("/api/v1/_contract-fixtures/nullable");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, json.GetProperty("note").ValueKind);
        Assert.Equal("Ready", json.GetProperty("state").GetString());
        Assert.Equal("\"fixture\"", response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task RangeIsHonoured()
    {
        using var host = Host("ContractTests");
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/_contract-fixtures/binary");
        request.Headers.Range = new RangeHeaderValue(1, 2);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(new byte[] { 2, 3 }, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("bytes 1-2/3", response.Content.Headers.ContentRange?.ToString());
    }


    [Fact]
    public async Task InvalidRangeReturns416()
    {
        using var host = Host("ContractTests");
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/_contract-fixtures/binary");
        request.Headers.Range = new RangeHeaderValue(99, 100);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
        Assert.Equal("bytes */3", response.Content.Headers.ContentRange?.ToString());
    }

    [Fact]
    public async Task MultipartAcceptsUploadedBytes()
    {
        using var host = Host("ContractTests");
        using var client = host.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "fixture.pdf");
        var response = await client.PostAsync("/api/v1/_contract-fixtures/multipart", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("length").GetInt64());
    }

    [Fact]
    public void ProjectReferencesRespectLayerBoundaries()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "VIA.MiniDocs.sln"))) current = current.Parent;
        Assert.NotNull(current);
        string[] References(string project) => System.Xml.Linq.XDocument.Load(Path.Combine(current.FullName, "src", "VIA.MiniDocs." + project, "VIA.MiniDocs." + project + ".csproj"))
            .Descendants("ProjectReference").Select(e => Path.GetFileNameWithoutExtension((string?)e.Attribute("Include"))!).Order().ToArray();
        Assert.Empty(References("Domain"));
        Assert.Equal(new[] { "VIA.MiniDocs.Domain" }, References("Application"));
        Assert.Equal(new[] { "VIA.MiniDocs.Application", "VIA.MiniDocs.Domain" }, References("Infrastructure"));
        Assert.Equal(new[] { "VIA.MiniDocs.Application", "VIA.MiniDocs.Infrastructure" }, References("Api"));
    }

    [Fact]
    public async Task ContractContainsSchemasAndRoutes()
    {
        using var host = Host("ContractTests");
        using var client = host.CreateClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = json.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/_contract-fixtures/multipart", out _));
        Assert.True(paths.TryGetProperty("/api/v1/_contract-fixtures/binary", out _));
        Assert.True(json.GetProperty("components").GetProperty("schemas").TryGetProperty("ProblemDetails", out _));
        var binary = paths.GetProperty("/api/v1/_contract-fixtures/binary").GetProperty("get");
        Assert.Equal("binary", binary.GetProperty("responses").GetProperty("206").GetProperty("content").GetProperty("application/pdf").GetProperty("schema").GetProperty("format").GetString());
        Assert.True(binary.GetProperty("responses").GetProperty("206").GetProperty("headers").TryGetProperty("Content-Range", out _));
        Assert.True(paths.GetProperty("/api/v1/_contract-fixtures/nullable").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("headers").TryGetProperty("ETag", out _));
    }
}
