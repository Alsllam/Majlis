using Majlis.Framework.Domain.Events;
using Majlis.Realtime.Host.Hubs;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Majlis.Realtime.Host.Presence;

/// <summary>
/// Every 15 s: closes local connections that missed re-authentication, and (on one instance at a time) removes presence
/// entries of connections that died with their instance, reporting users who are now gone.
/// </summary>
public sealed partial class PresenceSweeper(
    IServiceScopeFactory scopes, PresenceStore presence, LocalConnections local, IHubContext<SessionHub> hub,
    IConnectionMultiplexer redis, TimeProvider clock, ILogger<PresenceSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var expired in local.Expired(clock.GetUtcNow()))
                {
                    expired.Abort();
                }

                if (await redis.GetDatabase().StringSetAsync("majlis:lock:presence-sweep", Environment.MachineName, Interval - TimeSpan.FromSeconds(1), When.NotExists))
                {
                    await SweepPresenceAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.SweepFailed(logger, ex);
            }
        }
    }

    private async Task SweepPresenceAsync(CancellationToken cancellationToken)
    {
        var gone = await presence.SweepAsync();
        if (gone.Count == 0)
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        foreach (var (sessionId, userId) in gone)
        {
            await publisher.Publish(new SessionPresenceLost(sessionId, userId, clock.GetUtcNow().UtcDateTime), cancellationToken);
        }

        foreach (var sessionId in gone.Select(g => g.SessionId).Distinct())
        {
            await hub.Clients.Group(SessionHub.SessionGroup(sessionId)).SendAsync("presence", await presence.SnapshotAsync(sessionId), cancellationToken);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 3101, Level = LogLevel.Error, Message = "Presence sweep failed")]
        public static partial void SweepFailed(ILogger logger, Exception exception);
    }
}
