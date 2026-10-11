using System.Text.Json;
using Majlis.Framework.Domain.Events;
using Majlis.Notifications.Application.Dispatch;
using Majlis.Notifications.Domain.Constants;
using Wolverine.Attributes;

namespace Majlis.Notifications.Application.EventHandlers;

/// <summary>A task assigned to someone else than the person (or agent flow) assigning it.</summary>
[WolverineHandler]
public static class TaskAssignedHandler
{
    public static Task Handle(TaskAssigned m, NotificationDispatcher dispatcher, CancellationToken cancellationToken)
    {
        if (m.AssignedByUserId == m.AssigneeUserId)
        {
            return Task.CompletedTask;
        }

        return dispatcher.NotifyAsync(
            m.TenantId, [m.AssigneeUserId], NotificationTypes.TaskAssigned,
            new Dictionary<string, string> { ["name"] = m.AssignedByDisplayName ?? string.Empty, ["title"] = m.Title, ["taskId"] = m.TaskId.ToString() },
            $"/tasks?workspaceId={m.WorkspaceId}", m.TaskId.ToString(), cancellationToken);
    }
}

/// <summary>Session timeline events that concern one person: a hand-off offered to them (FR-NTF-001).</summary>
[WolverineHandler]
public static class SessionEventAppendedHandler
{
    public static async Task Handle(SessionEventAppended m, NotificationDispatcher dispatcher, CancellationToken cancellationToken)
    {
        if (m.Type != "handoff.offered")
        {
            return;
        }

        using var data = JsonDocument.Parse(m.DataJson);
        if (!data.RootElement.TryGetProperty("to", out var to) || !to.TryGetProperty("userId", out var userIdElement) || !userIdElement.TryGetGuid(out var userId))
        {
            return;
        }

        await dispatcher.NotifyAsync(
            m.TenantId, [userId], NotificationTypes.HandOffOffered,
            new Dictionary<string, string> { ["name"] = m.ActorDisplayName ?? string.Empty, ["sessionId"] = m.SessionId.ToString() },
            $"/rooms/{m.RoomId}", $"{m.SessionId:N}-{m.Seq}", cancellationToken);
    }
}
