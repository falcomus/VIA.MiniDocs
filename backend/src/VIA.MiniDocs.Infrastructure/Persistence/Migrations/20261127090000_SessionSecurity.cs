using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VIA.MiniDocs.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MiniDocsDbContext))]
[Migration("20261127090000_SessionSecurity")]
public sealed class SessionSecurity : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    using var stream = typeof(SessionSecurity).Assembly.GetManifestResourceStream(
        "VIA.MiniDocs.Infrastructure.Persistence.Migrations.D4.SessionSecurity.sql")
        ?? throw new InvalidOperationException("Missing D4 session migration.");
    using var reader = new StreamReader(stream);
    migrationBuilder.Sql(reader.ReadToEnd());
  }

  protected override void Down(MigrationBuilder migrationBuilder) =>
      throw new NotSupportedException("Restore the verified pre-upgrade backup with writes disabled.");
}
