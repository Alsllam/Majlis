using Majlis.Framework.Application.Dtos;
using Majlis.Rooms.Application.Rooms.DTOs;

namespace Majlis.Rooms.Application.Rooms;

public interface IRoomsAppService
{
    /// <summary>Rooms the caller participates in, newest first.</summary>
    Task<PagedResultDto<RoomListDto>> GetListAsync(FilterRoomDto input, CancellationToken cancellationToken = default);

    /// <summary>One room with its participants and active session id.</summary>
    Task<RoomDto> GetAsync(RoomIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Creates a room; the creator joins as a contributor. Returns the new id.</summary>
    Task<Guid> CreateAsync(CreateRoomDto input, CancellationToken cancellationToken = default);

    /// <summary>Adds a participant or changes their role.</summary>
    Task AddParticipantAsync(AddRoomParticipantDto input, CancellationToken cancellationToken = default);
}
