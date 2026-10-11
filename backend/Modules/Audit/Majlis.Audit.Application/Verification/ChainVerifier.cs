using Majlis.Audit.Domain.Entities;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine.Attributes;

namespace Majlis.Audit.Application.Verification;

public sealed record ChainVerification(long Count, bool Valid, long? BrokenSeq, string LastHash, DateTime VerifiedAt);

/// <summary>
/// Recomputes a tenant's hash chain from the first entry (invariant 7) and, when asked, records the outcome on the
/// chain head so the nightly result is visible without recomputing. Used by the verify endpoint and the nightly job.
/// </summary>
public sealed partial class ChainVerifier(IReadOnlyRepository<AuditEntry, Guid> entries, IRepository<AuditChain, Guid> chains, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<ChainVerifier> logger)
{
    public async Task<ChainVerification> VerifyAsync(Guid tenantId, bool record, CancellationToken cancellationToken)
    {
        var chain = await chains.QueryTracked().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);
        var previous = AuditChain.GenesisHash;
        long expected = 0;
        long? broken = null;
        var count = 0L;
        await foreach (var entry in entries.Query().IgnoreQueryFilters().Where(e => e.TenantId == tenantId).OrderBy(e => e.Seq).AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            count++;
            expected++;
            if (entry.Seq != expected || entry.PreviousHash != previous || entry.ComputeHash() != entry.Hash)
            {
                broken = entry.Seq;
                break;
            }

            previous = entry.Hash;
        }

        var valid = broken is null && (chain is null ? count == 0 : chain.LastSeq == count && chain.LastHash == previous);
        var now = clock.GetUtcNow().UtcDateTime;
        var result = new ChainVerification(count, valid, broken ?? (valid ? null : count), previous, now);
        if (!valid)
        {
            Log.Broken(logger, tenantId, result.BrokenSeq);
        }

        if (record && chain is not null)
        {
            chain.RecordVerification(now, valid, result.BrokenSeq);
            await chains.UpdateAsync(chain, autoSave: false, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>Every tenant with a chain; the Jobs tick runs this nightly.</summary>
    public async Task<int> VerifyAllAsync(CancellationToken cancellationToken)
    {
        var tenants = await chains.Query().IgnoreQueryFilters().Select(c => c.TenantId).ToListAsync(cancellationToken);
        var broken = 0;
        foreach (var tenantId in tenants)
        {
            var result = await VerifyAsync(tenantId, record: true, cancellationToken);
            if (!result.Valid)
            {
                broken++;
            }
        }

        Log.Nightly(logger, tenants.Count, broken);
        return broken;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 8101, Level = LogLevel.Critical, Message = "Audit chain of tenant {TenantId} is broken at seq {Seq}")]
        public static partial void Broken(ILogger logger, Guid tenantId, long? seq);

        [LoggerMessage(EventId = 8102, Level = LogLevel.Information, Message = "Verified {Tenants} audit chains; {Broken} broken")]
        public static partial void Nightly(ILogger logger, int tenants, int broken);
    }
}

[WolverineHandler]
public static class AuditChainVerificationRequestedHandler
{
    public static Task Handle(AuditChainVerificationRequested m, ChainVerifier verifier, CancellationToken cancellationToken)
    {
        _ = m;
        return verifier.VerifyAllAsync(cancellationToken);
    }
}
