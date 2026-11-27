using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

public sealed class ProvisioningProtection(IDataProtectionProvider protection)
{
  internal const int MaximumCiphertextBytes = 4096;
  private static ReadOnlySpan<byte> CredentialFormat => "MDC1"u8;
  private static ReadOnlySpan<byte> InvitationFormat => "MDI1"u8;

  public static bool IsInvitation(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

  internal static bool HasCredentialFormat(byte[]? value) => HasFormat(value, CredentialFormat);
  internal static bool HasInvitationFormat(byte[]? value) => HasFormat(value, InvitationFormat);

  internal byte[] CreateCredential(Guid workspace, Guid account, long generation)
  {
    var random = RandomNumberGenerator.GetBytes(32);
    try { return Protect(Credential(workspace, account, generation), WebEncoders.Base64UrlEncode(random), CredentialFormat); }
    finally { CryptographicOperations.ZeroMemory(random); }
  }

  internal byte[] ProtectInvitation(Guid workspace, Guid operation, string invitation)
  {
    if (!IsInvitation(invitation)) throw new ArgumentException("Invalid technical invitation.", nameof(invitation));
    return Protect(Invitation(workspace, operation), invitation, InvitationFormat);
  }

  internal string ReadCredential(Guid workspace, Guid account, long generation, byte[] value)
  {
    var secret = Read(Credential(workspace, account, generation), value, CredentialFormat, 43);
    try
    {
      var decoded = WebEncoders.Base64UrlDecode(secret);
      try
      {
        if (decoded.Length == 32 && WebEncoders.Base64UrlEncode(decoded) == secret) return secret;
      }
      finally { CryptographicOperations.ZeroMemory(decoded); }
    }
    catch (FormatException) { }
    throw new CryptographicException("Invalid technical credential format.");
  }

  internal string ReadInvitation(Guid workspace, Guid operation, byte[] value)
  {
    var invitation = Read(Invitation(workspace, operation), value, InvitationFormat, 64);
    return IsInvitation(invitation) ? invitation : throw new CryptographicException("Invalid technical invitation format.");
  }

  private IDataProtector Credential(Guid workspace, Guid account, long generation) =>
      protection.CreateProtector("VIA.MiniDocs.ViaDocsCredential", "v1", workspace.ToString("D"), account.ToString("D"),
          generation.ToString(System.Globalization.CultureInfo.InvariantCulture));

  private IDataProtector Invitation(Guid workspace, Guid operation) =>
      protection.CreateProtector("VIA.MiniDocs.ViaDocsInvitation", "v1", workspace.ToString("D"), operation.ToString("D"));

  private static bool HasFormat(byte[]? value, ReadOnlySpan<byte> format) =>
      value is { Length: > 4 and <= MaximumCiphertextBytes } && value.AsSpan().StartsWith(format);

  private static byte[] Protect(IDataProtector protector, string value, ReadOnlySpan<byte> format)
  {
    var cleartext = Encoding.ASCII.GetBytes(value);
    try
    {
      var ciphertext = protector.Protect(cleartext);
      if (ciphertext.Length > MaximumCiphertextBytes - format.Length)
        throw new CryptographicException("Technical protection envelope exceeds its limit.");
      return [.. format, .. ciphertext];
    }
    finally { CryptographicOperations.ZeroMemory(cleartext); }
  }

  private static string Read(IDataProtector protector, byte[] value, ReadOnlySpan<byte> format, int expectedLength)
  {
    if (!HasFormat(value, format)) throw new CryptographicException("Unsupported technical protection envelope.");
    var cleartext = protector.Unprotect(value[format.Length..]);
    try
    {
      if (cleartext.Length != expectedLength || !Ascii.IsValid(cleartext))
        throw new CryptographicException("Invalid technical protection payload.");
      return Encoding.ASCII.GetString(cleartext);
    }
    finally { CryptographicOperations.ZeroMemory(cleartext); }
  }
}
