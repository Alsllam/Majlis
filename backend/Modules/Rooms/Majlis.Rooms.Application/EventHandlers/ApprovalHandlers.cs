using System.Text.Json;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Application.Sessions;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Rooms.Application.EventHandlers;

/// <summary>Approvals → the session timeline (FR-APR-004/007): everyone in the room sees the card and its outcome live.</summary>
internal static class ApprovalEvents
{
    /// <summary>Appends once per (session, type, request); a redelivered message changes nothing.</summary>
    public static async Task AppendOnceAsync(
        Guid sessionId,
        string type,
        Guid requestId,
        object data,
        IRepository<AgentSession, Guid> sessions,
        IReadOnlyRepository<SessionEvent, Guid> events,
        SessionTimeline timeline,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var session = await sessions.QueryTracked().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return;
        }

        var marker = $"\"requestId\":\"{requestId}\"";
        var exists = await events.Query().AnyAsync(e => e.SessionId == sessionId && e.Type == type && e.DataJson.Contains(marker), cancellationToken);
        if (exists)
        {
            return;
        }

        await timeline.AppendAsync(session, type, data, EventActor.System, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

[WolverineHandler]
public static class ApprovalRequestedHandler
{
    public static Task Handle(ApprovalRequested m, IRepository<AgentSession, Guid> sessions, IReadOnlyRepository<SessionEvent, Guid> events, SessionTimeline timeline, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        using var args = JsonDocument.Parse(m.ArgsJson);
        return ApprovalEvents.AppendOnceAsync(
            m.SessionId, SessionEventTypes.ApprovalRequested, m.RequestId,
            new
            {
                requestId = m.RequestId, tool = m.Tool, summary = m.Summary, reason = m.Reason, risk = m.Risk, args = args.RootElement.Clone(),
                requestedBy = new { userId = m.RequestedByUserId, displayName = m.RequestedByDisplayName }, expiresAt = m.ExpiresAt, turnId = m.TurnId,
            },
            sessions, events, timeline, unitOfWork, ct);
    }
}

[WolverineHandler]
public static class ApprovalDecidedHandler
{
    public static Task Handle(ApprovalDecided m, IRepository<AgentSession, Guid> sessions, IReadOnlyRepository<SessionEvent, Guid> events, SessionTimeline timeline, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        var type = m.Decision == "Expired" ? SessionEventTypes.ApprovalExpired : SessionEventTypes.ApprovalDecided;
        return ApprovalEvents.AppendOnceAsync(
            m.SessionId, type, m.RequestId,
            new
            {
                requestId = m.RequestId, tool = m.Tool, decision = m.Decision,
                decidedBy = m.DecidedByUserId is { } id ? new { userId = id, displayName = m.DecidedByDisplayName } : null,
                note = m.Note, edited = m.EditedArgsJson is not null,
            },
            sessions, events, timeline, unitOfWork, ct);
    }
}

[WolverineHandler]
public static class ApprovalExecutedHandler
{
    public static Task Handle(ApprovalExecuted m, IRepository<AgentSession, Guid> sessions, IReadOnlyRepository<SessionEvent, Guid> events, SessionTimeline timeline, IUnitOfWork unitOfWork, CancellationToken ct)
        => ApprovalEvents.AppendOnceAsync(
            m.SessionId, SessionEventTypes.ApprovalExecuted, m.RequestId,
            new { requestId = m.RequestId, tool = m.Tool, succeeded = m.Succeeded, resultSummary = m.ResultSummary, entityId = m.EntityId, reasonKey = m.ReasonKey },
            sessions, events, timeline, unitOfWork, ct);
}
