using System.Security.Cryptography;
using System.Text;
using Majlis.Framework.Domain.Entities;

namespace Majlis.Audit.Domain.Entities;

/// <summary>
/// One append-only audit record (invariant 7). Never updated or deleted; each entry carries the hash of the previous
/// one of its tenant, so any later change to a row breaks the chain (`verify`). Details hold metadata only, never
/// document or conversation content.
/// </summary>
public class AuditEntry : Entity<Guid>, IMultiTenant
{
    protected AuditEntry()
    {
    }

    public AuditEntry(
        Guid id,
        Guid tenantId,
        long seq,
        DateTime occurredAt,
        string category,
        string action,
        string actorKind,
        Guid? actorUserId,
        string? actorDisplayName,
        Guid? workspaceId,
        Guid? roomId,
        Guid? sessionId,
        string? entityType,
        Guid? entityId,
        string detailsJson,
        string sourceKey,
        string previousHash)
        : base(id)
    {
        TenantId = tenantId;
        Seq = seq;
        OccurredAt = occurredAt;
        Category = category;
        Action = action;
        ActorKind = actorKind;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        WorkspaceId = workspaceId;
        RoomId = roomId;
        SessionId = sessionId;
        EntityType = entityType;
        EntityId = entityId;
        DetailsJson = detailsJson;
        SourceKey = sourceKey;
        PreviousHash = previousHash;
        Hash = ComputeHash();
    }

    public Guid TenantId { get; private set; }

    /// <summary>Position in the tenant's chain, 1-based and gapless.</summary>
    public long Seq { get; private set; }

    public DateTime OccurredAt { get; private set; }
    public DateTime RecordedAt { get; set; }
    public string Category { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ActorKind { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public string? ActorDisplayName { get; private set; }
    public Guid? WorkspaceId { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string DetailsJson { get; private set; } = "{}";

    /// <summary>Unique per source event; a redelivered message writes nothing.</summary>
    public string SourceKey { get; private set; } = string.Empty;

    public string PreviousHash { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 over the previous hash and every recorded field; recomputed by <c>verify</c>. The time goes in as ticks
    /// so the value is the same before and after the database round trip (which drops the <c>DateTimeKind</c>).
    /// </summary>
    public string ComputeHash()
    {
        var material = string.Join('\n',
            PreviousHash, TenantId.ToString("D"), Seq.ToString(System.Globalization.CultureInfo.InvariantCulture),
            OccurredAt.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), Category, Action, ActorKind,
            ActorUserId?.ToString("D") ?? string.Empty, ActorDisplayName ?? string.Empty, WorkspaceId?.ToString("D") ?? string.Empty,
            RoomId?.ToString("D") ?? string.Empty, SessionId?.ToString("D") ?? string.Empty, EntityType ?? string.Empty,
            EntityId?.ToString("D") ?? string.Empty, DetailsJson, SourceKey);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}

/// <summary>Head of a tenant's chain; the row version serializes concurrent appends (a conflict is retried).</summary>
public class AuditChain : Entity<Guid>, IMultiTenant, IHasConcurrencyStamp
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    protected AuditChain()
    {
    }

    public AuditChain(Guid tenantId)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
    }

    public Guid TenantId { get; private set; }
    public long LastSeq { get; private set; }
    public string LastHash { get; private set; } = GenesisHash;
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Outcome of the last full recomputation (the nightly job or a person's verify call).</summary>
    public DateTime? LastVerifiedAt { get; private set; }

    public bool? LastVerificationValid { get; private set; }

    public long? LastBrokenSeq { get; private set; }

    public void Advance(AuditEntry entry)
    {
        LastSeq = entry.Seq;
        LastHash = entry.Hash;
    }

    public void RecordVerification(DateTime at, bool valid, long? brokenSeq)
    {
        LastVerifiedAt = at;
        LastVerificationValid = valid;
        LastBrokenSeq = valid ? null : brokenSeq;
    }
}
