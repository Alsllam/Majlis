using System.Text.Json;
using FakeItEasy;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Workspaces.Application.Workspaces.DTOs;
using Majlis.Workspaces.Domain.Constants;
using Majlis.Workspaces.Domain.Enums;
using Majlis.Workspaces.Domain.Events;
using Majlis.Workspaces.Tests.Application.TestInfrastructure;
using Microsoft.Extensions.Caching.Distributed;
using static Majlis.Workspaces.Tests.Application.TestInfrastructure.WorkspacesTestHost;

namespace Majlis.Workspaces.Tests.Application.AppServices;

public class WorkspacesAppServiceTests : IDisposable
{
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
        return await _host.Service(scope).CreateAsync(new CreateWorkspaceDto { Name = "الشؤون القانونية", Color = "brand" }, Ct);
    }

    private async Task<string[]> GrantsOfAsync(Guid userId)
    {
        var json = await _host.Cache.GetStringAsync(PermissionCacheKeys.ForUser(userId), Ct);
        return json is null ? [] : JsonSerializer.Deserialize<string[]>(json)!;
    }

    [Fact]
    public async Task CreateAsync_ShouldMakeCreatorOwnerAndCacheGrants_WhenValid()
    {
        var id = await CreateAsSaraAsync();

        using var scope = _host.Scope(Sara);
        var workspace = await _host.Service(scope).GetAsync(new WorkspaceIdDto(id), Ct);
        Assert.Equal(WorkspaceRole.Owner, workspace.MyRole);
        Assert.Single(workspace.Members);
        Assert.Contains(WorkspacesPermissions.ArchiveWorkspace, await GrantsOfAsync(Sara.Id));
        A.CallTo(() => _host.Publisher.PublishAsync(A<WorkspaceCreated>.That.Matches(e => e.WorkspaceId == id), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberAdded>.That.Matches(e => e.UserId == Sara.Id && e.Role == "Owner"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectInvalidInput_WhenNameIsEmptyOrColorUnknown()
    {
        using var scope = _host.Scope(Sara);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _host.Service(scope).CreateAsync(new CreateWorkspaceDto { Name = " " }, Ct));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _host.Service(scope).CreateAsync(new CreateWorkspaceDto { Name = "x", Color = "#ff0000" }, Ct));
    }

    [Fact]
    public async Task GetAsync_ShouldThrowNotFound_WhenWorkspaceDoesNotExist()
    {
        using var scope = _host.Scope(Sara);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _host.Service(scope).GetAsync(new WorkspaceIdDto(Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task GetAsync_ShouldThrowForbidden_WhenCallerIsNotMember()
    {
        var id = await CreateAsSaraAsync();

        using var scope = _host.Scope(Khalid);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _host.Service(scope).GetAsync(new WorkspaceIdDto(id), Ct));
        Assert.Equal(WorkspacesErrors.NotMember, ex.MessageKey);
    }

    [Fact]
    public async Task SetMemberAsync_ShouldAddContributorPublishEventAndCacheGrants_WhenCalledByOwner()
    {
        var id = await CreateAsSaraAsync();

        using (var scope = _host.Scope(Sara))
        {
            await _host.Service(scope).SetMemberAsync(new SetWorkspaceMemberDto { WorkspaceId = id, UserId = Khalid.Id, DisplayName = Khalid.Name, Role = WorkspaceRole.Contributor }, Ct);
        }

        using var read = _host.Scope(Khalid);
        var workspace = await _host.Service(read).GetAsync(new WorkspaceIdDto(id), Ct);
        Assert.Equal(WorkspaceRole.Contributor, workspace.MyRole);
        Assert.Equal(2, workspace.Members.Count);
        var grants = await GrantsOfAsync(Khalid.Id);
        Assert.Contains("Permissions.Rooms.CreateRoom", grants);
        Assert.DoesNotContain("Permissions.Rooms.TakeOverSession", grants);
        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberAdded>.That.Matches(e => e.UserId == Khalid.Id && e.WorkspaceId == id), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SetMemberAsync_ShouldPublishRoleChanged_WhenMemberAlreadyExists()
    {
        var id = await CreateAsSaraAsync();
        using var scope = _host.Scope(Sara);
        var service = _host.Service(scope);
        await service.SetMemberAsync(new SetWorkspaceMemberDto { WorkspaceId = id, UserId = Khalid.Id, DisplayName = Khalid.Name, Role = WorkspaceRole.Viewer }, Ct);

        await service.SetMemberAsync(new SetWorkspaceMemberDto { WorkspaceId = id, UserId = Khalid.Id, DisplayName = Khalid.Name, Role = WorkspaceRole.Admin }, Ct);

        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberRoleChanged>.That.Matches(e => e.UserId == Khalid.Id && e.Role == "Admin"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.Contains("Permissions.Rooms.TakeOverSession", await GrantsOfAsync(Khalid.Id));
    }

    [Fact]
    public async Task SetMemberAsync_ShouldThrowForbidden_WhenCalledByContributor()
    {
        var id = await CreateAsSaraAsync();
        using (var scope = _host.Scope(Sara))
        {
            await _host.Service(scope).SetMemberAsync(new SetWorkspaceMemberDto { WorkspaceId = id, UserId = Khalid.Id, DisplayName = Khalid.Name, Role = WorkspaceRole.Contributor }, Ct);
        }

        using var khalid = _host.Scope(Khalid);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _host.Service(khalid).SetMemberAsync(new SetWorkspaceMemberDto { WorkspaceId = id, UserId = Noura.Id, DisplayName = Noura.Name }, Ct));
        Assert.Equal(WorkspacesErrors.NotManager, ex.MessageKey);
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldRejectRemovingTheLastOwner_AndRevokeAccessOtherwise()
    {
        var id = await CreateAsSaraAsync();
        using var scope = _host.Scope(Sara);
        var service = _host.Service(scope);
        await service.SetMemberAsync(new SetWorkspaceMemberDto { WorkspaceId = id, UserId = Khalid.Id, DisplayName = Khalid.Name, Role = WorkspaceRole.Contributor }, Ct);

        var ex = await Assert.ThrowsAsync<CustomValidationException>(() => service.RemoveMemberAsync(new RemoveWorkspaceMemberDto { WorkspaceId = id, UserId = Sara.Id }, Ct));
        Assert.Equal(WorkspacesErrors.LastOwner, ex.MessageKey);

        await service.RemoveMemberAsync(new RemoveWorkspaceMemberDto { WorkspaceId = id, UserId = Khalid.Id }, Ct);
        Assert.Empty(await GrantsOfAsync(Khalid.Id));
        A.CallTo(() => _host.Publisher.PublishAsync(A<MemberRemoved>.That.Matches(e => e.UserId == Khalid.Id), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<AccessRevoked>.That.Matches(e => e.UserId == Khalid.Id && e.ScopeType == "workspace"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ArchiveAsync_ShouldMakeWorkspaceReadOnlyAndClearGrants_WhenCalledByOwner()
    {
        var id = await CreateAsSaraAsync();
        using var scope = _host.Scope(Sara);
        var service = _host.Service(scope);

        await service.ArchiveAsync(new WorkspaceIdDto(id), Ct);

        var ex = await Assert.ThrowsAsync<CustomValidationException>(() => service.UpdateAsync(new UpdateWorkspaceDto { Id = id, Name = "x" }, Ct));
        Assert.Equal(WorkspacesErrors.Archived, ex.MessageKey);
        Assert.Empty(await GrantsOfAsync(Sara.Id));
        var list = await service.GetListAsync(new FilterWorkspaceDto(), Ct);
        Assert.Equal(0, list.TotalCount);
        var all = await service.GetListAsync(new FilterWorkspaceDto { IncludeArchived = true }, Ct);
        Assert.Equal(1, all.TotalCount);
    }

    [Fact]
    public async Task GetListAsync_ShouldReturnOnlyCallersWorkspaces_WithRoleAndCount()
    {
        var id = await CreateAsSaraAsync();
        using (var scope = _host.Scope(Khalid))
        {
            await _host.Service(scope).CreateAsync(new CreateWorkspaceDto { Name = "العمليات" }, Ct);
        }

        using var sara = _host.Scope(Sara);
        var page = await _host.Service(sara).GetListAsync(new FilterWorkspaceDto(), Ct);
        var row = Assert.Single(page.Items);
        Assert.Equal(id, row.Id);
        Assert.Equal(WorkspaceRole.Owner, row.MyRole);
        Assert.Equal(1, row.MemberCount);
    }
}
