using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Majlis.Realtime.Host.Hubs;

/// <summary>
/// Connections held by THIS instance, by session. The stream relay sends token deltas only to local connections,
/// so every instance can subscribe to the stream channel without duplicates (the backplane is not used for deltas).
/// </summary>
public sealed class LocalConnections
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _bySession = new();
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();

    public void Register(HubCallerContext context, Guid userId, DateTimeOffset expiresAt)
        => _connections[context.ConnectionId] = new ConnectionInfo(context, userId, expiresAt);

    public void Extend(string connectionId, DateTimeOffset expiresAt)
    {
        if (_connections.TryGetValue(connectionId, out var info))
        {
            _connections[connectionId] = info with { ExpiresAt = expiresAt };
        }
    }

    public bool IsExpired(string connectionId, DateTimeOffset now)
        => !_connections.TryGetValue(connectionId, out var info) || info.ExpiresAt <= now;

    public void Join(Guid sessionId, string connectionId)
        => _bySession.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, byte>())[connectionId] = 0;

    public void Leave(Guid sessionId, string connectionId)
    {
        if (_bySession.TryGetValue(sessionId, out var set))
        {
            set.TryRemove(connectionId, out _);
        }
    }

    /// <summary>Sessions the connection joined (for cleanup on disconnect).</summary>
    public IReadOnlyList<Guid> Unregister(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
        var sessions = new List<Guid>();
        foreach (var (sessionId, set) in _bySession)
        {
            if (set.TryRemove(connectionId, out _))
            {
                sessions.Add(sessionId);
            }
        }

        return sessions;
    }

    public IReadOnlyList<string> InSession(Guid sessionId)
        => _bySession.TryGetValue(sessionId, out var set) ? set.Keys.ToList() : [];

    public IReadOnlyList<HubCallerContext> Expired(DateTimeOffset now)
        => _connections.Values.Where(c => c.ExpiresAt <= now).Select(c => c.Context).ToList();

    private sealed record ConnectionInfo(HubCallerContext Context, Guid UserId, DateTimeOffset ExpiresAt);
}
