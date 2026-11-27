using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using VIA.MiniDocs.Api.Security;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Package", "D4-A")]
public sealed class KeyRingPathTests
{
  [Fact]
  public void RejectsAnUntrustedParentEvenWhenTheRingItselfIsPrivate()
  {
    using var keys = new SessionKeyRingFixture();
    var ring = new DirectoryInfo(keys.KeyDirectory);
    KeyRingPath.VerifyDirectory(ring);
    if (OperatingSystem.IsWindows())
    {
      var acl = keys.Root.GetAccessControl();
      acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
          FileSystemRights.DeleteSubdirectoriesAndFiles, AccessControlType.Allow));
      keys.Root.SetAccessControl(acl);
    }
    else
      File.SetUnixFileMode(keys.Root.FullName, File.GetUnixFileMode(keys.Root.FullName) | UnixFileMode.OtherWrite);
    Assert.Throws<InvalidOperationException>(() => KeyRingPath.VerifyDirectory(ring));
  }

  [Fact]
  public void RejectsAnIndirectLinkToAnOtherwiseValidRing()
  {
    using var keys = new SessionKeyRingFixture();
    var target = keys.Root.CreateSubdirectory("target");
    var realRing = target.CreateSubdirectory("ring");
    if (!OperatingSystem.IsWindows())
      File.SetUnixFileMode(realRing.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    KeyRingPath.VerifyDirectory(realRing);

    var link = Path.Combine(keys.Root.FullName, "link");
    if (OperatingSystem.IsWindows())
      CreateWindowsJunction(keys, target);
    else
      Directory.CreateSymbolicLink(link, target.FullName);
    try
    {
      var redirected = new DirectoryInfo(Path.Combine(link, realRing.Name));
      Assert.True(redirected.Exists);
      Assert.Throws<InvalidOperationException>(() => KeyRingPath.VerifyDirectory(redirected));
      Assert.Throws<InvalidOperationException>(() => KeyRingPath.VerifyDirectory(new DirectoryInfo(link)));
    }
    finally { Directory.Delete(link, recursive: false); }
  }

  [Fact]
  public void RejectsKeyOrCertificatePermissionsThatAllowAnotherIdentity()
  {
    using var keys = new SessionKeyRingFixture();
    var certificate = new FileInfo(keys.CertificatePath);
    KeyRingPath.VerifyKey(certificate);
    KeyRingPath.VerifyCertificate(certificate);
    if (OperatingSystem.IsWindows())
    {
      var acl = certificate.GetAccessControl();
      acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
          FileSystemRights.Read, AccessControlType.Allow));
      certificate.SetAccessControl(acl);
    }
    else
      File.SetUnixFileMode(certificate.FullName, File.GetUnixFileMode(certificate.FullName) | UnixFileMode.OtherRead);
    Assert.Throws<InvalidOperationException>(() => KeyRingPath.VerifyKey(certificate));
    Assert.Throws<InvalidOperationException>(() => KeyRingPath.VerifyCertificate(certificate));
  }

  [Fact]
  public void RejectsAnUnexpectedOwner()
  {
    using var keys = new SessionKeyRingFixture();
    if (OperatingSystem.IsWindows())
      VerifyWindowsOwnerIsRejected(keys.KeyDirectory);
    else if (Environment.UserName == "root")
    {
      var ring = new DirectoryInfo(keys.KeyDirectory);
      KeyRingPath.VerifyDirectory(ring);
      // Apply a different real UID while preserving the same owner-only mode.
      Run("chown", keys.Root.FullName, "65534", ring.Name);
      Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
          File.GetUnixFileMode(ring.FullName));
      Assert.Throws<InvalidOperationException>(() => KeyRingPath.VerifyDirectory(ring));
    }
    else
    {
      // Only the effective service UID may own the ring. /root is owned by
      // root and may be inspected without traversing it.
      Assert.Throws<InvalidOperationException>(() => KeyRingPath.VerifyDirectory(new DirectoryInfo("/root")));
    }
  }

  [SupportedOSPlatform("windows")]
  private static void VerifyWindowsOwnerIsRejected(string directory)
  {
    // Changing a real owner to a different SID needs Restore privilege.
    // Exercise the same OS descriptor verifier with the altered owner.
    using var identity = WindowsIdentity.GetCurrent();
    var acl = new DirectoryInfo(directory).GetAccessControl();
    acl.SetOwner(new SecurityIdentifier(WellKnownSidType.WorldSid, null));
    Assert.Throws<InvalidOperationException>(() => KeyRingPath.VerifyWindowsSecurity(acl, identity.User!, ancestor: false));
  }

  [SupportedOSPlatform("windows")]
  private static void CreateWindowsJunction(SessionKeyRingFixture keys, DirectoryInfo target) =>
      Run("cmd.exe", keys.Root.FullName, "/d", "/c", "mklink", "/J", "link", target.Name);

  private static void Run(string executable, string directory, params string[] arguments)
  {
    var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the filesystem test helper.");
    Assert.True(process.WaitForExit(5_000));
    Assert.Equal(0, process.ExitCode);
  }
}
