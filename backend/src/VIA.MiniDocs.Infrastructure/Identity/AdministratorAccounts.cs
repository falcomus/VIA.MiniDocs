using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure.Identity;

public enum DeactivationResult : short
{
  Applied,
  Forbidden,
  Missing,
  VersionMismatch,
  LastAdministrator
}

public sealed record AdministratorAccountVersion(Guid Id, string? UserName, AccountKind AccountKind,
    bool IsDisabled, string? ConcurrencyStamp, string[] Roles)
{
  public string ETag => $"\"{Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
      new { Id, UserName, AccountKind, IsDisabled, ConcurrencyStamp, Roles })))}\"";
}

public sealed class AdministratorAccounts(MiniDocsDbContext db)
{
  public const string Role = "ADMINISTRATOR";

  public Task<AdministratorAccountVersion?> ReadVersionAsync(Guid target, CancellationToken cancellationToken) =>
      db.Users.AsNoTracking().Where(u => u.Id == target)
          .Select(u => new AdministratorAccountVersion(u.Id, u.UserName, u.AccountKind, u.IsDisabled,
              u.ConcurrencyStamp, (from membership in db.UserRoles
                                  join role in db.Roles on membership.RoleId equals role.Id
                                  where membership.UserId == u.Id && role.NormalizedName != null
                                  orderby role.NormalizedName
                                  select role.NormalizedName!).ToArray()))
          .SingleOrDefaultAsync(cancellationToken);

  public async Task<DeactivationResult> SetDisabledAsync(SessionAuthority actor, Guid target, string ifMatch,
      bool disabled, CancellationToken cancellationToken)
  {
    var expected = await ReadVersionAsync(target, cancellationToken);
    // Missing and stale results are evaluated behind the SQL authorization point as well.
    var stamp = expected?.ETag == ifMatch ? expected?.ConcurrencyStamp : null;
    var roles = expected?.Roles ?? [];
    var result = await db.Database.SqlQuery<short>($"""
        SELECT minidocs.command_set_account_disabled({actor.UserId}, {actor.Stamp}, {actor.HandleHash},
          {target}, {stamp}, {roles}, {disabled}) AS "Value"
        """).SingleAsync(cancellationToken);
    return (DeactivationResult)result;
  }
}

public sealed class AdministratorBootstrap(MiniDocsDbContext db, UserManager<MiniDocsUser> users,
    IEnumerable<IPasswordValidator<MiniDocsUser>> passwords)
{
  public async Task<bool> CreateAsync(string userName, string password, CancellationToken cancellationToken)
  {
    if (!IdentityInput.IsUserName(userName) ||
        userName.Any(character => !users.Options.User.AllowedUserNameCharacters.Contains(character)))
      return false;

    var admin = new MiniDocsUser { Id = Guid.NewGuid(), UserName = userName };
    foreach (var validator in passwords)
    {
      if (!(await validator.ValidateAsync(users, admin, password)).Succeeded) return false;
    }

    var normalized = users.NormalizeName(userName);
    var hash = users.PasswordHasher.HashPassword(admin, password);
    return await db.Database.SqlQuery<bool>($"""
        SELECT minidocs.command_bootstrap_administrator({admin.Id}, {userName}, {normalized}, {hash}) AS "Value"
        """).SingleAsync(cancellationToken);
  }
}
