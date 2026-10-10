using Majlis.Framework.Domain.Entities;
using Majlis.Tasks.Domain.Enums;
using TaskStatus = Majlis.Tasks.Domain.Enums.TaskStatus;

namespace Majlis.Tasks.Domain.Entities;

/// <summary>A task created by a person or by the agent after approval (FR-TSK-001/002).</summary>
public class TaskItem : FullAuditedEntity<Guid>, IMultiTenant
{
    protected TaskItem()
    {
    }

    public TaskItem(Guid id, Guid tenantId, Guid workspaceId, Guid? roomId, Guid? sessionId, string title, string? description, TaskPriority priority, DateOnly? dueDate, TaskOrigin origin)
        : base(id)
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        RoomId = roomId;
        SessionId = sessionId;
        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        Origin = origin;
        Status = TaskStatus.ToDo;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? TurnId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public string? AssigneeDisplayName { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public TaskPriority Priority { get; private set; }
    public TaskStatus Status { get; private set; }
    public TaskOrigin Origin { get; private set; }

    /// <summary>Set for agent-created tasks: the approval that allowed it (idempotency key).</summary>
    public Guid? ApprovalRequestId { get; private set; }

    public void LinkApproval(Guid approvalRequestId, Guid? turnId)
    {
        ApprovalRequestId = approvalRequestId;
        TurnId = turnId;
    }

    public void Update(string title, string? description, TaskPriority priority, DateOnly? dueDate)
    {
        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
    }

    public void Assign(Guid? userId, string? displayName)
    {
        AssigneeUserId = userId;
        AssigneeDisplayName = displayName;
    }

    public void SetStatus(TaskStatus status) => Status = status;
}
