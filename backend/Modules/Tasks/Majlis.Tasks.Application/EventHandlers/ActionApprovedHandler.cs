using System.Text.Json;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Tasks.Application.Tasks.DTOs;
using Majlis.Tasks.Domain.Entities;
using Majlis.Tasks.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Tasks.Application.EventHandlers;

/// <summary>
/// Runs <c>create_task</c> after approval (FR-APR-007, FR-TSK-002): idempotent on the approval id, re-checks that the
/// requester is still a contributing member, then reports <c>ActionExecuted</c> (or <c>ActionExecutionFailed</c>).
/// Other tools are ignored here; their owning modules handle them.
/// </summary>
[WolverineHandler]
public static class ActionApprovedHandler
{
    public const string Tool = "create_task";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task Handle(
        ActionApproved message,
        IRepository<TaskItem, Guid> tasks,
        IReadOnlyRepository<WorkspaceMembership, Guid> memberships,
        IEventPublisher publisher,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (message.Tool != Tool)
        {
            return;
        }

        if (await tasks.Query().AnyAsync(t => t.ApprovalRequestId == message.RequestId, cancellationToken))
        {
            return; // already executed (duplicate delivery)
        }

        var requester = await memberships.Query()
            .FirstOrDefaultAsync(m => m.WorkspaceId == message.WorkspaceId && m.UserId == message.RequestedByUserId, cancellationToken);
        if (requester is null || !requester.CanContribute)
        {
            await publisher.PublishAsync(new ActionExecutionFailed(message.TenantId, message.RequestId, Tool, "Tasks:Task:NotWorkspaceMember", null), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        CreateTaskToolArgs args;
        try
        {
            args = JsonSerializer.Deserialize<CreateTaskToolArgs>(message.ArgsJson, Json) ?? new CreateTaskToolArgs();
        }
        catch (JsonException)
        {
            args = new CreateTaskToolArgs();
        }

        if (string.IsNullOrWhiteSpace(args.Title))
        {
            await publisher.PublishAsync(new ActionExecutionFailed(message.TenantId, message.RequestId, Tool, "General:Fields:Required", "title"), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var priority = Enum.TryParse<TaskPriority>(args.Priority, ignoreCase: true, out var p) ? p : TaskPriority.Normal;
        var task = new TaskItem(
            Guid.NewGuid(), message.TenantId, message.WorkspaceId, message.RoomId, message.SessionId,
            args.Title.Trim(), string.IsNullOrWhiteSpace(args.Description) ? null : args.Description.Trim(), priority, args.DueDate, TaskOrigin.Agent);
        task.LinkApproval(message.RequestId, message.TurnId);
        if (!string.IsNullOrWhiteSpace(args.AssigneeName))
        {
            var name = args.AssigneeName.Trim();
            var assignee = await memberships.Query()
                .FirstOrDefaultAsync(m => m.WorkspaceId == message.WorkspaceId && m.DisplayName == name, cancellationToken);
            if (assignee is not null)
            {
                task.Assign(assignee.UserId, assignee.DisplayName);
            }
        }

        await tasks.InsertAsync(task, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new ActionExecuted(message.TenantId, message.RequestId, Tool, task.Id, task.Title), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
