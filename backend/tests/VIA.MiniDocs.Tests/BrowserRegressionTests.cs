using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using VIA.MiniDocs.Application;
using Xunit;

namespace VIA.MiniDocs.Tests;

public sealed partial class SessionHostTests
{
  [Fact]
  public async Task BrowserRegression()
  {
    var frontend = Environment.GetEnvironmentVariable("MINIDOCS_E2E_FRONTEND")
        ?? throw new InvalidOperationException("Start browser regression through npm.");
    await using var db = await CreateDatabaseAsync();
    await using var runtime = fixture.CreateRuntimePeer(db);
    var password = Guid.NewGuid().ToString("N");
    await CreateUserAsync(db, "browser-admin", password, admin: true);
    var workspace = Guid.NewGuid();
    await CreateUserAsync(db, "browser-d6", password, activeWorkspace: workspace);
    await InsertMetadataDocumentAsync(db, workspace, "Persistentes Testdokument");
    using var keys = new SessionKeyRingFixture();
    using var host = CreateHost(runtime, keys).WithWebHostBuilder(builder =>
    {
      builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ViaDocs:Enabled"] = "true",
        ["ViaDocs:InvitationsEnabled"] = "true",
        ["ViaDocs:BaseAddress"] = "https://127.0.0.1:1/",
        ["Provisioning:Enabled"] = "true"
      }));
      builder.ConfigureServices(services =>
      {
        services.AddSingleton<IViaDocsAuthentication, BrowserViaDocs>();
        services.AddSingleton<IStartupFilter>(_ => new BrowserFiles(Path.Combine(frontend, "dist")));
      });
    });
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var san = new SubjectAlternativeNameBuilder();
    san.AddIpAddress(IPAddress.Loopback);
    request.CertificateExtensions.Add(san.Build());
    using var temporary = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
    using var cert = X509CertificateLoader.LoadPkcs12(temporary.Export(X509ContentType.Pkcs12), null,
        X509KeyStorageFlags.DefaultKeySet);
    host.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(cert)));
    var start = new ProcessStartInfo("node") { WorkingDirectory = frontend, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add("node_modules/@playwright/test/cli.js");
    start.ArgumentList.Add("test");
    if (Environment.GetEnvironmentVariable("MINIDOCS_E2E_FUNCTIONAL") == "1")
    {
      start.ArgumentList.Add("--project=functional");
    }
    start.Environment["MINIDOCS_E2E_ORIGIN"] = host.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    start.Environment["MINIDOCS_E2E_ADMIN_PASSWORD"] = password;
    start.Environment["MINIDOCS_E2E_CERT"] = cert.ExportCertificatePem();
    start.Environment["MINIDOCS_E2E_KEY"] = rsa.ExportPkcs8PrivateKeyPem();
    using var runner = Process.Start(start) ?? throw new InvalidOperationException("Playwright could not start.");
    try
    {
      var output = runner.StandardOutput.ReadToEndAsync();
      var errors = runner.StandardError.ReadToEndAsync();
      using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
      await runner.WaitForExitAsync(timeout.Token);
      var report = await output;
      Assert.True(runner.ExitCode == 0, $"{report}\n{await errors}");
      Console.WriteLine(report);
    }
    finally
    {
      if (!runner.HasExited)
      {
        runner.Kill(entireProcessTree: true);
        await runner.WaitForExitAsync();
      }
    }
  }

  private sealed class BrowserFiles(string root) : IStartupFilter
  {
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
      app.UseFileServer(new FileServerOptions { FileProvider = new PhysicalFileProvider(root) });
      next(app);
    };
  }

  private sealed class BrowserViaDocs : IViaDocsAuthentication
  {
    private readonly Guid application = Guid.NewGuid();

    public Task<ViaDocsResult<ViaDocsRegistrationCandidate>> RedeemAsync(ViaDocsPreparedIdentity identity,
        ViaDocsInvitation invitation, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return Task.FromResult<ViaDocsResult<ViaDocsRegistrationCandidate>>(
          new ViaDocsResult<ViaDocsRegistrationCandidate>.Success(new(application, identity.Name)));
    }

    public Task<ViaDocsResult<ViaDocsVerifiedIdentity>> FindAsync(ViaDocsPreparedIdentity identity,
        CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return Task.FromResult<ViaDocsResult<ViaDocsVerifiedIdentity>>(
          new ViaDocsResult<ViaDocsVerifiedIdentity>.Success(new(application, identity.Name)));
    }

    public Task<ViaDocsResult<ViaDocsVerifiedIdentity>> LoginAsync(ViaDocsBoundIdentity identity,
        CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected LOGIN in browser provisioning.");
  }
}
