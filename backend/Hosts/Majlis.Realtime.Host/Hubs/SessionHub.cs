using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Security;
using Majlis.Realtime.Host.Auth;
using Majlis.Realtime.Host.Clients;
using Majlis.Realtime.Host.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Majlis.Realtime.Host.Hubs;

/// <summary>What a client receives when it joins: presence and, if a turn is streaming, the text so far.</summary>
public sealed record JoinSessionResult(PresenceSnapshot Presence, StreamSnapshot? Stream);

/// <summary>The text of the running turn so far; deltas with a higher <see cref="Chunk"/> follow.</summary>
public sealed record StreamSnapshot(Guid TurnId, string Text, long Chunk);

/// <summary>
/// The only hub. It never changes business data (ADR-0002): clients join/leave sessions, send presence heartbeats and
/// re-authenticate. Durable events and token deltas are pushed to them.
/// </summary>
[Authorize(AuthenticationSchemes = TicketAuthenticationHandler.SchemeName)]
public sealed class SessionHub(
    IRoomsInternalClient rooms,
    IMemoryCache accessCache,
    PresenceStore presence,
    LocalConnections local,
    IEventPublisher publisher,
    RealtimeTicketStore tickets,
    IConnectionMultiplexer redis,
    TimeProvider clock) : Hub
{
    public const string Path = "/hubs/session";

    /// <summary>A connection must re-authenticate before this; tokens live 15 minutes.</summary>
    public static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(15);

    public static string SessionGroup(Guid sessionId) => $"session:{sessionId:N}";

    private Guid UserId => Guid.Parse(Context.User!.FindFirst(MajlisClaimTypes.Subject)!.Value);

    private string DisplayName => Context.User!.FindFirst(MajlisClaimTypes.Name)?.Value ?? string.Empty;

    public override Task OnConnectedAsync()
    {
        local.Register(Context, UserId, clock.GetUtcNow().Add(ConnectionLifetime));
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var sessionId in local.Unregister(Context.ConnectionId))
        {
            await LeaveInternalAsync(sessionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins a session after the Rooms access check. The client then fetches events after its <c>lastSeq</c> over HTTP
    /// once, to close the gap between opening the session and joining (§5.5).
    /// </summary>
    public async Task<JoinSessionResult> JoinSession(Guid sessionId, Guid? activeTurnId)
    {
        EnsureNotExpired();
        if (!await HasAccessAsync(sessionId))
        {
            throw new HubException("access_denied");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
        local.Join(sessionId, Context.ConnectionId);

        if (await presence.UpsertAsync(sessionId, Context.ConnectionId, UserId, DisplayName, "active"))
        {
            await publisher.PublishAsync(new SessionPresenceRestored(sessionId, UserId, clock.GetUtcNow().UtcDateTime), Context.ConnectionAborted);
        }

        var snapshot = await presence.SnapshotAsync(sessionId);
        await Clients.OthersInGroup(SessionGroup(sessionId)).SendAsync("presence", snapshot);
        return new JoinSessionResult(snapshot, activeTurnId is { } turnId ? await ReadStreamAsync(turnId) : null);
    }

    public async Task LeaveSession(Guid sessionId)
    {
        local.Leave(sessionId, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
        await LeaveInternalAsync(sessionId);
    }

    /// <summary>Every 15 s while the room is open; also carries typing state.</summary>
    public async Task Heartbeat(Guid sessionId, string state)
    {
        EnsureNotExpired();
        if (!PresenceStore.States.Contains(state) || !local.InSession(sessionId).Contains(Context.ConnectionId))
        {
            return;
        }

        var before = await presence.SnapshotAsync(sessionId);
        await presence.UpsertAsync(sessionId, Context.ConnectionId, UserId, DisplayName, state);
        var after = await presence.SnapshotAsync(sessionId);
        if (!before.Participants.SequenceEqual(after.Participants))
        {
            await Clients.Group(SessionGroup(sessionId)).SendAsync("presence", after);
        }
    }

    /// <summary>Extends the connection with a fresh ticket for the same user (every ~10 minutes).</summary>
    public async Task Reauthenticate(string ticket)
    {
        var renewed = await tickets.ValidateAsync(ticket, Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString());
        if (renewed is null || renewed.UserId != UserId)
        {
            Context.Abort();
            return;
        }

        local.Extend(Context.ConnectionId, clock.GetUtcNow().Add(ConnectionLifetime));
    }

    private async Task LeaveInternalAsync(Guid sessionId)
    {
        if (await presence.RemoveAsync(sessionId, Context.ConnectionId) is { } userId)
        {
            await publisher.PublishAsync(new SessionPresenceLost(sessionId, userId, clock.GetUtcNow().UtcDateTime));
        }

        await Clients.Group(SessionGroup(sessionId)).SendAsync("presence", await presence.SnapshotAsync(sessionId));
    }

    private async Task<bool> HasAccessAsync(Guid sessionId)
    {
        var key = $"access:{UserId:N}:{sessionId:N}";
        if (accessCache.TryGetValue(key, out bool allowed))
        {
            return allowed;
        }

        var access = await rooms.GetAccessAsync(new SessionAccessQuery(sessionId, UserId), Context.ConnectionAborted);
        allowed = access.Allowed && access.TenantId?.ToString() == Context.User!.FindFirst(MajlisClaimTypes.Tenant)?.Value;
        accessCache.Set(key, allowed, TimeSpan.FromSeconds(60));
        return allowed;
    }

    private async Task<StreamSnapshot?> ReadStreamAsync(Guid turnId)
    {
        var fields = await redis.GetDatabase().HashGetAllAsync($"majlis:turn:{turnId:N}:text");
        if (fields.Length == 0)
        {
            return null;
        }

        var map = fields.ToDictionary(f => f.Name.ToString(), f => f.Value.ToString());
        return new StreamSnapshot(turnId, map.GetValueOrDefault("text") ?? string.Empty, long.TryParse(map.GetValueOrDefault("chunk"), out var c) ? c : 0);
    }

    private void EnsureNotExpired()
    {
        if (local.IsExpired(Context.ConnectionId, clock.GetUtcNow()))
        {
            throw new HubException("reauth_required");
        }
    }
}
