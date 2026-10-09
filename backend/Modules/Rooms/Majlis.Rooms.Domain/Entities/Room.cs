using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Enums;

namespace Majlis.Rooms.Domain.Entities;

/// <summary>A topic or project space inside a workspace where people and the agent work.</summary>
public class Room : FullAuditedEntity<Guid>, IMultiTenant
{
    private readonly List<RoomParticipant> _participants = [];

    protected Room()
    {
    }

    public Room(Guid id, Guid tenantId, Guid workspaceId, string name, string? purpose, RoomVisibility visibility)
        : base(id)
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        Name = name;
        Purpose = purpose;
        Visibility = visibility;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Purpose { get; private set; }
    public RoomVisibility Visibility { get; private set; }
    public bool IsArchived { get; private set; }

    public IReadOnlyCollection<RoomParticipant> Participants => _participants;

    public RoomParticipant AddParticipant(Guid userId, string displayName, ParticipantRole role)
    {
        var existing = _participants.FirstOrDefault(p => p.UserId == userId);
        if (existing is not null)
        {
            existing.ChangeRole(role, displayName);
            return existing;
        }

        var participant = new RoomParticipant(Guid.NewGuid(), Id, userId, displayName, role);
        _participants.Add(participant);
        return participant;
    }

    public RoomParticipant? FindParticipant(Guid userId) => _participants.FirstOrDefault(p => p.UserId == userId);

    /// <summary>Throws unless the user is a participant; returns the participant.</summary>
    public RoomParticipant EnsureParticipant(Guid userId)
        => FindParticipant(userId) ?? throw new ForbiddenException(RoomsErrors.NotParticipant);

    /// <summary>Throws unless the user may drive (a contributor).</summary>
    public RoomParticipant EnsureCanDrive(Guid userId)
    {
        var participant = EnsureParticipant(userId);
        if (participant.Role != ParticipantRole.Contributor)
        {
            throw new ForbiddenException(RoomsErrors.ObserverCannotDrive);
        }

        return participant;
    }
}

public class RoomParticipant : CreationAuditedEntity<Guid>
{
    protected RoomParticipant()
    {
    }

    internal RoomParticipant(Guid id, Guid roomId, Guid userId, string displayName, ParticipantRole role)
        : base(id)
    {
        RoomId = roomId;
        UserId = userId;
        DisplayName = displayName;
        Role = role;
    }

    public Guid RoomId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Snapshot of the user's name for timelines; refreshed when the participant is updated.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public ParticipantRole Role { get; private set; }

    internal void ChangeRole(ParticipantRole role, string displayName)
    {
        Role = role;
        DisplayName = displayName;
    }
}
