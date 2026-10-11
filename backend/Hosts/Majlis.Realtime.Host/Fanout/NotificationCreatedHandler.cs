using System.Text.Json;
using Majlis.Framework.Domain.Events;
using Majlis.Realtime.Host.Hubs;
using Microsoft.AspNetCore.SignalR;
using Wolverine.Attributes;

namespace Majlis.Realtime.Host.Fanout;

/// <summary>Pushes a new in-app notification to every connection of its user (SignalR user id = token subject).</summary>
[WolverineHandler]
public static class NotificationCreatedHandler
{
    public static async Task Handle(NotificationCreated m, IHubContext<SessionHub> hub, CancellationToken cancellationToken)
    {
        using var data = JsonDocument.Parse(m.ParamsJson);
        var envelope = new { id = m.NotificationId, type = m.Type, @params = data.RootElement.Clone(), link = m.Link, createdAt = m.At, readAt = (DateTime?)null };
        await hub.Clients.User(m.UserId.ToString()).SendAsync("notification", envelope, cancellationToken);
    }
}
