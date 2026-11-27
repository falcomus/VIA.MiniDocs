using Microsoft.AspNetCore.Identity;

namespace VIA.MiniDocs.Infrastructure.Identity;

public static class IdentityInput
{
  public static bool IsUserName(string? value) => value is { Length: > 0 and <= 256 } &&
      !value.Any(char.IsControl) && !string.IsNullOrWhiteSpace(value);
}

public sealed class MiniDocsPasswordValidator : IPasswordValidator<MiniDocsUser>
{
  private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
  {
    "password1234", "password12345", "password123456", "Passwort1234", "Passwort12345",
    "123456789012", "1234567890123", "qwerty123456", "qwertz123456", "administrator",
    "letmein123456", "correcthorsebatterystaple"
  };

  public Task<IdentityResult> ValidateAsync(UserManager<MiniDocsUser> manager, MiniDocsUser user, string? password)
  {
    var acceptable = password is { Length: >= 12 and <= 128 } &&
        !string.IsNullOrWhiteSpace(password) &&
        !CommonPasswords.Contains(password) &&
        !string.Equals(user.UserName, password, StringComparison.OrdinalIgnoreCase);
    return Task.FromResult(acceptable ? IdentityResult.Success : IdentityResult.Failed(new IdentityError
    {
      Code = "PasswordPolicy",
      Description = "The password does not meet the password policy."
    }));
  }
}
