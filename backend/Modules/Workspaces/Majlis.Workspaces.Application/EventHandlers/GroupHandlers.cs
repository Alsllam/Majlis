using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Workspaces.Application.Security;
using Majlis.Workspaces.Domain.Entities;
using Majlis.Workspaces.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Workspaces.Application.EventHandlers;

internal static class GrantedWorkspaces
{
    /// <summary>Every workspace that granted the group a role, tracked, with members and grants.</summary>
    public static Task<List<Workspace>> LoadAsync(IRepository<Workspace, Guid> workspaces, Guid tenantId, Guid groupId, CancellationToken cancellationToken)
        => workspaces.QueryTracked().IgnoreQueryFilters().Include(w => w.Members).Include(w => w.Groups)
            .Where(w => w.TenantId == tenantId && !w.IsDeleted && w.Groups.Any(g => g.GroupId == groupId))
            .ToListAsync(cancellationToken);
}

/// <summary>A user joined a tenant group: remember it, and add them to every workspace that granted the group a role.</summary>
[WolverineHandler]
public static class GroupMemberAddedHandler
{
    public static async Task Handle(GroupMemberAdded m, IRepository<GroupMembership, Guid> memberships, IRepository<Workspace, Guid> workspaces, IEventPublisher publisher, IUnitOfWork unitOfWork, WorkspacePermissionCache cache, CancellationToken cancellationToken)
    {
        var existing = await memberships.QueryTracked().IgnoreQueryFilters().FirstOrDefaultAsync(g => g.GroupId == m.GroupId && g.UserId == m.UserId, cancellationToken);
        if (existing is null)
        {
            await memberships.InsertAsync(new GroupMembership(m.TenantId, m.GroupId, m.GroupName, m.UserId, m.DisplayName), autoSave: false, cancellationToken);
        }

        var touched = false;
        foreach (var workspace in await GrantedWorkspaces.LoadAsync(workspaces, m.TenantId, m.GroupId, cancellationToken))
        {
            foreach (var member in workspace.SyncGroupMembers(m.GroupId, [(m.UserId, m.DisplayName)]))
            {
                await publisher.PublishAsync(new MemberAdded(workspace.TenantId, workspace.Id, member.UserId, member.DisplayName, member.Role.ToString()), cancellationToken);
                touched = true;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (touched)
        {
            await cache.RefreshAsync(m.UserId, cancellationToken);
        }
    }
}

/// <summary>A user left a group: the memberships the group gave go away (direct memberships stay).</summary>
[WolverineHandler]
public static class GroupMemberRemovedHandler
{
    public static async Task Handle(GroupMemberRemoved m, IRepository<GroupMembership, Guid> memberships, IRepository<Workspace, Guid> workspaces, IEventPublisher publisher, IUnitOfWork unitOfWork, WorkspacePermissionCache cache, CancellationToken cancellationToken)
    {
        var rows = await memberships.QueryTracked().IgnoreQueryFilters().Where(g => g.GroupId == m.GroupId && g.UserId == m.UserId).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            await memberships.DeleteAsync(row, autoSave: false, cancellationToken);
        }

        var touched = false;
        foreach (var workspace in await GrantedWorkspaces.LoadAsync(workspaces, m.TenantId, m.GroupId, cancellationToken))
        {
            foreach (var userId in workspace.RemoveGroupMember(m.GroupId, m.UserId))
            {
                await publisher.PublishAsync(new MemberRemoved(workspace.TenantId, workspace.Id, userId), cancellationToken);
                await publisher.PublishAsync(new AccessRevoked(workspace.TenantId, userId, "workspace", workspace.Id), cancellationToken);
                touched = true;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (touched)
        {
            await cache.RefreshAsync(m.UserId, cancellationToken);
        }
    }
}

[WolverineHandler]
public static class GroupRenamedHandler
{
    public static async Task Handle(GroupRenamed m, IRepository<GroupMembership, Guid> memberships, IRepository<Workspace, Guid> workspaces, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        foreach (var row in await memberships.QueryTracked().IgnoreQueryFilters().Where(g => g.GroupId == m.GroupId).ToListAsync(cancellationToken))
        {
            row.Rename(m.GroupName);
        }

        foreach (var workspace in await GrantedWorkspaces.LoadAsync(workspaces, m.TenantId, m.GroupId, cancellationToken))
        {
            workspace.RenameGroup(m.GroupId, m.GroupName);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>The group is gone: every grant of it is withdrawn.</summary>
[WolverineHandler]
public static class GroupDeletedHandler
{
    public static async Task Handle(GroupDeleted m, IRepository<GroupMembership, Guid> memberships, IRepository<Workspace, Guid> workspaces, IEventPublisher publisher, IUnitOfWork unitOfWork, WorkspacePermissionCache cache, CancellationToken cancellationToken)
    {
        foreach (var row in await memberships.QueryTracked().IgnoreQueryFilters().Where(g => g.GroupId == m.GroupId).ToListAsync(cancellationToken))
        {
            await memberships.DeleteAsync(row, autoSave: false, cancellationToken);
        }

        var affected = new HashSet<Guid>();
        foreach (var workspace in await GrantedWorkspaces.LoadAsync(workspaces, m.TenantId, m.GroupId, cancellationToken))
        {
            foreach (var userId in workspace.RevokeGroup(null, m.GroupId))
            {
                await publisher.PublishAsync(new MemberRemoved(workspace.TenantId, workspace.Id, userId), cancellationToken);
                await publisher.PublishAsync(new AccessRevoked(workspace.TenantId, userId, "workspace", workspace.Id), cancellationToken);
                affected.Add(userId);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var userId in affected)
        {
            await cache.RefreshAsync(userId, cancellationToken);
        }
    }
}

/// <summary>
/// An invited person has an account: add them to the workspace they were invited to, acting as the inviter, who
/// must still manage that workspace (Identity cannot check that when the invitation is created).
/// </summary>
[WolverineHandler]
public static partial class UserInvitationAcceptedHandler
{
    public static async Task Handle(UserInvitationAccepted m, IRepository<Workspace, Guid> workspaces, IEventPublisher publisher, IUnitOfWork unitOfWork, WorkspacePermissionCache cache, Microsoft.Extensions.Logging.ILogger<Workspace> logger, CancellationToken cancellationToken)
    {
        if (m.WorkspaceId is not { } workspaceId || !Enum.TryParse<Domain.Enums.WorkspaceRole>(m.WorkspaceRole, ignoreCase: true, out var role))
        {
            return;
        }

        var workspace = await workspaces.QueryTracked().IgnoreQueryFilters().Include(w => w.Members).Include(w => w.Groups)
            .FirstOrDefaultAsync(w => w.Id == workspaceId && w.TenantId == m.TenantId && !w.IsDeleted, cancellationToken);
        if (workspace is null || workspace.IsArchived || workspace.FindMember(m.InvitedByUserId) is not { } inviter || !Domain.Constants.WorkspaceRolePermissions.CanManage(inviter.Role) || (role == Domain.Enums.WorkspaceRole.Owner && inviter.Role != Domain.Enums.WorkspaceRole.Owner))
        {
            Log.Skipped(logger, m.InvitationId, workspaceId, m.InvitedByUserId);
            return;
        }

        if (workspace.FindMember(m.UserId) is not null)
        {
            return; // already a member (a redelivered message, or added meanwhile): the invitation changes nothing
        }

        workspace.AddOrChangeMember(m.InvitedByUserId, m.UserId, m.DisplayName, role);
        await publisher.PublishAsync(new MemberAdded(workspace.TenantId, workspace.Id, m.UserId, m.DisplayName, role.ToString()), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RefreshAsync(m.UserId, cancellationToken);
    }

    private static partial class Log
    {
        [Microsoft.Extensions.Logging.LoggerMessage(EventId = 2101, Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Invitation {InvitationId} into workspace {WorkspaceId} skipped: inviter {InviterId} no longer manages it")]
        public static partial void Skipped(Microsoft.Extensions.Logging.ILogger logger, Guid invitationId, Guid workspaceId, Guid inviterId);
    }
}
