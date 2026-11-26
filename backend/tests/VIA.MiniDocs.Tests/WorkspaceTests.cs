using VIA.MiniDocs.Domain;
using Xunit;

namespace VIA.MiniDocs.Tests;

public class WorkspaceTests
{
  [Fact]
  public void NewWorkspaceStartsPendingWithVersionOne()
  {
    var owner = Guid.NewGuid();
    var workspace = new Workspace(Guid.NewGuid(), owner);
    Assert.Equal(owner, workspace.OwnerUserId);
    Assert.Equal(WorkspaceStatus.Pending, workspace.Status);
    Assert.Equal(1, workspace.Version);
  }

  [Fact]
  public void EmptyIdentifiersAreRejected()
  {
    Assert.Throws<ArgumentException>(() => new Workspace(Guid.Empty, Guid.NewGuid()));
    Assert.Throws<ArgumentException>(() => new Workspace(Guid.NewGuid(), Guid.Empty));
  }
}
