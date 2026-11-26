using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VIA.MiniDocs.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MiniDocsDbContext))]
[Migration("20261126170000_EnforceWriteBoundaries")]
public sealed class EnforceWriteBoundaries : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    foreach (var script in new[] { "Foundation", "Provisioning", "Documents", "Files", "Privileges" })
      migrationBuilder.Sql(Read(script));
  }

  protected override void Down(MigrationBuilder migrationBuilder) =>
      throw new NotSupportedException("Restore the verified pre-upgrade backup with writes disabled.");

  private static string Read(string name)
  {
    using var stream = typeof(EnforceWriteBoundaries).Assembly.GetManifestResourceStream(
        $"VIA.MiniDocs.Infrastructure.Persistence.Migrations.WriteBoundary.{name}.sql")
        ?? throw new InvalidOperationException($"Missing migration resource: {name}.");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }
}
