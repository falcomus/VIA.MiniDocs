using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace VIA.MiniDocs.Api.Security;

internal static class ProtectedKeyRing
{
  internal static void Configure(IServiceCollection services)
  {
    services.AddDataProtection().DisableAutomaticKeyGeneration();
    services.AddSingleton<ConfiguredKeyRing>();
    services.AddSingleton<IDataProtectionProvider>(provider =>
        provider.GetRequiredService<ConfiguredKeyRing>().Provider);
    services.AddSingleton<IKeyManager>(provider =>
        provider.GetRequiredService<ConfiguredKeyRing>().Manager);
    services.AddHostedService<KeyRingStartup>();
  }
}

internal sealed class ConfiguredKeyRing : IDisposable
{
  private readonly ServiceProvider protectorServices;
  private readonly X509Certificate2[] certificates;
  internal IDataProtectionProvider Provider => protectorServices.GetRequiredService<IDataProtectionProvider>();
  internal IKeyManager Manager => protectorServices.GetRequiredService<IKeyManager>();

  public ConfiguredKeyRing(IConfiguration configuration, IHostEnvironment environment, ILoggerFactory logger)
  {
    var config = configuration.GetSection("Security:KeyRing");
    var root = config["Directory"];
    var applicationName = config["ApplicationName"];
    if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) ||
        string.IsNullOrWhiteSpace(applicationName))
      throw new InvalidOperationException("The MiniDocs key ring directory and application name are required.");

    var directory = new DirectoryInfo(root);
    var relativeToRelease = Path.GetRelativePath(AppContext.BaseDirectory, directory.FullName);
    if (relativeToRelease == "." || (!Path.IsPathRooted(relativeToRelease) &&
        relativeToRelease != ".." && !relativeToRelease.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
      throw new InvalidOperationException("The MiniDocs key ring must be stored outside the release directory.");
    KeyRingPath.VerifyDirectory(directory);
    AllowInitialCreation = config.GetValue<bool>("AllowInitialCreation");
    if (!directory.EnumerateFiles("key-*.xml").Any() && !AllowInitialCreation)
      throw new InvalidOperationException("The MiniDocs key ring is empty. Initial creation must be explicitly authorized.");

    certificates = LoadCertificates(config);
    var isolatedServices = new ServiceCollection();
    isolatedServices.AddSingleton(logger);
    isolatedServices.AddDataProtection()
        .SetApplicationName($"VIA.MiniDocs/{environment.EnvironmentName}/{applicationName}")
        .ProtectKeysWithCertificate(certificates[0])
        .UnprotectKeysWithAnyCertificate(certificates);
    isolatedServices.Configure<KeyManagementOptions>(options =>
        options.XmlRepository = new ProtectedKeyRingRepository(directory, logger));
    protectorServices = isolatedServices.BuildServiceProvider();
  }

  internal bool AllowInitialCreation { get; }

  public void Dispose()
  {
    protectorServices.Dispose();
    foreach (var certificate in certificates) certificate.Dispose();
  }

  private static X509Certificate2[] LoadCertificates(IConfigurationSection config)
  {
    var certificates = new List<X509Certificate2>();
    try
    {
      certificates.Add(LoadCertificate(config["CertificateFile"], config["CertificatePassword"], active: true));
      foreach (var section in config.GetSection("PreviousCertificates").GetChildren())
        certificates.Add(LoadCertificate(section["File"], section["Password"], active: false));
      return certificates.ToArray();
    }
    catch
    {
      foreach (var certificate in certificates) certificate.Dispose();
      throw;
    }
  }

  private static X509Certificate2 LoadCertificate(string? path, string? password, bool active)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        throw new InvalidOperationException();
      var file = new FileInfo(path);
      KeyRingPath.VerifyCertificate(file);
      var certificate = X509CertificateLoader.LoadPkcs12FromFile(file.FullName, password, X509KeyStorageFlags.EphemeralKeySet);
      try
      {
        using var key = certificate.GetRSAPrivateKey();
        var now = DateTime.UtcNow;
        if (key is null || key.KeySize < 2048 ||
            (active && (now < certificate.NotBefore.ToUniversalTime() || now >= certificate.NotAfter.ToUniversalTime())))
          throw new InvalidOperationException();
        return certificate;
      }
      catch
      {
        certificate.Dispose();
        throw;
      }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
        System.Security.Cryptography.CryptographicException or InvalidOperationException)
    {
      throw new InvalidOperationException("A MiniDocs key ring certificate cannot be used.");
    }
  }
}

