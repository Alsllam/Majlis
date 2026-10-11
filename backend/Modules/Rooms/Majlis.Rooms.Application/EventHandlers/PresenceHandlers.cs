using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Rooms.Application.EventHandlers;

/// <summary>The driver's last connection left: start the absence clock. The sweeper frees control after the timeout.</summary>
[WolverineHandler]
public static class SessionPresenceLostHandler
{
    public static async Task Handle(SessionPresenceLost message, IRepository<AgentSession, Guid> sessions, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var session = await sessions.QueryTracked()
            .FirstOrDefaultAsync(s => s.Id == message.SessionId && s.DriverUserId == message.UserId, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.MarkDriverAbsent(message.UserId, message.At);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>The driver came back before the timeout.</summary>
[WolverineHandler]
public static class SessionPresenceRestoredHandler
{
    public static async Task Handle(SessionPresenceRestored message, IRepository<AgentSession, Guid> sessions, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var session = await sessions.QueryTracked()
            .FirstOrDefaultAsync(s => s.Id == message.SessionId && s.DriverUserId == message.UserId, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.MarkPresent(message.UserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
