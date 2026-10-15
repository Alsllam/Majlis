using FakeItEasy;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Workspaces.Application.EventHandlers;
using Majlis.Workspaces.Application.Security;
using Majlis.Workspaces.Application.Workspaces.DTOs;
using Majlis.Workspaces.Domain.Entities;
using Majlis.Workspaces.Domain.Enums;
using Majlis.Workspaces.Domain.Events;
using Majlis.Workspaces.Tests.Application.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Majlis.Workspaces.Tests.Application.TestInfrastructure.WorkspacesTestHost;

namespace Majlis.Workspaces.Tests.Application.AppServices;

/// <summary>FR-WSP-002 (groups) and FR-IDN-005 (invitations): group grants expand into memberships that follow the group.</summary>
public class GroupsAndInvitationsTests : IDisposable
{
    private static readonly Guid GroupId = Guid.NewGuid();
    private readonly WorkspacesTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Guid> CreateAsSaraAsync()
    {
        using var scope = _host.Scope(Sara);
        return await _host.Service(scope).CreateAsync(new CreateWorkspaceDto { Name = "العمليات" }, Ct);
    }

    private async Task GroupMemberJoinedAsync((Guid Id, string Name) user)
    {
        using var scope = _host.Scope(Sara);
        var sp = scope.ServiceProvider;
        await GroupMemberAddedHandler.Handle(new GroupMemberAdded(TenantId, GroupId, "الفريق القانوني", user.Id, user.Name), sp.GetRequiredService<IRepository<GroupMembership, Guid>>(), sp.GetRequiredService<IRepository<Workspace, Guid>>(), sp.GetRequiredService<IEventPublisher>(), sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<WorkspacePermissionCache>(), Ct);
    }

    private async Task<WorkspaceDto> GetAsync(Guid id, (Guid Id, string Name) as_)
    {
        using var scope = _host.Scope(as_);
        return await _host.Service(scope).GetAsync(new WorkspaceIdDto(id), Ct);
    }

    [Fact]
    public async Task GroupGrant_ShouldExpandIntoMemberships_ThatFollowTheGroup()
    {
        var id = await CreateAsSaraAsync();
        await GroupMemberJoinedAsync(Khalid); // in the group before the grant

        using (var scope = _host.Scope(Sara))
        {
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _host.Service(scope).SetGroupAsync(new SetWorkspaceGroupDto { WorkspaceId = id, GroupId = GroupId, GroupName = "الفريق القانوني", Role = WorkspaceRole.Owner }, Ct));
            await _host.Service(scope).SetGroupAsync(new SetWorkspaceGroupDto { WorkspaceId = id, GroupId = GroupId, GroupName = "الفريق القانوني", Role = WorkspaceRole.Viewer }, Ct);
        }

        var ws = await GetAsync(id, Khalid);
        Assert.Equal(WorkspaceRole.Viewer, ws.MyRole);
        var khalid = ws.Members.Single(m => m.UserId == Khalid.Id);
        Assert.Equal((GroupId, "الفريق القانوني"), (khalid.ViaGroupId, khalid.ViaGroupName));
        Assert.Equal((GroupId, WorkspaceRole.Viewer, 1), (ws.Groups.Single().GroupId, ws.Groups.Single().Role, ws.Groups.Single().MemberCount));
        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberAdded>.That.Matches(e => e.UserId == Khalid.Id && e.Role == "Viewer"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        // Noura joins the group later and gets in; the grant's role changes and both follow.
        await GroupMemberJoinedAsync(Noura);
        Assert.Equal(WorkspaceRole.Viewer, (await GetAsync(id, Noura)).MyRole);
        using (var scope = _host.Scope(Sara))
        {
            await _host.Service(scope).SetGroupAsync(new SetWorkspaceGroupDto { WorkspaceId = id, GroupId = GroupId, GroupName = "الفريق القانوني", Role = WorkspaceRole.Contributor }, Ct);
        }

        Assert.Equal(WorkspaceRole.Contributor, (await GetAsync(id, Noura)).MyRole);
        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberRoleChanged>.That.Matches(e => e.UserId == Khalid.Id && e.Role == "Contributor"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        // A direct membership wins over the group; leaving the group then changes nothing for Khalid, but Noura is out.
        using (var scope = _host.Scope(Sara))
        {
            await _host.Service(scope).SetMemberAsync(new SetWorkspaceMemberDto { WorkspaceId = id, UserId = Khalid.Id, DisplayName = Khalid.Name, Role = WorkspaceRole.Admin }, Ct);
        }

        using (var scope = _host.Scope(Sara))
        {
            var sp = scope.ServiceProvider;
            foreach (var user in new[] { Khalid, Noura })
            {
                await GroupMemberRemovedHandler.Handle(new GroupMemberRemoved(TenantId, GroupId, user.Id), sp.GetRequiredService<IRepository<GroupMembership, Guid>>(), sp.GetRequiredService<IRepository<Workspace, Guid>>(), sp.GetRequiredService<IEventPublisher>(), sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<WorkspacePermissionCache>(), Ct);
            }
        }

        ws = await GetAsync(id, Khalid);
        Assert.Equal(WorkspaceRole.Admin, ws.MyRole);
        Assert.Null(ws.Members.Single(m => m.UserId == Khalid.Id).ViaGroupId);
        Assert.DoesNotContain(ws.Members, m => m.UserId == Noura.Id);
        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberRemoved>.That.Matches(e => e.UserId == Noura.Id), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberRemoved>.That.Matches(e => e.UserId == Khalid.Id), A<CancellationToken>._)).MustNotHaveHappened();

