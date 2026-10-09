using FluentValidation;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Majlis.Rooms.Application.Rooms.DTOs;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Rooms.Application.Rooms;

[Route("rooms")]
public class RoomsAppService(
    IRepository<Room, Guid> rooms,
    IReadOnlyRepository<AgentSession, Guid> sessions,
    ICurrentUser currentUser,
    IValidator<CreateRoomDto> createValidator,
    IValidator<AddRoomParticipantDto> addParticipantValidator) : ApplicationService, IRoomsAppService
{
    /// <inheritdoc />
    [HttpPost("list")]
    [HasPermission(RoomsPermissions.ViewRoom)]
    public async Task<PagedResultDto<RoomListDto>> GetListAsync(FilterRoomDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var query = rooms.Query()
            .Where(r => r.Participants.Any(p => p.UserId == userId)
                && (input.WorkspaceId == null || r.WorkspaceId == input.WorkspaceId)
                && (string.IsNullOrEmpty(input.FilterText) || r.Name.Contains(input.FilterText)));

        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderByDescending(r => r.CreationTime)
            .Skip(input.SkipCount)
            .Take(Math.Clamp(input.MaxResultCount, 1, 100))
            .Select(r => new { r.Id, r.WorkspaceId, r.Name, r.Purpose, Count = r.Participants.Count, r.CreationTime })
            .ToListAsync(cancellationToken);

        var roomIds = page.Select(r => r.Id).ToList();
        var active = await sessions.Query()
            .Where(s => roomIds.Contains(s.RoomId) && s.Status == SessionStatus.Active)
            .Select(s => new { s.RoomId, s.Id })
            .ToDictionaryAsync(s => s.RoomId, s => s.Id, cancellationToken);

        return new PagedResultDto<RoomListDto>(
            page.Select(r => new RoomListDto(r.Id, r.WorkspaceId, r.Name, r.Purpose, active.TryGetValue(r.Id, out var s) ? s : null, r.Count, r.CreationTime)).ToList(),
            total);
    }

    /// <inheritdoc />
    [HttpPost("getbyid")]
    [HasPermission(RoomsPermissions.ViewRoom)]
    public async Task<RoomDto> GetAsync(RoomIdDto input, CancellationToken cancellationToken = default)
    {
        var room = await rooms.Query().Include(r => r.Participants).FirstOrDefaultAsync(r => r.Id == input.Id, cancellationToken)
            ?? throw new EntityNotFoundException();
        room.EnsureParticipant(currentUser.GetRequiredId());

        var activeSessionId = await sessions.Query()
            .Where(s => s.RoomId == room.Id && s.Status == SessionStatus.Active)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return room.ToDto(activeSessionId);
    }

    /// <inheritdoc />
    [HttpPost("")]
    [HasPermission(RoomsPermissions.CreateRoom)]
    public async Task<Guid> CreateAsync(CreateRoomDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(createValidator, input, cancellationToken);

        var room = new Room(Guid.NewGuid(), currentUser.GetRequiredTenantId(), input.WorkspaceId, input.Name.Trim(), input.Purpose?.Trim(), input.Visibility);
        room.AddParticipant(currentUser.GetRequiredId(), currentUser.DisplayName ?? string.Empty, ParticipantRole.Contributor);
        await rooms.InsertAsync(room, autoSave: true, cancellationToken);
        return room.Id;
    }

    /// <inheritdoc />
    [HttpPost("participants")]
    [HasPermission(RoomsPermissions.ManageParticipants)]
    public async Task AddParticipantAsync(AddRoomParticipantDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(addParticipantValidator, input, cancellationToken);

        var room = await rooms.QueryTracked().Include(r => r.Participants).FirstOrDefaultAsync(r => r.Id == input.RoomId, cancellationToken)
            ?? throw new EntityNotFoundException();
        room.EnsureParticipant(currentUser.GetRequiredId());
        room.AddParticipant(input.UserId, input.DisplayName.Trim(), input.Role);
        await rooms.UpdateAsync(room, autoSave: true, cancellationToken);
    }
}