internal sealed class ProtectedKeyRingRepository(DirectoryInfo directory, ILoggerFactory logger) : IXmlRepository
{
  private static readonly XNamespace Protection = "http://schemas.asp.net/2015/03/dataProtection";
  private static readonly XNamespace Encryption = "http://www.w3.org/2001/04/xmlenc#";
  private static readonly XNamespace Signature = "http://www.w3.org/2000/09/xmldsig#";
  private readonly FileSystemXmlRepository writer = new(directory, logger);

  public IReadOnlyCollection<XElement> GetAllElements()
  {
    KeyRingPath.VerifyDirectory(directory);
    var elements = new List<XElement>();
    foreach (var entry in directory.EnumerateFileSystemInfos("*.xml"))
    {
      if (entry is not FileInfo file) throw InvalidDocument();
      KeyRingPath.VerifyKey(file);
      using var xml = XmlReader.Create(file.FullName, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
      var element = XElement.Load(xml);
      if (file.Name.StartsWith("key-", StringComparison.Ordinal))
        VerifyKeyElement(element, file.Name);
      else if (!file.Name.StartsWith("revocation-", StringComparison.Ordinal) || element.Name != "revocation")
        throw InvalidDocument();
      elements.Add(element);
    }
    return elements;
  }

  public void StoreElement(XElement element, string friendlyName)
  {
    KeyRingPath.VerifyDirectory(directory);
    // The ASP.NET repository stages its owner-only Unix file in the OS temp directory.
    if (OperatingSystem.IsLinux()) KeyRingPath.VerifyTemporaryDirectory(new DirectoryInfo(Path.GetTempPath()));
    writer.StoreElement(element, friendlyName);
    KeyRingPath.VerifyKey(new FileInfo(Path.Combine(directory.FullName, friendlyName + ".xml")));
  }

  private static void VerifyKeyElement(XElement element, string name)
  {
    if (element.Name != "key" || !Guid.TryParse((string?)element.Attribute("id"), out var id) ||
        name != $"key-{id:D}.xml")
      throw InvalidDocument();

    foreach (var date in new[] { "creationDate", "activationDate", "expirationDate" })
      _ = XmlConvert.ToDateTimeOffset((string?)element.Element(date) ?? throw InvalidDocument());

    var descriptor = element.Element("descriptor");
    var encrypted = descriptor?.Descendants(Protection + "encryptedSecret").SingleOrDefault();
    var decryptor = (string?)encrypted?.Attribute("decryptorType");
    if (decryptor?.Split(',', 2)[0] != typeof(EncryptedXmlDecryptor).FullName ||
        encrypted?.Element(Encryption + "EncryptedData")?.Descendants(Encryption + "EncryptedKey")
            .SingleOrDefault()?.Descendants(Signature + "X509Data").SingleOrDefault() is null ||
        descriptor!.Descendants("masterKey").Any())
      throw InvalidDocument();
  }

  private static InvalidOperationException InvalidDocument() =>
      new("The MiniDocs key ring contains an invalid or unprotected XML document.");
}

internal sealed class KeyRingStartup(ConfiguredKeyRing ring) : IHostedService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    try
    {
      var keys = ring.Manager.GetAllKeys();
      if (keys.Count == 0 && !ring.AllowInitialCreation) throw new InvalidOperationException();
      foreach (var key in keys.Where(key => !key.IsRevoked))
        if (key.CreateEncryptor() is null) throw new InvalidOperationException();
      var protector = ring.Provider.CreateProtector("VIA.MiniDocs.Startup.v1");
      if (protector.Unprotect(protector.Protect("ready")) != "ready") throw new InvalidOperationException();
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      throw new InvalidOperationException("The MiniDocs protected key ring could not be initialized.");
    }
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
