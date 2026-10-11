using Majlis.Approvals.Domain.Entities;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Approvals.Application.EventHandlers;

/// <summary>The owning module ran the approved action (FR-APR-007); Rooms gets <c>ApprovalExecuted</c> for the card.</summary>
[WolverineHandler]
public static class ActionExecutedHandler
{
    public static async Task Handle(ActionExecuted message, IRepository<ApprovalRequest, Guid> requests, IEventPublisher publisher, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var request = await requests.QueryTracked().FirstOrDefaultAsync(r => r.Id == message.RequestId, cancellationToken);
        if (request is null || !request.MarkExecuted(message.EntityId, message.ResultSummary))
        {
            return;
        }

        await publisher.PublishAsync(new ApprovalExecuted(request.TenantId, request.SessionId, request.Id, request.Tool, true, message.ResultSummary, message.EntityId, null), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

[WolverineHandler]
public static class ActionExecutionFailedHandler
{
    public static async Task Handle(ActionExecutionFailed message, IRepository<ApprovalRequest, Guid> requests, IEventPublisher publisher, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var request = await requests.QueryTracked().FirstOrDefaultAsync(r => r.Id == message.RequestId, cancellationToken);
        if (request is null || !request.MarkFailed(message.ReasonKey, message.Detail))
        {
            return;
        }

        await publisher.PublishAsync(new ApprovalExecuted(request.TenantId, request.SessionId, request.Id, request.Tool, false, message.Detail, null, message.ReasonKey), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
