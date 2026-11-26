using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VIA.MiniDocs.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MiniDocsDbContext>
{
  public MiniDocsDbContext CreateDbContext(string[] args)
  {
    var connection = Environment.GetEnvironmentVariable("MINIDOCS_DB_CONNECTION") ?? "Host=localhost;Database=minidocs_design;Username=minidocs;Password=design_only";
    var options = new DbContextOptionsBuilder<MiniDocsDbContext>()
     .UseNpgsql(connection, postgres => postgres.SetPostgresVersion(18, 7))
     .Options;
    return new MiniDocsDbContext(options);
  }
}
