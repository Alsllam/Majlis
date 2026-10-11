using System.Text.Json;
using Majlis.Audit.Domain.Entities;
using Majlis.Framework.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Audit.Application.Recording;

/// <summary>What a consumer asks to record. <see cref="Details"/> is metadata only (ids, names, decisions), never content.</summary>
public sealed record AuditRecord(
    Guid TenantId,
    DateTime OccurredAt,
    string Category,
    string Action,
    string ActorKind,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string SourceKey,
    Guid? WorkspaceId = null,
    Guid? RoomId = null,
    Guid? SessionId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    object? Details = null);

/// <summary>
/// Appends to the tenant's chain: next seq, hash over the previous hash. Idempotent on <c>SourceKey</c>. Two
/// consumers appending at once collide on the chain's row version; the messaging layer retries the loser.
/// </summary>
public sealed class AuditRecorder(IRepository<AuditEntry, Guid> entries, IRepository<AuditChain, Guid> chains, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>Readable Arabic in the stored details and the CSV export (no HTML context, so relaxed escaping is safe).</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Returns the entry written, or null when the source event was already recorded.</summary>
    public async Task<AuditEntry?> RecordAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        if (await entries.Query().AnyAsync(e => e.SourceKey == record.SourceKey, cancellationToken))
        {
            return null;
        }

        var chain = await chains.QueryTracked().FirstOrDefaultAsync(c => c.TenantId == record.TenantId, cancellationToken);
        var isNew = chain is null;
        chain ??= new AuditChain(record.TenantId);
        var entry = new AuditEntry(
            Guid.NewGuid(), record.TenantId, chain.LastSeq + 1, DateTime.SpecifyKind(record.OccurredAt, DateTimeKind.Utc), record.Category, record.Action,
            record.ActorKind, record.ActorUserId, record.ActorDisplayName, record.WorkspaceId, record.RoomId, record.SessionId, record.EntityType,
            record.EntityId, JsonSerializer.Serialize(record.Details ?? new { }, Json), record.SourceKey, chain.LastHash)
        {
            RecordedAt = clock.GetUtcNow().UtcDateTime,
        };
        chain.Advance(entry);
        await entries.InsertAsync(entry, autoSave: false, cancellationToken);
        if (isNew)
        {
            await chains.InsertAsync(chain, autoSave: false, cancellationToken);
        }
        else
        {
            await chains.UpdateAsync(chain, autoSave: false, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entry;
    }
}
