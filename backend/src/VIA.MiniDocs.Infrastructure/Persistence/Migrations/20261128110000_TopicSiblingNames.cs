using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VIA.MiniDocs.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MiniDocsDbContext))]
[Migration("20261128110000_TopicSiblingNames")]
public sealed class TopicSiblingNames : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    using var stream = typeof(TopicSiblingNames).Assembly.GetManifestResourceStream(
        "VIA.MiniDocs.Infrastructure.Persistence.Migrations.D6.TopicSiblingNames.sql")
        ?? throw new InvalidOperationException("Missing topic sibling names migration.");
    using var reader = new StreamReader(stream);
    migrationBuilder.Sql(reader.ReadToEnd());
  }

  protected override void Down(MigrationBuilder migrationBuilder) =>
      throw new NotSupportedException("Restore the verified pre-upgrade backup with writes stopped.");
}
