using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace VIA.MiniDocs.Api.Security;

internal static class KeyRingPath
{
  internal static void VerifyDirectory(DirectoryInfo directory) => Verify(directory, certificate: false);
  internal static void VerifyKey(FileInfo file) => Verify(file, certificate: false);
  internal static void VerifyCertificate(FileInfo file) => Verify(file, certificate: true);
  internal static void VerifyTemporaryDirectory(DirectoryInfo directory) =>
      Verify(directory, certificate: false, isTemporaryDirectory: true);

  private static void Verify(FileSystemInfo target, bool certificate, bool isTemporaryDirectory = false)
  {
    // Establish trust from the root downward. Each validated parent prevents an
    // untrusted identity from replacing the next component after inspection.
    var ancestors = new Stack<DirectoryInfo>();
    for (var parent = target is DirectoryInfo directory ? directory.Parent : ((FileInfo)target).Directory;
        parent is not null; parent = parent.Parent)
      ancestors.Push(parent);

    try
    {
      if (OperatingSystem.IsLinux())
      {
        var uid = GetEffectiveUserId();
        foreach (var parent in ancestors) VerifyLinux(parent, uid, ancestor: true, certificate: false);
        VerifyLinux(target, uid, ancestor: isTemporaryDirectory, certificate);
      }
      else if (OperatingSystem.IsWindows())
      {
        var root = Path.GetPathRoot(target.FullName)!;
        if (root.Length != 3 || new DriveInfo(root).DriveType != DriveType.Fixed) throw UnsafePath();
        using var identity = WindowsIdentity.GetCurrent();
        var principal = identity.User ?? throw UnsafePath();
        foreach (var parent in ancestors) VerifyWindows(parent, principal, ancestor: true);
        VerifyWindows(target, principal, ancestor: false);
      }
      else throw new PlatformNotSupportedException("MiniDocs protected key rings require Linux or Windows.");
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
    {
      throw UnsafePath();
    }
  }

  private static void VerifyLinux(FileSystemInfo entry, uint uid, bool ancestor, bool certificate)
  {
    const uint required = 0x0f; // STATX_TYPE | STATX_MODE | STATX_NLINK | STATX_UID
    const int atCurrentWorkingDirectory = -100;
    const int noFollow = 0x100;
    const ushort typeMask = 0xf000, directory = 0x4000, regularFile = 0x8000;
    if (Statx(atCurrentWorkingDirectory, entry.FullName, noFollow, required, out var status) != 0 ||
        (status.Mask & required) != required ||
        (status.Mode & typeMask) != (entry is DirectoryInfo ? directory : regularFile))
      throw UnsafePath();

    var mode = (UnixFileMode)status.Mode;
    const UnixFileMode sharedWrite = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
    const UnixFileMode sharedAccess = sharedWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
        UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
    if (ancestor)
    {
      // A root-owned sticky directory (such as /tmp) still protects an existing
      // root- or service-owned child against removal by other identities.
      var stickyRoot = status.UserId == 0 && mode.HasFlag(UnixFileMode.StickyBit);
      if ((status.UserId != uid && status.UserId != 0) || ((mode & sharedWrite) != 0 && !stickyRoot))
        throw UnsafePath();
    }
    else if ((status.UserId != uid && !(certificate && status.UserId == 0)) ||
        (mode & sharedAccess) != 0 || (entry is FileInfo && status.LinkCount != 1))
      throw UnsafePath();
  }

  [SupportedOSPlatform("windows")]
  private static void VerifyWindows(FileSystemInfo entry, SecurityIdentifier principal, bool ancestor)
  {
    var attributes = File.GetAttributes(entry.FullName);
    if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
        attributes.HasFlag(FileAttributes.Directory) != (entry is DirectoryInfo))
      throw UnsafePath();

    var security = entry is DirectoryInfo directory
        ? (FileSystemSecurity)directory.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access)
        : ((FileInfo)entry).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
    VerifyWindowsSecurity(security, principal, ancestor);
  }

  [SupportedOSPlatform("windows")]
  internal static void VerifyWindowsSecurity(FileSystemSecurity security, SecurityIdentifier principal, bool ancestor)
  {
    var descriptor = new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0);
    if (descriptor.Owner is not { } owner || !IsTrustedWindowsIdentity(owner, principal) ||
        !descriptor.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclPresent) ||
        descriptor.DiscretionaryAcl is not { } acl)
      throw UnsafePath();

    const uint genericWriteOrAll = 0x50000000;
    const FileSystemRights replacementRights = FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership |
        FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes;
    foreach (GenericAce rule in acl)
    {
      if (rule.AceFlags.HasFlag(AceFlags.InheritOnly)) continue;
      if (rule is not QualifiedAce ace) throw UnsafePath();
      if (ace.AceQualifier == AceQualifier.AccessAllowed && !IsTrustedWindowsIdentity(ace.SecurityIdentifier, principal) &&
          (!ancestor || ((uint)ace.AccessMask & (genericWriteOrAll | (uint)replacementRights)) != 0))
        throw UnsafePath();
    }
  }

  [SupportedOSPlatform("windows")]
  private static bool IsTrustedWindowsIdentity(SecurityIdentifier sid, SecurityIdentifier principal) =>
      sid.Equals(principal) || sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
      sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
      sid.Value == "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"; // Windows TrustedInstaller

  private static InvalidOperationException UnsafePath() =>
      new("The MiniDocs key ring path has an unsafe owner, permissions or file type.");

  [DllImport("libc", EntryPoint = "geteuid")]
  private static extern uint GetEffectiveUserId();

  // statx uses the fixed Linux UAPI layout on both 32- and 64-bit Linux.
  [StructLayout(LayoutKind.Explicit, Size = 256)]
  private struct LinuxFileStatus
  {
    [FieldOffset(0)] public uint Mask;
    [FieldOffset(16)] public uint LinkCount;
    [FieldOffset(20)] public uint UserId;
    [FieldOffset(28)] public ushort Mode;
  }

  [DllImport("libc", EntryPoint = "statx")]
  private static extern int Statx(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
      int flags, uint mask, out LinuxFileStatus status);
}
