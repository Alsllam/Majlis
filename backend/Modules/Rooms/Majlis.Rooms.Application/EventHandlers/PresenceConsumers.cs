using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Rooms.Application.EventHandlers;

/// <summary>The driver's last connection left: start the absence clock. The sweeper frees control after the timeout.</summary>
public sealed class SessionPresenceLostConsumer(IRepository<AgentSession, Guid> sessions, IUnitOfWork unitOfWork) : IConsumer<SessionPresenceLost>
{
    public async Task Consume(ConsumeContext<SessionPresenceLost> context)
    {
        var m = context.Message;
        var session = await sessions.QueryTracked().FirstOrDefaultAsync(s => s.Id == m.SessionId && s.DriverUserId == m.UserId, context.CancellationToken);
        if (session is null)
        {
            return;
        }

        session.MarkDriverAbsent(m.UserId, m.At);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>The driver came back before the timeout.</summary>
public sealed class SessionPresenceRestoredConsumer(IRepository<AgentSession, Guid> sessions, IUnitOfWork unitOfWork) : IConsumer<SessionPresenceRestored>
{
    public async Task Consume(ConsumeContext<SessionPresenceRestored> context)
    {
        var m = context.Message;
        var session = await sessions.QueryTracked().FirstOrDefaultAsync(s => s.Id == m.SessionId && s.DriverUserId == m.UserId, context.CancellationToken);
        if (session is null)
        {
            return;
        }

        session.MarkPresent(m.UserId);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
