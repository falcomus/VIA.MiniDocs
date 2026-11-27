using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VIA.MiniDocs.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MiniDocsDbContext))]
[Migration("20261127100000_TechnicalProvisioning")]
public sealed class TechnicalProvisioning : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    using var stream = typeof(TechnicalProvisioning).Assembly.GetManifestResourceStream(
        "VIA.MiniDocs.Infrastructure.Persistence.Migrations.D4.TechnicalProvisioning.sql")
        ?? throw new InvalidOperationException("Missing technical provisioning migration.");
    using var reader = new StreamReader(stream);
    migrationBuilder.Sql(reader.ReadToEnd());
  }

  protected override void Down(MigrationBuilder migrationBuilder) =>
      throw new NotSupportedException("Restore the verified pre-upgrade backup with workers and writes stopped.");
}
