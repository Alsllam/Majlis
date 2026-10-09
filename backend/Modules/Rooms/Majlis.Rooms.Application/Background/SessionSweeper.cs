using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Application.Ai;
using Majlis.Rooms.Application.Sessions;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majlis.Rooms.Application.Background;

/// <summary>
/// Frees control when the driver has been away longer than the absence timeout, and fails turns whose
/// ai-service worker stopped sending heartbeats. Both are epoch/state checked, so a late run is harmless.
/// </summary>
public sealed partial class SessionSweeper(IServiceScopeFactory scopes, IOptions<RoomsOptions> options, TimeProvider clock, ILogger<SessionSweeper> logger) : BackgroundService
{
    /// <summary>Without Redis there is no heartbeat; a turn this old is assumed dead.</summary>
    private static readonly TimeSpan NoHeartbeatMaxAge = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.SweepIntervalSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.SweepFailed(logger, ex);
            }
        }
    }

    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await FreeAbsentDriversAsync(now, cancellationToken);
        await FailStuckTurnsAsync(now, cancellationToken);
    }

    private async Task FreeAbsentDriversAsync(DateTime now, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(options.Value.DriverAbsenceTimeoutSeconds);
        var cutoff = now - timeout;

        List<Guid> due;
        using (var scope = scopes.CreateScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<IReadOnlyRepository<AgentSession, Guid>>().Query()
                .Where(s => s.Status == SessionStatus.Active && s.DriverAbsentSince != null && s.DriverAbsentSince <= cutoff)
                .Select(s => s.Id)
                .Take(100)
                .ToListAsync(cancellationToken);
        }

        foreach (var id in due)
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            try
            {
                var session = await sp.GetRequiredService<IRepository<AgentSession, Guid>>().QueryTracked()
                    .Include(s => s.Requests).FirstAsync(s => s.Id == id, cancellationToken);
                if (session.FreeIfDriverAbsent(now, timeout) is { } change)
                {
                    await sp.GetRequiredService<SessionTimeline>().AppendControlChangeAsync(session, change, EventActor.System, cancellationToken);
                    await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex) when (Framework.EntityFrameworkCore.DbConflicts.IsConflict(ex))
            {
                // Someone changed the session at the same time; the next sweep re-checks it.
            }
        }
    }

    private async Task FailStuckTurnsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var cutoff = now - TimeSpan.FromSeconds(options.Value.TurnHeartbeatTimeoutSeconds);

        List<(Guid TurnId, Guid SessionId, DateTime Created)> candidates;
        using (var scope = scopes.CreateScope())
        {
            candidates = (await scope.ServiceProvider.GetRequiredService<IReadOnlyRepository<Turn, Guid>>().Query()
                    .Where(t => t.Status == TurnStatus.Streaming && t.CreationTime <= cutoff)
                    .Select(t => new { t.Id, t.SessionId, t.CreationTime })
                    .Take(100)
                    .ToListAsync(cancellationToken))
                .Select(t => (t.Id, t.SessionId, t.CreationTime))
                .ToList();
        }

        foreach (var (turnId, sessionId, created) in candidates)
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var alive = await sp.GetRequiredService<ITurnSignals>().HasHeartbeatAsync(turnId);
            if (alive == true || (alive is null && now - created < NoHeartbeatMaxAge))
            {
                continue;
            }

            try
            {
                var session = await sp.GetRequiredService<IRepository<AgentSession, Guid>>().QueryTracked().FirstAsync(s => s.Id == sessionId, cancellationToken);
                var turn = await sp.GetRequiredService<IRepository<Turn, Guid>>().QueryTracked().FirstAsync(t => t.Id == turnId, cancellationToken);
                if (turn.Fail("Rooms:Turn:AiUnavailable", null, now))
                {
                    session.FinishTurn(turn.Id);
                    await sp.GetRequiredService<SessionTimeline>().AppendAsync(
                        session, SessionEventTypes.TurnFailed, new { reasonKey = "Rooms:Turn:AiUnavailable" }, EventActor.System, turn.Id, cancellationToken);
                    await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
                    Log.TurnFailedNoHeartbeat(logger, turnId);
                }
            }
            catch (Exception ex) when (Framework.EntityFrameworkCore.DbConflicts.IsConflict(ex))
            {
                // The turn finished at the same time; nothing to do.
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2101, Level = LogLevel.Error, Message = "Session sweep failed")]
        public static partial void SweepFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "Turn {TurnId} failed: no heartbeat from ai-service")]
        public static partial void TurnFailedNoHeartbeat(ILogger logger, Guid turnId);
    }
}
