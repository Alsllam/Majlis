using System.Text.Json;
using Majlis.Rooms.Domain.Enums;

namespace Majlis.Rooms.Application.Sessions.DTOs;

public sealed record SessionActorDto(Guid UserId, string DisplayName);

public sealed record ControlRequestDto(Guid Id, Guid UserId, string DisplayName, DateTime RequestedAt);

/// <summary>Control and turn state; returned by every control command.</summary>
public sealed record SessionStateDto(
    Guid Id,
    Guid RoomId,
    SessionStatus Status,
    SessionActorDto? Driver,
    long ControlEpoch,
    SessionActorDto? PendingHandOffTo,
    string? PendingHandOffNote,
    IReadOnlyList<ControlRequestDto> PendingRequests,
    Guid? ActiveTurnId,
    long LastSeq);

/// <summary>Everything a client needs to open a session: state, the latest events and <c>lastSeq</c> to join the hub with.</summary>
public sealed record SessionDto(SessionStateDto State, IReadOnlyList<SessionEventDto> Events, bool HasOlderEvents);

/// <summary>The envelope clients receive (same shape over HTTP and the hub; docs/architecture/realtime-collaboration.md §4.1).</summary>
public sealed record SessionEventDto(
    int V,
    string Type,
    SessionEventScopeDto Scope,
    long Seq,
    Guid? TurnId,
    DateTime At,
    SessionEventActorDto Actor,
    JsonElement Data);

public sealed record SessionEventScopeDto(Guid TenantId, Guid WorkspaceId, Guid RoomId, Guid SessionId);

public sealed record SessionEventActorDto(string Kind, Guid? Id, string? DisplayName);

public sealed record SessionIdDto
{
    public Guid SessionId { get; init; }
}

public sealed record StartSessionDto
{
    public Guid RoomId { get; init; }
}

public sealed record SessionEventsFilterDto
{
    public Guid SessionId { get; init; }

    /// <summary>Return events with seq greater than this (gap fetch / reconnect).</summary>
    public long? AfterSeq { get; init; }

    /// <summary>Return events with seq lower than this (load older history).</summary>
    public long? BeforeSeq { get; init; }

    public int MaxResultCount { get; init; } = 200;
}

public sealed record SessionEpochDto
{
    public Guid SessionId { get; init; }
    public long Epoch { get; init; }
}

public sealed record InstructSessionDto
{
    public Guid SessionId { get; init; }
    public string Text { get; init; } = string.Empty;
    public long Epoch { get; init; }

    /// <summary>Client-generated id; repeating it returns the same turn.</summary>
    public Guid ClientRequestId { get; init; }

    /// <summary>Optional; detected from the text when missing.</summary>
    public string? Language { get; init; }
}

public sealed record InstructResultDto(Guid TurnId, long Seq);

public sealed record StopTurnDto
{
    public Guid SessionId { get; init; }
    public Guid TurnId { get; init; }
    public long Epoch { get; init; }
}

public sealed record ResolveControlRequestDto
{
    public Guid SessionId { get; init; }
    public Guid RequestId { get; init; }
    public long Epoch { get; init; }
    public bool Accept { get; init; }
}

public sealed record OfferHandOffDto
{
    public Guid SessionId { get; init; }
    public Guid ToUserId { get; init; }
    public long Epoch { get; init; }
    public string? Note { get; init; }
}

public sealed record ResolveHandOffDto
{
    public Guid SessionId { get; init; }
    public bool Accept { get; init; }
}

/// <summary>Internal: may this user read this session? Asked by the Realtime host.</summary>
public sealed record SessionAccessQueryDto
{
    public Guid SessionId { get; init; }
    public Guid UserId { get; init; }
}

public sealed record SessionAccessDto(bool Allowed, Guid? TenantId, Guid? WorkspaceId, Guid? RoomId, ParticipantRole? Role);
