using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Infrastructure;

namespace VIA.MiniDocs.Tests;

internal static class PostgresSeed
{
  internal static Task<int> InsertUserAsync(MiniDocsDbContext db, Guid id, string name, string accountKind) =>
      db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO minidocs.identity_users(
              id, user_name, normalized_user_name, account_kind, is_disabled,
              email_confirmed, phone_number_confirmed, two_factor_enabled,
              lockout_enabled, access_failed_count)
          VALUES ({id}, {name}, {name.ToUpperInvariant()}, {accountKind}, false,
              false, false, false, true, 0)
          """);

  internal static Task<int> InsertWorkspaceAsync(MiniDocsDbContext db, Guid owner, Guid workspace) =>
      db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO minidocs.workspaces(id, owner_user_id, status)
          VALUES ({workspace}, {owner}, 'pending')
          """);

  internal static async Task<Guid> InsertUserAndWorkspace(
      MiniDocsDbContext db, Guid owner, Guid workspace, string name)
  {
    await using var transaction = await db.Database.BeginTransactionAsync();
    await InsertUserAsync(db, owner, name, "regular");
    await InsertWorkspaceAsync(db, owner, workspace);
    var systemTopic = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.topics(id, workspace_id, name, system_key)
        VALUES ({systemTopic}, {workspace}, 'Nicht zugeordnet', 'UNASSIGNED')
        """);
    await transaction.CommitAsync();
    return systemTopic;
  }

  internal static async Task<Guid> InsertActiveWorkspaceAsync(
      MiniDocsDbContext db, Guid owner, Guid workspace, string name)
  {
    var topic = await InsertUserAndWorkspace(db, owner, workspace, name);
    var application = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO minidocs.via_docs_accounts(id, workspace_id, application_name,
            application_secret_protected, application_id, verified_at)
        VALUES ({Guid.NewGuid()}, {workspace}, {name}, decode('01','hex'), {application}, clock_timestamp());
        INSERT INTO minidocs.provisioning_operations(id, workspace_id, status, attempt_no, candidate_application_id)
        VALUES ({Guid.NewGuid()}, {workspace}, 'active', 1, {application});
        UPDATE minidocs.workspaces SET status = 'active' WHERE id = {workspace};
        """);
    return topic;
  }
}
