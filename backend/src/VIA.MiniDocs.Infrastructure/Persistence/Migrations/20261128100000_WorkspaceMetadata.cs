using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VIA.MiniDocs.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MiniDocsDbContext))]
[Migration("20261128100000_WorkspaceMetadata")]
public sealed class WorkspaceMetadata : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    using var stream = typeof(WorkspaceMetadata).Assembly.GetManifestResourceStream(
        "VIA.MiniDocs.Infrastructure.Persistence.Migrations.D6.WorkspaceMetadata.sql")
        ?? throw new InvalidOperationException("Missing workspace metadata migration.");
    using var reader = new StreamReader(stream);
    migrationBuilder.Sql(reader.ReadToEnd());
  }

  protected override void Down(MigrationBuilder migrationBuilder) =>
      throw new NotSupportedException("Restore the verified pre-upgrade backup with writes stopped.");
}
