using Majlis.Approvals.Domain.Entities;
using Majlis.Approvals.Domain.Enums;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majlis.Approvals.Application.Background;

/// <summary>FR-APR-006: pending requests past <c>ExpiresAt</c> expire and never execute (moves to the Jobs host later).</summary>
public sealed partial class ExpirySweeper(IServiceScopeFactory scopes, IOptions<ApprovalsOptions> options, TimeProvider clock, ILogger<ExpirySweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.SweepFailed(logger, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds), stoppingToken);
        }
    }

    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var requests = scope.ServiceProvider.GetRequiredService<IRepository<ApprovalRequest, Guid>>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = clock.GetUtcNow().UtcDateTime;

        var due = await requests.QueryTracked().IgnoreQueryFilters()
            .Where(r => !r.IsDeleted && r.Status == ApprovalStatus.Pending && r.ExpiresAt <= now)
            .Take(100)
            .ToListAsync(cancellationToken);
        var count = 0;
        foreach (var request in due.Where(r => r.Expire(now)))
        {
            await publisher.PublishAsync(new ApprovalDecided(request.TenantId, request.SessionId, request.Id, request.Tool, "Expired", null, null, null, null), cancellationToken);
            count++;
        }

        if (count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            Log.Expired(logger, count);
        }

        return count;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Expired {Count} approval requests")]
        public static partial void Expired(ILogger logger, int count);

        [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Approval expiry sweep failed")]
        public static partial void SweepFailed(ILogger logger, Exception exception);
    }
}
