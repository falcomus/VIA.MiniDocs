using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace VIA.MiniDocs.Infrastructure.Identity;

public enum RegistrationStatus { Registered, Invalid, UnavailableInvitation, Conflict, Unknown }
public sealed record RegistrationOutcome(RegistrationStatus Status, Guid UserId = default);

public sealed class Registration(MiniDocsDbContext db, UserManager<MiniDocsUser> users,
    IServiceScopeFactory scopes, ILogger<Registration> logger)
{
  public async Task<RegistrationOutcome> RegisterAsync(byte[] hash, string name, string password, CancellationToken token)
  {
    var user = new MiniDocsUser { Id = Guid.NewGuid(), UserName = name };
    var workspace = Guid.NewGuid();
    var topic = Guid.NewGuid();
    var result = RegistrationStatus.Invalid;
    var committing = false;
    try
    {
      var write = await LocalWriteTransaction.ExecuteAsync(db, async ct =>
      {
        if (!await InvitationPersistence.TryLockValidAsync(db, hash, ct))
          return result = RegistrationStatus.UnavailableInvitation;
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
          return result = created.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
              ? RegistrationStatus.Conflict : RegistrationStatus.Invalid;
        return result = await InvitationPersistence.TryRegisterWorkspaceAsync(db, hash, user.Id, workspace, topic, ct)
            ? RegistrationStatus.Registered : RegistrationStatus.UnavailableInvitation;
      }, value => value == RegistrationStatus.Registered, cancellationToken: token, committing: () => committing = true);
      return write.Status switch
      {
        LocalWriteStatus.Committed => new(RegistrationStatus.Registered, user.Id),
        LocalWriteStatus.ConcurrencyConflict => new(RegistrationStatus.Conflict),
        _ => new(result)
      };
    }
    catch (Exception error) when (committing && (error.GetBaseException() is NpgsqlException or TimeoutException ||
        error is OperationCanceledException))
    {
      await ObserveCommitAsync(user.Id, workspace, topic);
      return new(RegistrationStatus.Unknown);
    }
    catch (DbUpdateException error) when (error.GetBaseException() is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UserNameIndex" })
    {
      return new(RegistrationStatus.Conflict);
    }
  }

  private async Task ObserveCommitAsync(Guid user, Guid workspace, Guid topic)
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await using var scope = scopes.CreateAsyncScope();
    var observer = scope.ServiceProvider.GetRequiredService<MiniDocsDbContext>();
    try
    {
      // This internal reconciliation can only inspect this attempt's exact generated tuple.
      var observed = await observer.Database.SqlQuery<bool>($"""
          SELECT EXISTS (SELECT 1 FROM minidocs.workspaces w
            JOIN minidocs.identity_users u ON u.id = w.owner_user_id
            JOIN minidocs.topics t ON t.workspace_id = w.id
            WHERE u.id = {user} AND w.id = {workspace} AND t.id = {topic}
              AND t.system_key = 'UNASSIGNED') AS "Value"
          """).SingleAsync(timeout.Token);
      logger.LogInformation("Unconfirmed registration commit observed: {Committed}", observed);
    }
    catch (Exception error) when (error.GetBaseException() is NpgsqlException or TimeoutException ||
        error is OperationCanceledException)
    {
      logger.LogWarning("Could not observe the result of an unconfirmed registration commit.");
    }
  }
}
