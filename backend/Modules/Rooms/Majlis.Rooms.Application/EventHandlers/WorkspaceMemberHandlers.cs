using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Rooms.Application.EventHandlers;

/// <summary>Keeps the <see cref="WorkspaceMembership"/> read model in step with Workspaces. Idempotent: upserts by (workspace, user).</summary>
[WolverineHandler]
public static class MemberAddedHandler
{
    public static Task Handle(MemberAdded message, IRepository<WorkspaceMembership, Guid> memberships, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
        => WorkspaceMembershipUpsert.UpsertAsync(memberships, unitOfWork, message.TenantId, message.WorkspaceId, message.UserId, message.DisplayName, message.Role, cancellationToken);
}

[WolverineHandler]
public static class MemberRoleChangedHandler
{
    public static Task Handle(MemberRoleChanged message, IRepository<WorkspaceMembership, Guid> memberships, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
        => WorkspaceMembershipUpsert.UpsertAsync(memberships, unitOfWork, message.TenantId, message.WorkspaceId, message.UserId, message.DisplayName, message.Role, cancellationToken);
}

[WolverineHandler]
public static class MemberRemovedHandler
{
    public static async Task Handle(MemberRemoved message, IRepository<WorkspaceMembership, Guid> memberships, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var existing = await memberships.QueryTracked()
            .FirstOrDefaultAsync(m => m.WorkspaceId == message.WorkspaceId && m.UserId == message.UserId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        await memberships.DeleteAsync(existing, autoSave: false, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal static class WorkspaceMembershipUpsert
{
    public static async Task UpsertAsync(
        IRepository<WorkspaceMembership, Guid> memberships,
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid workspaceId,
        Guid userId,
        string displayName,
        string role,
        CancellationToken cancellationToken)
    {
        var existing = await memberships.QueryTracked()
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken);
        if (existing is null)
        {
            await memberships.InsertAsync(new WorkspaceMembership(tenantId, workspaceId, userId, displayName, role), autoSave: false, cancellationToken);
        }
        else
        {
            existing.Update(displayName, role);
            await memberships.UpdateAsync(existing, autoSave: false, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
