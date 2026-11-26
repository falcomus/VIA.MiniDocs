using Microsoft.AspNetCore.Identity;
using VIA.MiniDocs.Domain;

namespace VIA.MiniDocs.Infrastructure;

public sealed class MiniDocsUser : IdentityUser<Guid>
{
  public bool IsDisabled { get; private set; }
  public AccountKind AccountKind { get; private set; }
}
