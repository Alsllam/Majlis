using System.Text.Json;
using Majlis.Audit.Application.Recording;
using Majlis.Audit.Domain.Constants;
using Majlis.Audit.Domain.Events;
using Majlis.Framework.Domain.Events;
using Wolverine.Attributes;

namespace Majlis.Audit.Application.EventHandlers;

/// <summary>Session control changes (invariant 7). Turns are not recorded here: their text is content, not audit metadata.</summary>
[WolverineHandler]
public static class SessionEventAppendedHandler
{
    private static readonly HashSet<string> Recorded = ["session.started", "session.ended", "control.changed", "control.requested", "control.request.resolved", "handoff.offered", "handoff.resolved"];

    public static async Task Handle(SessionEventAppended m, AuditRecorder recorder, CancellationToken cancellationToken)
    {
        if (!Recorded.Contains(m.Type))
        {
            return;
        }

        using var data = JsonDocument.Parse(m.DataJson);
        var root = data.RootElement;
        var details = new Dictionary<string, object?> { ["seq"] = m.Seq };
        foreach (var name in new[] { "kind", "epoch", "accepted", "reason" })
        {
            if (root.TryGetProperty(name, out var value))
            {
                details[name] = value.ValueKind switch { JsonValueKind.Number => value.GetDouble(), JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null, _ => value.ToString() };
            }
        }

        foreach (var name in new[] { "from", "to" })
        {
            if (root.TryGetProperty(name, out var actor) && actor.ValueKind == JsonValueKind.Object)
            {
                details[name] = actor.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
            }
        }

        await recorder.RecordAsync(
            new AuditRecord(
                m.TenantId, m.At, AuditCategories.Session, m.Type, m.ActorKind, m.ActorId, m.ActorDisplayName,
                $"session:{m.SessionId:N}:{m.Seq}", m.WorkspaceId, m.RoomId, m.SessionId, "session", m.SessionId, details),
            cancellationToken);
    }
}

[WolverineHandler]
public static class ApprovalRequestedHandler
{
    public static Task Handle(ApprovalRequested m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(
                m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Approval, "approval.requested", "agent", m.RequestedByUserId, m.RequestedByDisplayName,
                $"approval:{m.RequestId:N}:requested", m.WorkspaceId, m.RoomId, m.SessionId, "approval", m.RequestId,
                new { tool = m.Tool, risk = m.Risk, summary = m.Summary, expiresAt = m.ExpiresAt }),
            cancellationToken);
}

[WolverineHandler]
public static class ApprovalDecidedHandler
{
    public static Task Handle(ApprovalDecided m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(
                m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Approval, $"approval.{m.Decision.ToLowerInvariant()}",
                m.DecidedByUserId is null ? "system" : "user", m.DecidedByUserId, m.DecidedByDisplayName,
                $"approval:{m.RequestId:N}:{m.Decision.ToLowerInvariant()}", null, null, m.SessionId, "approval", m.RequestId,
                new { tool = m.Tool, decision = m.Decision, note = m.Note, edited = m.EditedArgsJson is not null }),
            cancellationToken);
}

/// <summary>Tool executions reported by the owning modules through Approvals.</summary>
[WolverineHandler]
public static class ApprovalExecutedHandler
{
    public static Task Handle(ApprovalExecuted m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(
                m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Tool, m.Succeeded ? "tool.executed" : "tool.failed", "system", null, null,
                $"approval:{m.RequestId:N}:executed", null, null, m.SessionId, m.Tool, m.EntityId,
                new { tool = m.Tool, approvalRequestId = m.RequestId, resultSummary = m.ResultSummary, reasonKey = m.ReasonKey }),
            cancellationToken);
}

[WolverineHandler]
public static class DocumentUploadedHandler
{
    public static Task Handle(DocumentUploaded m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(
                m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Document, "document.uploaded", "user", null, null,
                $"document:{m.VersionId:N}:uploaded", m.WorkspaceId, m.RoomId, null, "document", m.DocumentId,
                new { title = m.Title, versionId = m.VersionId, fileName = m.FileName, contentType = m.ContentType, type = m.DocType }),
            cancellationToken);
}

[WolverineHandler]
public static class DocumentDeletedHandler
{
    public static Task Handle(DocumentDeleted m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Document, "document.deleted", "user", null, null, $"document:{m.DocumentId:N}:deleted", null, null, null, "document", m.DocumentId),
            cancellationToken);
}

/// <summary>Document access (downloads and in-page reads).</summary>
[WolverineHandler]
public static class DocumentAccessedHandler
{
    public static Task Handle(DocumentAccessed m, AuditRecorder recorder, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(
                m.TenantId, m.At, AuditCategories.Document, $"document.{m.Kind}", "user", m.UserId, m.DisplayName,
                $"document:{m.VersionId:N}:{m.Kind}:{m.UserId:N}:{m.At.Ticks}", m.WorkspaceId, null, null, "document", m.DocumentId, new { versionId = m.VersionId }),
            cancellationToken);
}

[WolverineHandler]
public static class MemberAddedHandler
{
    public static Task Handle(MemberAdded m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Membership, "member.added", "user", null, null, $"member:{m.WorkspaceId:N}:{m.UserId:N}:added:{m.Role}", m.WorkspaceId, null, null, "user", m.UserId, new { displayName = m.DisplayName, role = m.Role }),
            cancellationToken);
}

[WolverineHandler]
public static class MemberRoleChangedHandler
{
    public static Task Handle(MemberRoleChanged m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Membership, "member.role-changed", "user", null, null, $"member:{m.WorkspaceId:N}:{m.UserId:N}:role:{m.Role}:{clock.GetUtcNow().Ticks / TimeSpan.TicksPerSecond}", m.WorkspaceId, null, null, "user", m.UserId, new { displayName = m.DisplayName, role = m.Role }),
            cancellationToken);
}

[WolverineHandler]
public static class MemberRemovedHandler
{
    public static Task Handle(MemberRemoved m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Membership, "member.removed", "user", null, null, $"member:{m.WorkspaceId:N}:{m.UserId:N}:removed:{clock.GetUtcNow().Ticks / TimeSpan.TicksPerSecond}", m.WorkspaceId, null, null, "user", m.UserId),
            cancellationToken);
}

[WolverineHandler]
public static class TaskAssignedHandler
{
    public static Task Handle(TaskAssigned m, AuditRecorder recorder, TimeProvider clock, CancellationToken cancellationToken)
        => recorder.RecordAsync(
            new AuditRecord(m.TenantId, clock.GetUtcNow().UtcDateTime, AuditCategories.Task, "task.assigned", "user", m.AssignedByUserId, m.AssignedByDisplayName, $"task:{m.TaskId:N}:assigned:{m.AssigneeUserId:N}", m.WorkspaceId, m.RoomId, null, "task", m.TaskId, new { title = m.Title, assigneeUserId = m.AssigneeUserId }),
            cancellationToken);
}
