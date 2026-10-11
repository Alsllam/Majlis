using System.Text.Json;
using StackExchange.Redis;

namespace Majlis.Realtime.Host.Presence;

public sealed record PresenceEntry(Guid UserId, string DisplayName, string State, long At);

/// <summary>One participant in a presence snapshot (all their connections merged).</summary>
public sealed record PresenceParticipant(Guid UserId, string DisplayName, string State, int Connections);

public sealed record PresenceSnapshot(Guid SessionId, IReadOnlyList<PresenceParticipant> Participants);

/// <summary>
/// Presence lives only in Redis (docs/architecture/realtime-collaboration.md §5.6):
/// <c>majlis:presence:session:{id}</c> = hash connectionId → entry. Entries older than <see cref="StaleAfter"/> are ignored and swept.
/// </summary>
public sealed class PresenceStore(IConnectionMultiplexer redis, TimeProvider clock)
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(45);

    /// <summary>Allowed client states. Typing is throttled client-side to one signal per 3 s.</summary>
    public static readonly string[] States = ["active", "idle", "typing:comment", "typing:suggestion"];

    private const string SessionsKey = "majlis:presence:sessions";

    private static string Key(Guid sessionId) => $"majlis:presence:session:{sessionId:N}";

    /// <summary>Adds or refreshes a connection. Returns true when it is the user's first live connection in the session.</summary>
    public async Task<bool> UpsertAsync(Guid sessionId, string connectionId, Guid userId, string displayName, string state)
    {
        var db = redis.GetDatabase();
        var hadOthers = (await LiveEntriesAsync(sessionId)).Any(e => e.Value.UserId == userId && e.Key != connectionId);
        var entry = new PresenceEntry(userId, displayName, state, clock.GetUtcNow().ToUnixTimeMilliseconds());
        await db.HashSetAsync(Key(sessionId), connectionId, JsonSerializer.Serialize(entry));
        await db.KeyExpireAsync(Key(sessionId), TimeSpan.FromHours(12));
        await db.SetAddAsync(SessionsKey, sessionId.ToString("N"));
        return !hadOthers;
    }

    /// <summary>Removes a connection. Returns the user id when it was the user's last live connection in the session.</summary>
    public async Task<Guid?> RemoveAsync(Guid sessionId, string connectionId)
    {
        var db = redis.GetDatabase();
        var raw = await db.HashGetAsync(Key(sessionId), connectionId);
        await db.HashDeleteAsync(Key(sessionId), connectionId);
        if (raw.IsNullOrEmpty)
        {
            return null;
        }

        var removed = JsonSerializer.Deserialize<PresenceEntry>(raw.ToString())!;
        var stillHere = (await LiveEntriesAsync(sessionId)).Any(e => e.Value.UserId == removed.UserId);
        return stillHere ? null : removed.UserId;
    }

    public async Task<PresenceSnapshot> SnapshotAsync(Guid sessionId)
    {
        var participants = (await LiveEntriesAsync(sessionId))
            .GroupBy(e => e.Value.UserId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(e => e.Value.At).First().Value;
                return new PresenceParticipant(g.Key, latest.DisplayName, latest.State, g.Count());
            })
            .OrderBy(p => p.DisplayName, StringComparer.Ordinal)
            .ToList();
        return new PresenceSnapshot(sessionId, participants);
    }

    /// <summary>Drops stale entries (connections of a crashed instance). Returns (session, user) pairs whose last entry was dropped.</summary>
    public async Task<IReadOnlyList<(Guid SessionId, Guid UserId)>> SweepAsync()
    {
        var db = redis.GetDatabase();
        var gone = new List<(Guid, Guid)>();
        foreach (var member in await db.SetMembersAsync(SessionsKey))
        {
            var sessionId = Guid.ParseExact(member.ToString(), "N");
            var all = await AllEntriesAsync(sessionId);
            if (all.Count == 0)
            {
                await db.SetRemoveAsync(SessionsKey, member);
                continue;
            }

            var cutoff = clock.GetUtcNow().Add(-StaleAfter).ToUnixTimeMilliseconds();
            var stale = all.Where(e => e.Value.At < cutoff).ToList();
            foreach (var (connectionId, _) in stale)
            {
                await db.HashDeleteAsync(Key(sessionId), connectionId);
            }

            var remainingUsers = all.Where(e => e.Value.At >= cutoff).Select(e => e.Value.UserId).ToHashSet();
            gone.AddRange(stale.Select(e => e.Value.UserId).Distinct().Where(u => !remainingUsers.Contains(u)).Select(u => (sessionId, u)));
        }

        return gone;
    }

    private async Task<List<KeyValuePair<string, PresenceEntry>>> LiveEntriesAsync(Guid sessionId)
    {
        var cutoff = clock.GetUtcNow().Add(-StaleAfter).ToUnixTimeMilliseconds();
        return (await AllEntriesAsync(sessionId)).Where(e => e.Value.At >= cutoff).ToList();
    }

    private async Task<List<KeyValuePair<string, PresenceEntry>>> AllEntriesAsync(Guid sessionId)
        => (await redis.GetDatabase().HashGetAllAsync(Key(sessionId)))
            .Select(h => KeyValuePair.Create(h.Name.ToString(), JsonSerializer.Deserialize<PresenceEntry>(h.Value.ToString())!))
            .ToList();
}
