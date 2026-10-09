using Majlis.Framework.Application.Dtos;
using Majlis.Rooms.Domain.Enums;

namespace Majlis.Rooms.Application.Rooms.DTOs;

public sealed record RoomDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Purpose,
    RoomVisibility Visibility,
    bool IsArchived,
    Guid? ActiveSessionId,
    IReadOnlyList<RoomParticipantDto> Participants);

public sealed record RoomListDto(Guid Id, Guid WorkspaceId, string Name, string? Purpose, Guid? ActiveSessionId, int ParticipantCount, DateTime CreationTime);

public sealed record RoomParticipantDto(Guid UserId, string DisplayName, ParticipantRole Role);

public sealed record RoomIdDto(Guid Id);

public sealed record CreateRoomDto
{
    public Guid WorkspaceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Purpose { get; init; }
    public RoomVisibility Visibility { get; init; } = RoomVisibility.Private;
}

public sealed record AddRoomParticipantDto
{
    public Guid RoomId { get; init; }
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public ParticipantRole Role { get; init; } = ParticipantRole.Contributor;
}

public sealed record FilterRoomDto : BaseFilterRequestDto
{
    public Guid? WorkspaceId { get; init; }
}
