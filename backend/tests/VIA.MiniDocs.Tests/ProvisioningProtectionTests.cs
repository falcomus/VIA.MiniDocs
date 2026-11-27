using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using VIA.MiniDocs.Infrastructure.ViaDocs;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Package", "D4-C")]
public sealed class ProvisioningProtectionTests
{
  [Fact]
  public void EnvelopesRequireTheirPurposeAndCurrentWorkspaceAccountGenerationOrOperation()
  {
    var provider = new EphemeralDataProtectionProvider();
    var protection = new ProvisioningProtection(provider);
    var workspace = Guid.NewGuid();
    var account = Guid.NewGuid();
    var operation = Guid.NewGuid();
    var token = new string('c', 64);
    var credential = protection.CreateCredential(workspace, account, 1);
    var invitation = protection.ProtectInvitation(workspace, operation, token);
    Assert.Equal(43, protection.ReadCredential(workspace, account, 1, credential).Length);
    Assert.Equal(token, protection.ReadInvitation(workspace, operation, invitation));
    Assert.Throws<CryptographicException>(() => protection.ReadCredential(Guid.NewGuid(), account, 1, credential));
    Assert.Throws<CryptographicException>(() => protection.ReadCredential(workspace, Guid.NewGuid(), 1, credential));
    Assert.Throws<CryptographicException>(() => protection.ReadCredential(workspace, account, 2, credential));
    Assert.Throws<CryptographicException>(() => protection.ReadInvitation(workspace, Guid.NewGuid(), invitation));
    Assert.Throws<CryptographicException>(() => protection.ReadInvitation(Guid.NewGuid(), operation, invitation));
    Assert.Throws<CryptographicException>(() => protection.ReadCredential(workspace, account, 1, invitation));
    Assert.Throws<CryptographicException>(() => protection.ReadInvitation(workspace, operation, new byte[4097]));
    invitation[^1] ^= 1;
    Assert.Throws<CryptographicException>(() => protection.ReadInvitation(workspace, operation, invitation));
    var purpose = provider.CreateProtector("VIA.MiniDocs.ViaDocsCredential", "v1", workspace.ToString("D"), account.ToString("D"), "1");
    var forged = purpose.Protect(new byte[43]);
    Assert.Throws<CryptographicException>(() => protection.ReadCredential(workspace, account, 1, [.. "MDC1"u8, .. forged]));
    Assert.Throws<ArgumentException>(() => protection.ProtectInvitation(workspace, operation, token + "0"));
  }

  [Fact]
  public void CredentialRotationAndFileRestoreRequireTheOriginalDecryptionCertificates()
  {
    using var old = Certificate();
    using var next = Certificate();
    using var foreign = Certificate();
    var ring = Directory.CreateTempSubdirectory("minidocs-provisioning-ring-");
    var restored = Directory.CreateTempSubdirectory("minidocs-provisioning-restore-");
    try
    {
      var workspace = Guid.NewGuid();
      var account = Guid.NewGuid();
      byte[] before;
      string original;
      using (var initial = Open(ring, old))
      {
        initial.GetRequiredService<IKeyManager>().CreateNewKey(
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(90));
        var protection = Protect(initial);
        before = protection.CreateCredential(workspace, account, 1);
        original = protection.ReadCredential(workspace, account, 1, before);
      }
      Guid newId;
      using (var rotate = Open(ring, next, old))
        newId = rotate.GetRequiredService<IKeyManager>().CreateNewKey(
            DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddDays(90)).KeyId;
      Assert.True(File.Exists(Path.Combine(ring.FullName, $"key-{newId:D}.xml")));
      byte[] after;
      string newer;
      var secondAccount = Guid.NewGuid();
      using (var running = Open(ring, next, old))
      {
        var protection = Protect(running);
        Assert.Equal(original, protection.ReadCredential(workspace, account, 1, before));
        after = protection.CreateCredential(workspace, secondAccount, 1);
        Assert.Equal(newId, new Guid(after.AsSpan(8, 16)));
        newer = protection.ReadCredential(workspace, secondAccount, 1, after);
      }
      foreach (var file in ring.EnumerateFiles("key-*.xml")) file.CopyTo(Path.Combine(restored.FullName, file.Name));
      using var restore = Open(restored, next, old);
      Assert.Equal(original, Protect(restore).ReadCredential(workspace, account, 1, before));
      Assert.Equal(newer, Protect(restore).ReadCredential(workspace, secondAccount, 1, after));
      using var wrong = Open(restored, foreign);
      Assert.Throws<CryptographicException>(() => Protect(wrong).ReadCredential(workspace, account, 1, before));
      Assert.Throws<CryptographicException>(() => Protect(wrong).ReadCredential(workspace, secondAccount, 1, after));
    }
    finally
    {
      ring.Delete(recursive: true);
      restored.Delete(recursive: true);
    }
  }

  private static X509Certificate2 Certificate()
  {
    using var key = RSA.Create(2048);
    var request = new CertificateRequest("CN=MiniDocs local provisioning test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
  }

  private static ServiceProvider Open(DirectoryInfo directory, X509Certificate2 writer, params X509Certificate2[] older)
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddDataProtection().SetApplicationName("MiniDocs.Provisioning.Test").PersistKeysToFileSystem(directory)
        .ProtectKeysWithCertificate(writer).UnprotectKeysWithAnyCertificate([writer, .. older]);
    return services.BuildServiceProvider();
  }

  private static ProvisioningProtection Protect(IServiceProvider services) => new(services.GetRequiredService<IDataProtectionProvider>());
}
