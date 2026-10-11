using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Notifications.Application.Dispatch;
using Majlis.Notifications.Domain.Constants;
using Majlis.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Notifications.Application.EventHandlers;

/// <summary>A new approval request: tell the people who may decide it (FR-NTF-001), never the requester.</summary>
[WolverineHandler]
public static class ApprovalRequestedHandler
{
    public static async Task Handle(
        ApprovalRequested m,
        IRepository<ApprovalRef, Guid> refs,
        IReadOnlyRepository<WorkspaceMembership, Guid> memberships,
        IUnitOfWork unitOfWork,
        NotificationDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!await refs.Query().AnyAsync(r => r.Id == m.RequestId, cancellationToken))
        {
            await refs.InsertAsync(new ApprovalRef(m.RequestId, m.TenantId, m.WorkspaceId, m.RoomId, m.RequestedByUserId, m.Tool, m.Summary), autoSave: false, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var members = await memberships.Query().Where(x => x.WorkspaceId == m.WorkspaceId && x.UserId != m.RequestedByUserId).ToListAsync(cancellationToken);
        var approvers = members.Where(x => m.Risk == "Low" ? x.CanContribute : x.CanManage).Select(x => x.UserId).ToList();
        await dispatcher.NotifyAsync(
            m.TenantId, approvers, NotificationTypes.ApprovalRequested,
            new Dictionary<string, string> { ["name"] = m.RequestedByDisplayName, ["summary"] = m.Summary, ["tool"] = m.Tool, ["requestId"] = m.RequestId.ToString() },
            $"/rooms/{m.RoomId}", m.RequestId.ToString(), cancellationToken);
    }
}

/// <summary>The requester learns the decision; an expiry is not a decision by a person and is not notified.</summary>
[WolverineHandler]
public static class ApprovalDecidedHandler
{
    public static async Task Handle(ApprovalDecided m, IReadOnlyRepository<ApprovalRef, Guid> refs, NotificationDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var r = await refs.Query().FirstOrDefaultAsync(x => x.Id == m.RequestId, cancellationToken);
        if (r is null || m.DecidedByUserId is not { } decidedBy || m.Decision == "Expired" || decidedBy == r.RequesterUserId)
        {
            return;
        }

        await dispatcher.NotifyAsync(
            m.TenantId, [r.RequesterUserId], NotificationTypes.ApprovalDecided,
            new Dictionary<string, string> { ["name"] = m.DecidedByDisplayName ?? string.Empty, ["summary"] = r.Summary, ["decision"] = m.Decision, ["note"] = m.Note ?? string.Empty, ["requestId"] = m.RequestId.ToString() },
            $"/rooms/{r.RoomId}", m.RequestId.ToString(), cancellationToken);
    }
}

/// <summary>The requester learns whether the approved action ran.</summary>
[WolverineHandler]
public static class ApprovalExecutedHandler
{
    public static async Task Handle(ApprovalExecuted m, IReadOnlyRepository<ApprovalRef, Guid> refs, NotificationDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var r = await refs.Query().FirstOrDefaultAsync(x => x.Id == m.RequestId, cancellationToken);
        if (r is null)
        {
            return;
        }

        await dispatcher.NotifyAsync(
            m.TenantId, [r.RequesterUserId], NotificationTypes.ApprovalExecuted,
            new Dictionary<string, string> { ["summary"] = m.ResultSummary ?? r.Summary, ["succeeded"] = m.Succeeded ? "true" : "false", ["reasonKey"] = m.ReasonKey ?? string.Empty, ["requestId"] = m.RequestId.ToString() },
            $"/rooms/{r.RoomId}", m.RequestId.ToString(), cancellationToken);
    }
}
