using System.Text.Json;
using Majlis.Framework.Domain.Events;
using Majlis.Realtime.Host.Hubs;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Majlis.Realtime.Host.Fanout;

/// <summary>
/// Pushes every durable session event to the session group. One instance consumes each message (competing consumers);
/// the Redis backplane delivers it to connections on every instance. Clients apply events strictly by <c>seq</c>.
/// </summary>
public sealed class SessionEventAppendedConsumer(IHubContext<SessionHub> hub) : IConsumer<SessionEventAppended>
{
    public async Task Consume(ConsumeContext<SessionEventAppended> context)
    {
        var m = context.Message;
        using var data = JsonDocument.Parse(m.DataJson);
        var envelope = new
        {
            v = 1,
            type = m.Type,
            scope = new { tenantId = m.TenantId, workspaceId = m.WorkspaceId, roomId = m.RoomId, sessionId = m.SessionId },
            seq = m.Seq,
            turnId = m.TurnId,
            at = m.At,
            actor = new { kind = m.ActorKind, id = m.ActorId, displayName = m.ActorDisplayName },
            data = data.RootElement.Clone(),
        };
        await hub.Clients.Group(SessionHub.SessionGroup(m.SessionId)).SendAsync("sessionEvent", envelope, context.CancellationToken);
    }
}
