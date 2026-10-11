using System.Text.Json;
using Majlis.Realtime.Host.Hubs;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Majlis.Realtime.Host.Streaming;

/// <summary>
/// The stream lane (ADR-0003): ai-service publishes token deltas and progress ticks to Redis pub/sub on
/// <c>majlis:session:{sessionId}:stream</c>. Every instance subscribes and forwards each message only to its own
/// connections in that session, so nothing is duplicated and deltas never touch the database.
/// Message: <c>{ "type": "turn.delta" | "turn.progress", "turnId", "chunk", "data": { ... } }</c>.
/// </summary>
public sealed partial class StreamRelay(IConnectionMultiplexer redis, LocalConnections local, IHubContext<SessionHub> hub, ILogger<StreamRelay> logger) : BackgroundService
{
    public static readonly RedisChannel Channel = RedisChannel.Pattern("majlis:session:*:stream");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = redis.GetSubscriber();
        var queue = await subscriber.SubscribeAsync(Channel);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = await queue.ReadAsync(stoppingToken);
                await RelayAsync(message.Channel.ToString(), message.Message.ToString(), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        finally
        {
            await queue.UnsubscribeAsync();
        }
    }

    public async Task RelayAsync(string channel, string payload, CancellationToken cancellationToken)
    {
        // majlis:session:{id}:stream
        var parts = channel.Split(':');
        if (parts.Length != 4 || !Guid.TryParse(parts[2], out var sessionId))
        {
            return;
        }

        var connections = local.InSession(sessionId);
        if (connections.Count == 0)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            await hub.Clients.Clients(connections).SendAsync("streamEvent", doc.RootElement.Clone(), cancellationToken);
        }
        catch (JsonException ex)
        {
            Log.BadPayload(logger, ex, sessionId);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "Ignored a malformed stream message for session {SessionId}")]
        public static partial void BadPayload(ILogger logger, Exception exception, Guid sessionId);
    }
}
