using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VIA.MiniDocs.Infrastructure;
using Xunit;

namespace VIA.MiniDocs.Tests;

/// <summary>Own disposable PostgreSQL container; never accepts a caller-supplied database connection.</summary>
public sealed class PostgresTestDatabase : IAsyncLifetime
{
  private const string Image = "postgres:18.7@sha256:c699303802144847d238196173655dbbecf7a96aeca1565f2b02eb671a1f0b6b";
  private readonly string containerName = $"minidocs-test-{Guid.NewGuid():N}";
  private readonly string password = Guid.NewGuid().ToString("N");
  private readonly string runtimePassword = Guid.NewGuid().ToString("N");
  private readonly string migratorPassword = Guid.NewGuid().ToString("N");
  private string? containerId;
  private string? adminConnectionString;

  public async Task InitializeAsync()
  {
    try
    {
      containerId = await DockerCoreAsync(["run", "--detach", "--rm", "--name", containerName,
          "--label", "via.minidocs.test=true",
          "--label", $"via.minidocs.test.run={containerName}", "--publish", "127.0.0.1::5432",
          "--tmpfs", "/var/lib/postgresql:rw", "--env", "POSTGRES_USER=minidocs_test",
          "--env", "POSTGRES_PASSWORD", Image], password);

      var binding = await DockerAsync("port", containerId, "5432/tcp");
      const string prefix = "127.0.0.1:";
      if (!binding.StartsWith(prefix, StringComparison.Ordinal) ||
          !int.TryParse(binding[prefix.Length..], out var port) || port is < 1 or > 65535)
      {
        throw new InvalidOperationException("Expected exactly one loopback PostgreSQL port binding.");
      }

      adminConnectionString = new NpgsqlConnectionStringBuilder
      {
        Host = "127.0.0.1",
        Port = port,
        Database = "postgres",
        Username = "minidocs_test",
        Password = password,
        Pooling = false,
        Timeout = 2,
        CommandTimeout = 10
      }.ConnectionString;

      await WaitUntilReadyAsync(adminConnectionString);
    }
    catch (Exception startupError)
    {
      try
      {
        await DisposeAsync();
      }
      catch (Exception cleanupError)
      {
        throw new AggregateException("PostgreSQL test startup and cleanup failed.", startupError, cleanupError);
      }
      throw;
    }
  }

  public async Task<MiniDocsDbContext> CreateDatabaseAsync()
  {
    var connectionString = adminConnectionString
        ?? throw new InvalidOperationException("The isolated PostgreSQL container is not ready.");
    var database = $"minidocs_test_{Guid.NewGuid():N}";
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    // Identifier is generated locally from a UUID, not from caller input.
    await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
    await command.ExecuteNonQueryAsync();

    return CreateContext(database);
  }

  /// <summary>A separate connection to a database owned by this fixture.</summary>
  internal MiniDocsDbContext CreatePeer(MiniDocsDbContext db) => CreateContext(db.Database.GetDbConnection().Database);

  internal MiniDocsDbContext CreateRuntimePeer(MiniDocsDbContext db) =>
      CreateRoleContext(db, "minidocs_runtime", runtimePassword);

  internal MiniDocsDbContext CreateMigratorPeer(MiniDocsDbContext db) =>
      CreateRoleContext(db, "minidocs_migrator", migratorPassword);

  internal static Task VerifyPermissionsAsync(MiniDocsDbContext db) =>
      db.Database.ExecuteSqlRawAsync(PostgresRoleAndRestoreTests.ReadRoleScript("GrantRuntimePrivileges.sql"));

  internal async Task PrepareRuntimeAsync(MiniDocsDbContext db)
  {
    // Roles are cluster-wide; every class fixture owns an isolated cluster.
    await using var command = db.Database.GetDbConnection().CreateCommand();
    command.CommandText = $"""
        DO $$
        BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'minidocs_runtime') THEN
            CREATE ROLE minidocs_runtime LOGIN NOINHERIT PASSWORD '{runtimePassword}';
            CREATE ROLE minidocs_migrator LOGIN NOINHERIT PASSWORD '{migratorPassword}';
          END IF;
        END;
        $$
        """;
    await db.Database.OpenConnectionAsync();
    try { await command.ExecuteNonQueryAsync(); }
    finally { await db.Database.CloseConnectionAsync(); }
    await db.Database.ExecuteSqlRawAsync(PostgresRoleAndRestoreTests.ReadRoleScript("PrepareDatabaseRoles.sql"));
  }

  internal MiniDocsDbContext CreateRoleContext(MiniDocsDbContext db, string role, string rolePassword)
  {
    if (role is not ("minidocs_migrator" or "minidocs_runtime"))
      throw new ArgumentException("Expected a dedicated MiniDocs test role.", nameof(role));
    return CreateContext(db.Database.GetDbConnection().Database, role, rolePassword);
  }

