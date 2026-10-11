using System.Text.Json;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Application.Sessions;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Rooms.Application.EventHandlers;

/// <summary>Shared loading and idempotency for ai-service turn results.</summary>
internal static class TurnResults
{
    /// <summary>Applies <paramref name="finish"/> once; duplicates (turn no longer streaming) are ignored.</summary>
    public static async Task HandleAsync(
        Guid sessionId,
        Guid turnId,
        IRepository<AgentSession, Guid> sessions,
        IRepository<Turn, Guid> turns,
        IUnitOfWork unitOfWork,
        Func<AgentSession, Turn, Task<bool>> finish,
        CancellationToken cancellationToken)
    {
        var session = await sessions.QueryTracked().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        var turn = await turns.QueryTracked().FirstOrDefaultAsync(t => t.Id == turnId && t.SessionId == sessionId, cancellationToken);
        if (session is null || turn is null)
        {
            return;
        }

        if (await finish(session, turn))
        {
            session.FinishTurn(turn.Id);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

[WolverineHandler]
public static class TurnCompletedHandler
{
    public static Task Handle(
        TurnCompleted message,
        IRepository<AgentSession, Guid> sessions,
        IRepository<Turn, Guid> turns,
        SessionTimeline timeline,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
        => TurnResults.HandleAsync(message.SessionId, message.TurnId, sessions, turns, unitOfWork, async (session, turn) =>
        {
            var citations = JsonSerializer.Serialize(message.Citations, SessionTimeline.Json);
            if (!turn.Complete(message.Text, citations, message.InputTokens, message.OutputTokens, message.CachedTokens, clock.GetUtcNow().UtcDateTime))
            {
                return false;
            }

            await timeline.AppendAsync(
                session, SessionEventTypes.TurnCompleted,
                new { text = message.Text, citations = message.Citations, usage = new { input = message.InputTokens, output = message.OutputTokens, cached = message.CachedTokens } },
                EventActor.Agent, turn.Id, cancellationToken);
            return true;
        }, cancellationToken);
}

[WolverineHandler]
public static class TurnStoppedHandler
{
    public static Task Handle(
        TurnStopped message,
        IRepository<AgentSession, Guid> sessions,
        IRepository<Turn, Guid> turns,
        SessionTimeline timeline,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
        => TurnResults.HandleAsync(message.SessionId, message.TurnId, sessions, turns, unitOfWork, async (session, turn) =>
        {
            if (!turn.Stop(message.PartialText, clock.GetUtcNow().UtcDateTime))
            {
                return false;
            }

            await timeline.AppendAsync(session, SessionEventTypes.TurnStopped, new { partialText = message.PartialText }, EventActor.Agent, turn.Id, cancellationToken);
            return true;
        }, cancellationToken);
}

[WolverineHandler]
public static class TurnFailedHandler
{
    public static Task Handle(
        TurnFailed message,
        IRepository<AgentSession, Guid> sessions,
        IRepository<Turn, Guid> turns,
        SessionTimeline timeline,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
        => TurnResults.HandleAsync(message.SessionId, message.TurnId, sessions, turns, unitOfWork, async (session, turn) =>
        {
            if (!turn.Fail(message.ReasonKey, message.PartialText, clock.GetUtcNow().UtcDateTime))
            {
                return false;
            }

            await timeline.AppendAsync(
                session, SessionEventTypes.TurnFailed, new { reasonKey = message.ReasonKey, partialText = message.PartialText },
                EventActor.Agent, turn.Id, cancellationToken);
            return true;
        }, cancellationToken);
}
