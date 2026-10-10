using Majlis.Tasks.Application.Tasks.DTOs;
using Majlis.Tasks.Domain.Entities;

namespace Majlis.Tasks.Application;

/// <summary>Explicit entity → DTO mapping (no AutoMapper; ADR-0008).</summary>
public static class TasksMappings
{
    public static TaskDto ToDto(this TaskItem t) => new(
        t.Id, t.WorkspaceId, t.RoomId, t.SessionId, t.Title, t.Description, t.AssigneeUserId, t.AssigneeDisplayName, t.DueDate,
        t.Priority, t.Status, t.Origin, t.ApprovalRequestId, t.CreationTime, t.CreatorId);
}
