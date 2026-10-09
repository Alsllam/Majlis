using System.Text.Json;
using Majlis.Rooms.Application.Rooms.DTOs;
using Majlis.Rooms.Application.Sessions.DTOs;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;

namespace Majlis.Rooms.Application;

/// <summary>Explicit entity → DTO mapping (no AutoMapper; ADR-0008).</summary>
public static class RoomsMappings
{
    public static RoomDto ToDto(this Room room, Guid? activeSessionId) => new(
        room.Id,
        room.WorkspaceId,
        room.Name,
        room.Purpose,
        room.Visibility,
        room.IsArchived,
        activeSessionId,
        room.Participants.OrderBy(p => p.DisplayName).Select(p => new RoomParticipantDto(p.UserId, p.DisplayName, p.Role)).ToList());

    public static SessionStateDto ToStateDto(this AgentSession session) => new(
        session.Id,
        session.RoomId,
        session.Status,
        session.Driver is { } d ? new SessionActorDto(d.UserId, d.DisplayName) : null,
        session.ControlEpoch,
        session.PendingHandOffToUserId is { } to ? new SessionActorDto(to, session.PendingHandOffToDisplayName ?? string.Empty) : null,
        session.PendingHandOffNote,
        session.Requests
            .Where(r => r.Status == ControlRequestStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .Select(r => new ControlRequestDto(r.Id, r.UserId, r.DisplayName, r.RequestedAt))
            .ToList(),
        session.ActiveTurnId,
        session.LastSeq);

    public static SessionEventDto ToDto(this SessionEvent e, Guid workspaceId, Guid roomId)
    {
        using var data = JsonDocument.Parse(e.DataJson);
        return new SessionEventDto(
            1,
            e.Type,
            new SessionEventScopeDto(e.TenantId, workspaceId, roomId, e.SessionId),
            e.Seq,
            e.TurnId,
            e.At,
            new SessionEventActorDto(e.ActorKind, e.ActorId, e.ActorDisplayName),
            data.RootElement.Clone());
    }
}
