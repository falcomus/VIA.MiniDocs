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
    private static WebApplicationFactory<Program> Host(string environment, SessionKeyRingFixture? keys = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(environment);
            keys?.Configure(b);
        });

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
        using var keys = new SessionKeyRingFixture();
        using var host = Host("Production", keys);
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/_contract-fixtures/nullable")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public void ErrorHandlerRegisteredInDependencyInjection()
    {
        using var keys = new SessionKeyRingFixture();
        using var host = Host("Production", keys);
        Assert.Contains(host.Services.GetServices<IExceptionHandler>(), x => x is SafeExceptionHandler);
    }

    [Fact]
    [Trait("Package", "D4-A")]
    public void ProductionStartupRejectsMissingRing()
    {
        using var host = Host("Production");
        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("key ring directory", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Package", "D4-A")]
    public void ProductionStartupRejectsMissingCertificateAndUninitializedRing()
    {
        using var keys = new SessionKeyRingFixture();
        using var uninitialized = Host("Production").WithWebHostBuilder(builder => keys.Configure(builder, allowCreation: false));
        Assert.Contains("key ring is empty", Assert.Throws<InvalidOperationException>(() => uninitialized.CreateClient()).Message);

        using var invalid = Host("Production").WithWebHostBuilder(builder =>
            keys.Configure(builder, active: Path.Combine(keys.KeyDirectory, "missing.pfx")));
        Assert.Contains("certificate cannot be used", Assert.Throws<InvalidOperationException>(() => invalid.CreateClient()).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Package", "D4-A")]
    public void ProductionStartupRejectsMalformedAndExpiredCertificates(bool expired)
    {
        using var keys = new SessionKeyRingFixture();
        var certificate = expired ? keys.AddCertificate(expired: true) : keys.CertificatePath;
        if (!expired) File.WriteAllText(certificate, "invalid PKCS#12");
        using var host = Host("Production").WithWebHostBuilder(builder => keys.Configure(builder, active: certificate));
        Assert.Contains("certificate cannot be used", Assert.Throws<InvalidOperationException>(() => host.CreateClient()).Message);
    }

    [Theory]
    [InlineData("<key")]
    [InlineData("<unrecognized />")]
    [Trait("Package", "D4-A")]
    public void ProductionStartupRejectsCorruptedKeyFiles(string damagedXml)
    {
        using var keys = new SessionKeyRingFixture();
        using (var original = Host("Production", keys))
        using (original.CreateClient())
        {
            var key = Assert.Single(Directory.GetFiles(keys.KeyDirectory, "key-*.xml"));
            File.WriteAllText(key, damagedXml);
        }
        using var restart = Host("Production").WithWebHostBuilder(builder => keys.Configure(builder, allowCreation: false));
        Assert.Contains("could not be initialized", Assert.Throws<InvalidOperationException>(() => restart.CreateClient()).Message);
    }

    [Fact]
    [Trait("Package", "D4-A")]
    public async Task ForwardedProtocolFromUnknownProxyCannotEnableAnInsecureRequest()
    {
        using var keys = new SessionKeyRingFixture();
        using var host = Host("Production", keys);
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/antiforgery");
        request.Headers.Add("X-Forwarded-For", "192.0.2.20");
        request.Headers.Add("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
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
        // The build output may be outside the repository (MSBuild --artifacts-path).
        var current = FindSolutionDirectory();
        string[] References(string project) => System.Xml.Linq.XDocument.Load(Path.Combine(current.FullName, "src", "VIA.MiniDocs." + project, "VIA.MiniDocs." + project + ".csproj"))
            .Descendants("ProjectReference").Select(e => Path.GetFileNameWithoutExtension((string?)e.Attribute("Include"))!).Order().ToArray();
        Assert.Empty(References("Domain"));
        Assert.Equal(new[] { "VIA.MiniDocs.Domain" }, References("Application"));
        Assert.Equal(new[] { "VIA.MiniDocs.Application", "VIA.MiniDocs.Domain" }, References("Infrastructure"));
        Assert.Equal(new[] { "VIA.MiniDocs.Application", "VIA.MiniDocs.Infrastructure" }, References("Api"));
    }

    private static DirectoryInfo FindSolutionDirectory(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        var directory = Path.GetDirectoryName(sourceFile);
        var current = directory is null ? null : new DirectoryInfo(directory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "VIA.MiniDocs.sln")))
        {
            current = current.Parent;
        }
        return current ?? throw new InvalidOperationException("MiniDocs solution not found from the test source path.");
    }

    [Fact]
    public async Task ContractContainsSchemasAndRoutes()
    {
        using var host = Host("ContractTests");
        using var client = host.CreateClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = json.GetProperty("paths");
        (string Route, string Method, string Success)[] d4 = [
            ("/api/v1/antiforgery", "get", "200"), ("/api/v1/session/login", "post", "200"),
            ("/api/v1/session", "get", "200"), ("/api/v1/session", "delete", "204"),
            ("/api/v1/registration", "post", "201"), ("/api/v1/workspace", "get", "200"),
            ("/api/v1/admin/invitations", "get", "200"), ("/api/v1/admin/invitations", "post", "201"),
            ("/api/v1/admin/invitations/{id}", "get", "200"), ("/api/v1/admin/invitations/{id}", "delete", "204"),
            ("/api/v1/admin/users", "get", "200"), ("/api/v1/admin/users/{id}", "patch", "200"),
            ("/api/v1/admin/provisioning", "get", "200"),
            ("/api/v1/admin/workspaces/{id}/technical-invitation", "post", "202"),
            ("/api/v1/admin/workspaces/{id}/recovery", "post", "202")];
        Assert.Equal(15, d4.Length);
        foreach (var (route, method, success) in d4)
        {
            Assert.True(paths.GetProperty(route).GetProperty(method).GetProperty("responses").TryGetProperty(success, out _));
            var operation = paths.GetProperty(route).GetProperty(method);
            var internalError = operation.GetProperty("responses").GetProperty("500");
            Assert.Equal("#/components/schemas/ProblemDetails", internalError.GetProperty("content")
                .GetProperty("application/problem+json").GetProperty("schema").GetProperty("$ref").GetString());
            Assert.True(internalError.GetProperty("headers").GetProperty("Cache-Control").GetProperty("required").GetBoolean());
            foreach (var response in operation.GetProperty("responses").EnumerateObject())
                Assert.True(response.Value.GetProperty("headers").TryGetProperty("Cache-Control", out _));
            if (method is "post" or "patch" or "delete")
                Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "X-CSRF-TOKEN" && p.GetProperty("required").GetBoolean());
        }
        (string Route, string Method, string[] Errors)[] errors = [
            ("/api/v1/antiforgery", "get", ["503"]), ("/api/v1/session/login", "post", ["400", "401", "403", "413", "415", "423", "429", "503"]),
            ("/api/v1/session", "get", ["401", "503"]), ("/api/v1/session", "delete", ["401", "403", "503"]),
            ("/api/v1/registration", "post", ["400", "403", "409", "410", "413", "415", "429", "503"]), ("/api/v1/workspace", "get", ["401", "404", "503"]),
            ("/api/v1/admin/invitations", "get", ["400", "401", "403", "503"]), ("/api/v1/admin/invitations", "post", ["400", "401", "403", "409", "413", "415", "429", "503"]),
            ("/api/v1/admin/invitations/{id}", "get", ["401", "403", "404", "503"]), ("/api/v1/admin/invitations/{id}", "delete", ["400", "401", "403", "404", "409", "503"]),
            ("/api/v1/admin/users", "get", ["400", "401", "403", "503"]), ("/api/v1/admin/users/{id}", "patch", ["400", "401", "403", "404", "409", "412", "413", "415", "428", "503"]),
            ("/api/v1/admin/provisioning", "get", ["400", "401", "403", "503"]),
            ("/api/v1/admin/workspaces/{id}/technical-invitation", "post", ["400", "401", "403", "404", "409", "413", "415", "429", "503"]),
            ("/api/v1/admin/workspaces/{id}/recovery", "post", ["400", "401", "403", "404", "409", "413", "415", "429", "503"])];
        foreach (var (route, method, statuses) in errors)
        {
            var responses = paths.GetProperty(route).GetProperty(method).GetProperty("responses");
            Assert.Equal(statuses.Append("500").Order(), responses.EnumerateObject()
                .Select(response => response.Name).Where(status => status.StartsWith('4') || status.StartsWith('5')).Order());
            foreach (var status in statuses)
            {
                var error = responses.GetProperty(status);
                Assert.True(error.GetProperty("content").TryGetProperty("application/problem+json", out _));
                if (status == "429")
                    Assert.True(error.GetProperty("headers").GetProperty("Retry-After").GetProperty("required").GetBoolean());
            }
        }
        Assert.True(paths.TryGetProperty("/api/v1/_contract-fixtures/multipart", out _));
        Assert.True(paths.TryGetProperty("/api/v1/_contract-fixtures/binary", out _));
        Assert.True(json.GetProperty("components").GetProperty("schemas").TryGetProperty("ProblemDetails", out _));
        var problem = json.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails");
        foreach (var name in new[] { "code", "traceId" })
        {
            Assert.Equal("string", problem.GetProperty("properties").GetProperty(name).GetProperty("type").GetString());
            Assert.Contains(problem.GetProperty("required").EnumerateArray(), item => item.GetString() == name);
        }
        var invitation = paths.GetProperty("/api/v1/admin/invitations").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.False(invitation.TryGetProperty("required", out var required) &&
            required.EnumerateArray().Any(item => item.GetString() == "expiresAt"));
        foreach (var (route, method, status, header) in new[] {
            ("/api/v1/admin/invitations", "post", "201", "Location"),
            ("/api/v1/admin/users/{id}", "patch", "200", "ETag"),
            ("/api/v1/admin/workspaces/{id}/technical-invitation", "post", "202", "Location"),
            ("/api/v1/admin/workspaces/{id}/technical-invitation", "post", "202", "Retry-After"),
            ("/api/v1/admin/workspaces/{id}/recovery", "post", "202", "Location"),
            ("/api/v1/admin/workspaces/{id}/recovery", "post", "202", "Retry-After") })
            Assert.True(paths.GetProperty(route).GetProperty(method).GetProperty("responses")
                .GetProperty(status).GetProperty("headers").GetProperty(header).GetProperty("required").GetBoolean());
        Assert.Contains(paths.GetProperty("/api/v1/admin/users/{id}").GetProperty("patch").GetProperty("parameters").EnumerateArray(),
            p => p.GetProperty("name").GetString() == "If-Match" && p.GetProperty("required").GetBoolean());
        var binary = paths.GetProperty("/api/v1/_contract-fixtures/binary").GetProperty("get");
        Assert.Equal("binary", binary.GetProperty("responses").GetProperty("206").GetProperty("content").GetProperty("application/pdf").GetProperty("schema").GetProperty("format").GetString());
        Assert.True(binary.GetProperty("responses").GetProperty("206").GetProperty("headers").TryGetProperty("Content-Range", out _));
        Assert.True(paths.GetProperty("/api/v1/_contract-fixtures/nullable").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("headers").TryGetProperty("ETag", out _));
    }
}
