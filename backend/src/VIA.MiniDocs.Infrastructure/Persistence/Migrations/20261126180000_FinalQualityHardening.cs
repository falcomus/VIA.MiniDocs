using System.Globalization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace VIA.MiniDocs.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MiniDocsDbContext))]
[Migration("20261126180000_FinalQualityHardening")]
public sealed class FinalQualityHardening : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    var seconds = D2LeasePolicy.MaximumSeconds.ToString(CultureInfo.InvariantCulture);
    migrationBuilder.Sql($"""
        CREATE FUNCTION minidocs.maximum_lease_duration() RETURNS interval
        LANGUAGE sql IMMUTABLE SET search_path = pg_catalog, pg_temp
        AS $$ SELECT make_interval(secs => {seconds}); $$;
        """);
    migrationBuilder.Sql(Read("Foundation.sql"));
    migrationBuilder.Sql(Read("Invariants.sql"));

    // Existing commands belong to the NOLOGIN writer. CREATE is needed only
    // during replacement. Neither it nor the role switch survives this migration.
    migrationBuilder.Sql("""
        DO $$
        BEGIN
          IF to_regrole('minidocs_command_owner') IS NOT NULL THEN
            GRANT DELETE ON minidocs.pending_transcript_pages TO minidocs_command_owner;
            GRANT UPDATE (source_name, meta_date, meta_topic, meta_title, meta_tags)
              ON minidocs.pending_transcripts TO minidocs_command_owner;
            GRANT EXECUTE ON FUNCTION minidocs.require_command_argument(boolean, text),
              minidocs.valid_lease_duration(interval), minidocs.workspace_accepts_provisioning(uuid),
              minidocs.maximum_lease_duration() TO minidocs_command_owner;
            GRANT CREATE ON SCHEMA minidocs TO minidocs_command_owner;
            SET LOCAL ROLE minidocs_command_owner;
          END IF;
        END;
        $$;
        """);
    foreach (var script in new[] { "Provisioning.sql", "Documents.sql", "Files.sql" })
      migrationBuilder.Sql(Read(script));
    migrationBuilder.Sql("""
        RESET ROLE;
        DO $$
        BEGIN
          IF to_regrole('minidocs_command_owner') IS NOT NULL THEN
            REVOKE CREATE ON SCHEMA minidocs FROM minidocs_command_owner;
          END IF;
        END;
        $$;
        """);
  }

  protected override void Down(MigrationBuilder migrationBuilder) =>
      throw new NotSupportedException("Restore the verified pre-upgrade backup with writes disabled.");

  internal static string Read(string name)
  {
    using var stream = typeof(FinalQualityHardening).Assembly.GetManifestResourceStream(
        $"VIA.MiniDocs.Infrastructure.Persistence.Migrations.FinalHardening.{name}")
        ?? throw new InvalidOperationException($"Missing migration resource: {name}.");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }
}
