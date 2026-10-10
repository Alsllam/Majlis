using Majlis.Framework.Domain.Exceptions;
using Majlis.Workspaces.Domain.Constants;
using Majlis.Workspaces.Domain.Entities;
using Majlis.Workspaces.Domain.Enums;

namespace Majlis.Workspaces.Tests.Domain;

public class WorkspaceTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();

    private static Workspace Build()
    {
        var workspace = new Workspace(Guid.NewGuid(), Guid.NewGuid(), "ws", null, null, null);
        workspace.AddFirstOwner(Owner, "owner");
        workspace.AddOrChangeMember(Owner, Admin, "admin", WorkspaceRole.Admin);
        return workspace;
    }

    [Fact]
    public void AddOrChangeMember_ShouldThrowForbidden_WhenAdminAssignsOwner()
    {
        var workspace = Build();
        var ex = Assert.Throws<ForbiddenException>(() => workspace.AddOrChangeMember(Admin, Member, "m", WorkspaceRole.Owner));
        Assert.Equal(WorkspacesErrors.OnlyOwnerChangesOwner, ex.MessageKey);
    }

    [Fact]
    public void AddOrChangeMember_ShouldThrowLastOwner_WhenDemotingTheOnlyOwner()
    {
        var workspace = Build();
        var ex = Assert.Throws<CustomValidationException>(() => workspace.AddOrChangeMember(Owner, Owner, "owner", WorkspaceRole.Admin));
        Assert.Equal(WorkspacesErrors.LastOwner, ex.MessageKey);
    }

    [Fact]
    public void AddOrChangeMember_ShouldAllowDemotion_WhenAnotherOwnerExists()
    {
        var workspace = Build();
        workspace.AddOrChangeMember(Owner, Admin, "admin", WorkspaceRole.Owner);
        workspace.AddOrChangeMember(Owner, Owner, "owner", WorkspaceRole.Contributor);
        Assert.Equal(WorkspaceRole.Contributor, workspace.FindMember(Owner)!.Role);
    }

    [Theory]
    [InlineData(WorkspaceRole.Owner, true)]
    [InlineData(WorkspaceRole.Admin, true)]
    [InlineData(WorkspaceRole.Contributor, false)]
    [InlineData(WorkspaceRole.Viewer, false)]
    public void RolePermissions_ShouldGrantTakeOver_OnlyToManagers(WorkspaceRole role, bool expected)
    {
        Assert.Equal(expected, WorkspaceRolePermissions.For(role).Contains("Permissions.Rooms.TakeOverSession"));
        Assert.Contains("Permissions.Rooms.ViewRoom", WorkspaceRolePermissions.For(role));
    }
}