  /// <summary>Dump and restore within this fixture's disposable container only.</summary>
  internal async Task RestoreCopyAsync(MiniDocsDbContext source, MiniDocsDbContext target)
  {
    var from = source.Database.GetDbConnection().Database;
    var to = target.Database.GetDbConnection().Database;
    // Validate names and require the fixture's own admin connection and container.
    await using var checkedSource = CreateContext(from);
    await using var checkedTarget = CreateContext(to);
    if (from == to || containerId is null)
      throw new InvalidOperationException("A fresh fixture database and owned container are required.");
    var id = containerId;
    var archive = $"/tmp/minidocs-{Guid.NewGuid():N}.dump";
    try
    {
      await DockerAsync("exec", id, "pg_dump", "--username=minidocs_test", "--format=custom",
          $"--file={archive}", $"--dbname={from}");
      await DockerAsync("exec", id, "pg_restore", "--username=minidocs_test",
          "--exit-on-error", "--single-transaction", $"--dbname={to}", archive);
    }
    finally
    {
      await DockerAsync("exec", id, "rm", "-f", archive);
    }
  }

  private MiniDocsDbContext CreateContext(string database, string? role = null, string? rolePassword = null)
  {
    const string prefix = "minidocs_test_";
    if (!database.StartsWith(prefix, StringComparison.Ordinal) ||
        !Guid.TryParseExact(database[prefix.Length..], "N", out _))
    {
      throw new InvalidOperationException("Expected a fixture-created test database name.");
    }
    var connectionString = adminConnectionString
        ?? throw new InvalidOperationException("The isolated PostgreSQL container is not ready.");
    var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = database };
    if (role is not null)
    {
      builder.Username = role;
      builder.Password = rolePassword ?? throw new ArgumentNullException(nameof(rolePassword));
    }
    var options = new DbContextOptionsBuilder<MiniDocsDbContext>()
        .UseNpgsql(builder.ConnectionString,
            postgres => postgres.SetPostgresVersion(18, 7))
        .Options;
    return new MiniDocsDbContext(options);
  }

  public async Task DisposeAsync()
  {
    adminConnectionString = null;
    // The unique immutable run label lets us recover our own container even when
    // docker run failed before returning its ID. Never sweep shared containers.
    var owned = await DockerAsync("ps", "--all", "--quiet", "--no-trunc",
        "--filter", $"label=via.minidocs.test.run={containerName}");
    var ids = owned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (ids.Length > 1 || ids.Any(id => id.Length != 64 || !id.All(Uri.IsHexDigit)) ||
        (ids.Length == 1 && containerId is not null && ids[0] != containerId))
    {
      throw new InvalidOperationException("Ambiguous PostgreSQL test container ownership; cleanup refused.");
    }
    if (ids.Length == 1)
    {
      await DockerAsync("rm", "--force", ids[0]);
    }
    containerId = null;
  }

  private static async Task WaitUntilReadyAsync(string connectionString)
  {
    var elapsed = Stopwatch.StartNew();
    while (elapsed.Elapsed < TimeSpan.FromSeconds(90))
    {
      try
      {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SHOW server_version_num", connection);
        var version = await command.ExecuteScalarAsync();
        if (version is not "180007")
        {
          throw new InvalidOperationException("Expected PostgreSQL 18.7 in the isolated test container.");
        }
        return;
      }
      catch (NpgsqlException) when (elapsed.Elapsed < TimeSpan.FromSeconds(90))
      {
        await Task.Delay(TimeSpan.FromMilliseconds(500));
      }
      catch (TimeoutException) when (elapsed.Elapsed < TimeSpan.FromSeconds(90))
      {
        await Task.Delay(TimeSpan.FromMilliseconds(500));
      }
    }
    throw new TimeoutException("The isolated PostgreSQL test container did not become ready.");
  }

  private static Task<string> DockerAsync(params string[] arguments) => DockerCoreAsync(arguments);

  private static async Task<string> DockerCoreAsync(string[] arguments, string? containerPassword = null)
  {
    var start = new ProcessStartInfo("docker")
    {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true
    };
    foreach (var argument in arguments)
    {
      start.ArgumentList.Add(argument);
    }
    if (containerPassword is not null)
      start.Environment["POSTGRES_PASSWORD"] = containerPassword;
    using var process = Process.Start(start)
        ?? throw new InvalidOperationException("Docker CLI could not be started.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    try
    {
      await process.WaitForExitAsync(timeout.Token);
    }
    catch (OperationCanceledException)
    {
      if (!process.HasExited)
      {
        process.Kill(entireProcessTree: true);
      }
      await process.WaitForExitAsync();
      throw new TimeoutException($"Docker {arguments[0]} timed out.");
    }
    var output = await stdout;
    _ = await stderr;
    if (process.ExitCode != 0)
    {
      // Do not echo arguments or arbitrary command output containing ephemeral credentials.
      throw new InvalidOperationException($"Docker {arguments[0]} failed (exit {process.ExitCode}).");
    }
    return output.Trim();
  }
}
