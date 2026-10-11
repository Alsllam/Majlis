using Majlis.Approvals.Domain.Entities;
using Majlis.Approvals.Domain.Enums;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine.Attributes;

namespace Majlis.Approvals.Application.Background;

/// <summary>FR-APR-006: pending requests past <c>ExpiresAt</c> expire and never execute. Run by the Jobs host tick every minute.</summary>
public sealed partial class ExpirySweeper(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ExpirySweeper> logger)
{
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
    }
}

/// <summary>The Jobs host ticks once a minute; a late or duplicate tick finds nothing due and is harmless.</summary>
[WolverineHandler]
public static class ApprovalExpirySweepRequestedHandler
{
    public static async Task Handle(ApprovalExpirySweepRequested m, ExpirySweeper sweeper, CancellationToken cancellationToken)
    {
        _ = m;
        var expired = await sweeper.SweepOnceAsync(cancellationToken);
        while (expired >= 100)
        {
            expired = await sweeper.SweepOnceAsync(cancellationToken);
        }
    }
}
