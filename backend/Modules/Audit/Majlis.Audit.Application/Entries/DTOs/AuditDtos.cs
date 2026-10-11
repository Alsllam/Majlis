using System.Text.Json;
using Majlis.Framework.Application.Dtos;

namespace Majlis.Audit.Application.Entries.DTOs;

public sealed record AuditEntryDto(
    Guid Id,
    long Seq,
    DateTime OccurredAt,
    string Category,
    string Action,
    string ActorKind,
    Guid? ActorUserId,
    string? ActorDisplayName,
    Guid? WorkspaceId,
    Guid? RoomId,
    Guid? SessionId,
    string? EntityType,
    Guid? EntityId,
    JsonElement Details,
    string Hash);

public sealed record FilterAuditDto : BaseFilterRequestDto
{
    public Guid? WorkspaceId { get; init; }
    public string? Category { get; init; }
    public Guid? ActorUserId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

/// <summary>Result of recomputing the tenant's chain (FR / NFR integrity): the first broken link, if any.</summary>
public sealed record ChainVerificationDto(long Entries, bool Valid, long? BrokenAtSeq, string HeadHash, DateTime VerifiedAt);