        // Revoking the grant removes nobody now (Khalid is direct) and the group row is gone.
        using (var scope = _host.Scope(Sara))
        {
            await _host.Service(scope).RemoveGroupAsync(new RemoveWorkspaceGroupDto { WorkspaceId = id, GroupId = GroupId }, Ct);
        }

        Assert.Empty((await GetAsync(id, Sara)).Groups);
    }

    [Fact]
    public async Task GroupDeleted_ShouldWithdrawTheGrant_AndItsMembers()
    {
        var id = await CreateAsSaraAsync();
        using (var scope = _host.Scope(Sara))
        {
            await _host.Service(scope).SetGroupAsync(new SetWorkspaceGroupDto { WorkspaceId = id, GroupId = GroupId, GroupName = "فريق", Role = WorkspaceRole.Contributor }, Ct);
        }

        await GroupMemberJoinedAsync(Khalid);
        using (var scope = _host.Scope(Sara))
        {
            var sp = scope.ServiceProvider;
            await GroupDeletedHandler.Handle(new GroupDeleted(TenantId, GroupId), sp.GetRequiredService<IRepository<GroupMembership, Guid>>(), sp.GetRequiredService<IRepository<Workspace, Guid>>(), sp.GetRequiredService<IEventPublisher>(), sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<WorkspacePermissionCache>(), Ct);
        }

        var ws = await GetAsync(id, Sara);
        Assert.Empty(ws.Groups);
        Assert.Single(ws.Members);
        using var khalid = _host.Scope(Khalid);
        await Assert.ThrowsAsync<ForbiddenException>(() => _host.Service(khalid).GetAsync(new WorkspaceIdDto(id), Ct));
    }

    [Fact]
    public async Task InvitationAccepted_ShouldAddTheMember_OnlyWhenTheInviterStillManagesTheWorkspace()
    {
        var id = await CreateAsSaraAsync();
        using (var scope = _host.Scope(Sara))
        {
            await _host.Service(scope).SetMemberAsync(new SetWorkspaceMemberDto { WorkspaceId = id, UserId = Khalid.Id, DisplayName = Khalid.Name, Role = WorkspaceRole.Contributor }, Ct);
        }

        async Task AcceptAsync(Guid inviter, (Guid Id, string Name) user, string role)
        {
            using var scope = _host.Scope(Sara);
            var sp = scope.ServiceProvider;
            await UserInvitationAcceptedHandler.Handle(new UserInvitationAccepted(TenantId, Guid.NewGuid(), user.Id, user.Name, id, role, inviter), sp.GetRequiredService<IRepository<Workspace, Guid>>(), sp.GetRequiredService<IEventPublisher>(), sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<WorkspacePermissionCache>(), sp.GetRequiredService<ILogger<Workspace>>(), Ct);
        }

        var newUser = (Id: Guid.NewGuid(), Name: "ليلى");
        await AcceptAsync(Khalid.Id, newUser, "Viewer"); // a contributor cannot invite into the workspace
        Assert.DoesNotContain((await GetAsync(id, Sara)).Members, m => m.UserId == newUser.Id);

        await AcceptAsync(Sara.Id, newUser, "Viewer");
        await AcceptAsync(Sara.Id, newUser, "Admin"); // redelivered or a second invitation: the first membership stays
        var ws = await GetAsync(id, newUser);
        Assert.Equal(WorkspaceRole.Viewer, ws.MyRole);
        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberAdded>.That.Matches(e => e.UserId == newUser.Id && e.Role == "Viewer"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
