using Majlis.Framework.Application.Dtos;
using Majlis.Tasks.Domain.Enums;
using TaskStatus = Majlis.Tasks.Domain.Enums.TaskStatus;

namespace Majlis.Tasks.Application.Tasks.DTOs;

public sealed record TaskDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? RoomId,
    Guid? SessionId,
    string Title,
    string? Description,
    Guid? AssigneeUserId,
    string? AssigneeDisplayName,
    DateOnly? DueDate,
    TaskPriority Priority,
    TaskStatus Status,
    TaskOrigin Origin,
    Guid? ApprovalRequestId,
    DateTime CreationTime,
    Guid? CreatorId);

public sealed record TaskIdDto(Guid Id);

public sealed record FilterTaskDto : BaseFilterRequestDto
{
    public Guid? WorkspaceId { get; init; }
    public Guid? RoomId { get; init; }
    public TaskStatus? Status { get; init; }
    public bool AssignedToMe { get; init; }
}

public sealed record CreateTaskDto
{
    public Guid WorkspaceId { get; init; }
    public Guid? RoomId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? AssigneeUserId { get; init; }
    public DateOnly? DueDate { get; init; }
    public TaskPriority Priority { get; init; } = TaskPriority.Normal;
}

public sealed record UpdateTaskDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? AssigneeUserId { get; init; }
    public DateOnly? DueDate { get; init; }
    public TaskPriority Priority { get; init; }
}

public sealed record SetTaskStatusDto
{
    public Guid Id { get; init; }
    public TaskStatus Status { get; init; }
}

/// <summary>Arguments of the agent tool <c>create_task</c> (the JSON schema lives in ai-service; both must agree).</summary>
public sealed record CreateTaskToolArgs
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>Display name of a workspace member, as the agent sees it; resolved against the membership read model.</summary>
    public string? AssigneeName { get; init; }

    public DateOnly? DueDate { get; init; }
    public string? Priority { get; init; }
}
