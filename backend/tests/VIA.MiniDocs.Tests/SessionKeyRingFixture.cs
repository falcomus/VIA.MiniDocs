using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace VIA.MiniDocs.Tests;

internal sealed class SessionKeyRingFixture : IDisposable
{
  private readonly DirectoryInfo root = CreateFixtureRoot();
  private readonly string password = Guid.NewGuid().ToString("N");
  internal DirectoryInfo Root => root;
  internal string KeyDirectory { get; }
  internal string CertificatePath { get; }

  internal SessionKeyRingFixture()
  {
    MakePrivate(root);
    KeyDirectory = CreateRingDirectory();
    CertificatePath = AddCertificate();
  }

  private static DirectoryInfo CreateFixtureRoot()
  {
    if (!OperatingSystem.IsWindows()) return Directory.CreateTempSubdirectory("minidocs-keyring-");

    // Windows TEMP may grant other identities rights to replace children.
    // Use a disposable directory in the service user's private profile instead.
    var profile = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    return profile.CreateSubdirectory($"minidocs-keyring-{Guid.NewGuid():N}");
  }

  internal string CreateRingDirectory() => MakePrivate(root.CreateSubdirectory(Guid.NewGuid().ToString("N")));

  private static string MakePrivate(DirectoryInfo directory)
  {
    if (OperatingSystem.IsWindows())
    {
      using var identity = WindowsIdentity.GetCurrent();
      var acl = directory.GetAccessControl();
      acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
      acl.SetAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
          InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
      directory.SetAccessControl(acl);
    }
    else
      File.SetUnixFileMode(directory.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    return directory.FullName;
  }

  internal string AddCertificate(bool expired = false)
  {
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=MiniDocs isolated test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var now = DateTimeOffset.UtcNow;
    using var certificate = request.CreateSelfSigned(now.AddDays(-2), now.AddDays(expired ? -1 : 2));
    var path = Path.Combine(root.FullName, $"{Guid.NewGuid():N}.pfx");
    var permissions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
    if (!OperatingSystem.IsWindows()) permissions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    using var file = new FileStream(path, permissions);
    file.Write(certificate.Export(X509ContentType.Pkcs12, password));
    return path;
  }

  internal void Configure(IWebHostBuilder builder, string? active = null, string? previous = null, bool allowCreation = true,
      string? directory = null)
  {
    builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Security:KeyRing:Directory"] = directory ?? KeyDirectory,
      ["Security:KeyRing:ApplicationName"] = "isolated",
      ["Security:KeyRing:CertificateFile"] = active ?? CertificatePath,
      ["Security:KeyRing:CertificatePassword"] = password,
      ["Security:KeyRing:AllowInitialCreation"] = allowCreation ? "true" : "false",
      ["Security:KeyRing:PreviousCertificates:0:File"] = previous,
      ["Security:KeyRing:PreviousCertificates:0:Password"] = previous is null ? null : password
    }.Where(kv => kv.Value is not null)));
  }

  public void Dispose() => root.Delete(recursive: true);
}
