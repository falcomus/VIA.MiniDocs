using Microsoft.OpenApi;

namespace VIA.MiniDocs.Api.Security;

/// <summary>Publishes the D6 wire contract, including omitted versus explicitly nullable PATCH fields.</summary>
internal static class WorkspaceMetadataSchemas
{
  internal static void Configure(OpenApiSchema schema, Type dto)
  {
    if (schema.Properties is not { } properties) return;
    if (dto == typeof(TopicPatch))
    {
      properties["name"] = Text(1, 255);
      properties["parentId"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "uuid" };
      schema.Required = new HashSet<string>();
    }
    else if (dto == typeof(DocumentPatch))
    {
      properties["title"] = Text(1, 255);
      properties["kind"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = ["meeting", "note"] };
      properties["documentDate"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date" };
      properties["topicId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };
      schema.Required = new HashSet<string>();
    }
    else if (dto == typeof(TagCreate) || dto == typeof(TagPatch))
    {
      properties["name"] = Text(1, 100);
      properties["description"] = Text(0, 1000);
      properties["color"] = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^#[0-9A-Fa-f]{6}$" };
      schema.Required = dto == typeof(TagCreate) ? new HashSet<string> { "name", "description", "color" } : new HashSet<string>();
    }
    else if (dto == typeof(TopicCreate))
    {
      properties["name"] = Text(1, 255);
      // Creation at the root also accepts omission, as used by existing clients.
      schema.Required = new HashSet<string> { "name" };
    }
    else if (dto == typeof(DocumentDto))
    {
      properties["kind"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = ["meeting", "note"] };
      properties["availability"] = new OpenApiSchema { Type = JsonSchemaType.String,
        Enum = ["pending_upload", "upload_failed", "available", "missing", "delete_pending"] };
      properties["pageCount"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null,
        Format = "int32", Minimum = "1", Maximum = "100000" };
    }
  }

  private static OpenApiSchema Text(int minimum, int maximum) => new()
  {
    Type = JsonSchemaType.String, MinLength = minimum, MaxLength = maximum
  };
}
