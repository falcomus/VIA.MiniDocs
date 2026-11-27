using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using VIA.MiniDocs.Api.Security;
using VIA.MiniDocs.Infrastructure.ViaDocs;

var localOperation = args is ["--bootstrap-administrator"] or ["--cleanup-sessions"];
var builder = WebApplication.CreateBuilder(localOperation ? [] : args);
builder.Services.AddProblemDetails();
builder.Services.AddViaDocs(builder.Configuration);
builder.AddMiniDocsSecurity(localOperation);
if (!localOperation)
    builder.Services.AddTechnicalProvisioning(builder.Configuration, !builder.Environment.IsEnvironment("ContractTests"));
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddOperationTransformer(async (operation, context, token) =>
{
    var body = context.Description.ActionDescriptor.EndpointMetadata.OfType<JsonRequest>().SingleOrDefault()?.Body;
    if (context.Description.RelativePath == "api/v1/session/login") body = typeof(Login);
    if (body is not null)
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = await context.GetOrCreateSchemaAsync(body, cancellationToken: token) }
            }
        };
    if (context.Description.HttpMethod == "GET" &&
        context.Description.RelativePath is "api/v1/admin/users" or "api/v1/admin/invitations" or "api/v1/admin/provisioning")
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter { Name = "cursor", In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 2048 } });
        operation.Parameters.Add(new OpenApiParameter { Name = "limit", In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = "1", Maximum = "100" } });
        if (context.Description.RelativePath == "api/v1/admin/provisioning")
            operation.Parameters.Add(new OpenApiParameter { Name = "workspaceId", In = ParameterLocation.Query,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" } });
    }
    if (context.Description.HttpMethod == "PATCH" && context.Description.RelativePath == "api/v1/admin/users/{id:guid}")
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter { Name = "If-Match", In = ParameterLocation.Header, Required = true,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String } });
        ((OpenApiResponse)operation.Responses!["200"]).Headers = new Dictionary<string, IOpenApiHeader>
        { ["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } } };
    }
    if (context.Description.ActionDescriptor.EndpointMetadata.OfType<ValidateSessionAntiforgery>().Any() &&
        (context.Description.RelativePath == "api/v1/registration" ||
         context.Description.RelativePath?.StartsWith("api/v1/admin/", StringComparison.Ordinal) == true))
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter { Name = SessionHttp.AntiforgeryHeader, In = ParameterLocation.Header,
            Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } });
    }
    if (context.Description.HttpMethod == "POST" && context.Description.RelativePath == "api/v1/admin/invitations")
        ((OpenApiResponse)operation.Responses!["201"]).Headers = new Dictionary<string, IOpenApiHeader>
        { ["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } } };
    if (context.Description.HttpMethod == "POST" && operation.Responses?.TryGetValue("202", out var accepted) == true)
        ((OpenApiResponse)accepted).Headers = new Dictionary<string, IOpenApiHeader>
        {
            ["Location"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
            ["Retry-After"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } }
        };
    if (context.Description.RelativePath == "api/v1/_contract-fixtures/nullable")
        ((OpenApiResponse)operation.Responses!["200"]).Headers = new Dictionary<string, IOpenApiHeader>
        { ["ETag"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } } };
    if (context.Description.RelativePath == "api/v1/_contract-fixtures/binary")
    {
        foreach (var code in new[] { "200", "206" })
        {
            operation.Responses![code].Content!["application/pdf"].Schema =
                new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
        }
        foreach (var code in new[] { "206", "416" })
            ((OpenApiResponse)operation.Responses![code]).Headers = new Dictionary<string, IOpenApiHeader>
            { ["Content-Range"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } } };
    }
}));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
if (localOperation)
{
    await using (app)
        Environment.ExitCode = await LocalSecurityOperations.RunAsync(app.Services, args[0]);
    return;
}
app.UseForwardedHeaders();
app.UseExceptionHandler();
if (app.Environment.IsProduction()) app.UseHsts();
app.UseRouting();
app.UseMiddleware<SessionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SessionAntiforgeryMiddleware>();
app.MapMiniDocsSessions();
app.MapD4B();
app.MapD4C();
if (app.Environment.IsEnvironment("ContractTests"))
{
    app.MapOpenApi();
    var fixtures = app.MapGroup("/api/v1/_contract-fixtures");
    fixtures.MapGet("/nullable", (HttpResponse response) =>
    {
        response.Headers.ETag = "\"fixture\"";
        return new ContractFixture(null, FixtureState.Ready);
    }).Produces<ContractFixture>().ProducesProblem(500);
    fixtures.MapPost("/multipart", (IFormFile file) => Results.Ok(new UploadFixture(file.Length)))
        .DisableAntiforgery().Produces<UploadFixture>().ProducesProblem(400);
    fixtures.MapGet("/binary", ([FromHeader(Name = "If-Range")] string? ifRange, [FromHeader(Name = "Range")] string? range) => Results.File(new byte[] { 1, 2, 3 }, "application/pdf", enableRangeProcessing: true))
        .Produces<byte[]>(200, contentType: "application/pdf").Produces<byte[]>(206, contentType: "application/pdf").Produces(416);
    fixtures.MapGet("/error", IResult () => throw new InvalidOperationException("sensitive fixture detail"))
        .ProducesProblem(StatusCodes.Status500InternalServerError);
}
app.Run();

public sealed record UploadFixture(long Length);
public enum FixtureState { Ready, Pending }
public sealed record ContractFixture(string? Note, FixtureState State);
public sealed class SafeExceptionHandler(ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError("Unhandled request failure {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            statusCode: 500,
            title: "An unexpected error occurred.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "internal_error",
                ["traceId"] = context.TraceIdentifier
            })
            .ExecuteAsync(context);
        return true;
    }
}
public partial class Program;
