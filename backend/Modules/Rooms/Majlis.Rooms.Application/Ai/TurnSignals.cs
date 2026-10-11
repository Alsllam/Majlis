using StackExchange.Redis;

namespace Majlis.Rooms.Application.Ai;

/// <summary>Fast signals shared with ai-service through Redis (docs/architecture/realtime-collaboration.md §5.1–5.2).</summary>
public interface ITurnSignals
{
    /// <summary>Sets <c>majlis:turn:{id}:cancel</c>; ai-service checks it between deltas.</summary>
    Task RequestCancelAsync(Guid turnId);

    /// <summary>True when ai-service refreshed <c>majlis:turn:{id}:hb</c> recently. Null when Redis is not configured.</summary>
    Task<bool?> HasHeartbeatAsync(Guid turnId);
}

public static class TurnSignalKeys
{
    public static string Cancel(Guid turnId) => $"majlis:turn:{turnId:N}:cancel";

    public static string Heartbeat(Guid turnId) => $"majlis:turn:{turnId:N}:hb";

    public static string Text(Guid turnId) => $"majlis:turn:{turnId:N}:text";
}

public sealed class RedisTurnSignals(IConnectionMultiplexer redis) : ITurnSignals
{
    public Task RequestCancelAsync(Guid turnId)
        => redis.GetDatabase().StringSetAsync(TurnSignalKeys.Cancel(turnId), "1", TimeSpan.FromMinutes(10));

    public async Task<bool?> HasHeartbeatAsync(Guid turnId)
        => await redis.GetDatabase().KeyExistsAsync(TurnSignalKeys.Heartbeat(turnId));
}

/// <summary>Without Redis there is no fast path: stop goes through the TurnStopRequested message only.</summary>
public sealed class NoTurnSignals : ITurnSignals
{
    public Task RequestCancelAsync(Guid turnId) => Task.CompletedTask;

    public Task<bool?> HasHeartbeatAsync(Guid turnId) => Task.FromResult<bool?>(null);
}
