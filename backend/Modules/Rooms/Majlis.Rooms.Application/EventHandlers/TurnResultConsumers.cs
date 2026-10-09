using System.Text.Json;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Application.Sessions;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Rooms.Application.EventHandlers;

/// <summary>Shared loading and idempotency for ai-service turn results.</summary>
public abstract class TurnResultConsumerBase(
    IRepository<AgentSession, Guid> sessions,
    IRepository<Turn, Guid> turns,
    SessionTimeline timeline,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    protected SessionTimeline Timeline { get; } = timeline;

    protected DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Applies <paramref name="finish"/> once; duplicates (turn no longer streaming) are ignored.</summary>
    protected async Task HandleAsync(Guid sessionId, Guid turnId, Func<AgentSession, Turn, Task<bool>> finish, CancellationToken cancellationToken)
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

public sealed class TurnCompletedConsumer(
    IRepository<AgentSession, Guid> sessions, IRepository<Turn, Guid> turns, SessionTimeline timeline, IUnitOfWork unitOfWork, TimeProvider clock)
    : TurnResultConsumerBase(sessions, turns, timeline, unitOfWork, clock), IConsumer<TurnCompleted>
{
    public Task Consume(ConsumeContext<TurnCompleted> context)
    {
        var m = context.Message;
        return HandleAsync(m.SessionId, m.TurnId, async (session, turn) =>
        {
            var citations = JsonSerializer.Serialize(m.Citations, SessionTimeline.Json);
            if (!turn.Complete(m.Text, citations, m.InputTokens, m.OutputTokens, m.CachedTokens, Now))
            {
                return false;
            }

            await Timeline.AppendAsync(
                session, SessionEventTypes.TurnCompleted,
                new { text = m.Text, citations = m.Citations, usage = new { input = m.InputTokens, output = m.OutputTokens, cached = m.CachedTokens } },
                EventActor.Agent, turn.Id, context.CancellationToken);
            return true;
        }, context.CancellationToken);
    }
}

public sealed class TurnStoppedConsumer(
    IRepository<AgentSession, Guid> sessions, IRepository<Turn, Guid> turns, SessionTimeline timeline, IUnitOfWork unitOfWork, TimeProvider clock)
    : TurnResultConsumerBase(sessions, turns, timeline, unitOfWork, clock), IConsumer<TurnStopped>
{
    public Task Consume(ConsumeContext<TurnStopped> context)
    {
        var m = context.Message;
        return HandleAsync(m.SessionId, m.TurnId, async (session, turn) =>
        {
            if (!turn.Stop(m.PartialText, Now))
            {
                return false;
            }

            await Timeline.AppendAsync(session, SessionEventTypes.TurnStopped, new { partialText = m.PartialText }, EventActor.Agent, turn.Id, context.CancellationToken);
            return true;
        }, context.CancellationToken);
    }
}

public sealed class TurnFailedConsumer(
    IRepository<AgentSession, Guid> sessions, IRepository<Turn, Guid> turns, SessionTimeline timeline, IUnitOfWork unitOfWork, TimeProvider clock)
    : TurnResultConsumerBase(sessions, turns, timeline, unitOfWork, clock), IConsumer<TurnFailed>
{
    public Task Consume(ConsumeContext<TurnFailed> context)
    {
        var m = context.Message;
        return HandleAsync(m.SessionId, m.TurnId, async (session, turn) =>
        {
            if (!turn.Fail(m.ReasonKey, m.PartialText, Now))
            {
                return false;
            }

            await Timeline.AppendAsync(session, SessionEventTypes.TurnFailed, new { reasonKey = m.ReasonKey, partialText = m.PartialText }, EventActor.Agent, turn.Id, context.CancellationToken);
            return true;
        }, context.CancellationToken);
    }
}
