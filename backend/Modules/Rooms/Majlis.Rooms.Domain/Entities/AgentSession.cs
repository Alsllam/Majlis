using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Enums;

namespace Majlis.Rooms.Domain.Entities;

/// <summary>A participant as recorded on a session (id + display-name snapshot).</summary>
public readonly record struct SessionActor(Guid UserId, string DisplayName);

/// <summary>The result of a control transition, written to the timeline as <c>control.changed</c>.</summary>
public sealed record ControlChange(ControlChangeKind Kind, SessionActor? From, SessionActor? To, long Epoch, string? Note);

/// <summary>
/// One run of the shared agent inside a room. Single driver at a time, protected by a fencing epoch (ADR-0004):
/// every control transition increments <see cref="ControlEpoch"/>, and driver commands must carry the epoch they were issued under.
/// The session is also the sequencer of its timeline (<see cref="NextSeq"/>, ADR-0003).
/// </summary>
public class AgentSession : AuditedEntity<Guid>, IMultiTenant, IHasConcurrencyStamp
{
    private readonly List<ControlRequest> _requests = [];

    protected AgentSession()
    {
    }

    private AgentSession(Guid id, Guid tenantId, Guid workspaceId, Guid roomId)
        : base(id)
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        RoomId = roomId;
        Status = SessionStatus.Active;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid RoomId { get; private set; }
    public SessionStatus Status { get; private set; }

    public Guid? DriverUserId { get; private set; }
    public string? DriverDisplayName { get; private set; }
    public long ControlEpoch { get; private set; }

    /// <summary>When the driver's last connection left; cleared when they come back.</summary>
    public DateTime? DriverAbsentSince { get; private set; }

    public Guid? PendingHandOffToUserId { get; private set; }
    public string? PendingHandOffToDisplayName { get; private set; }
    public string? PendingHandOffNote { get; private set; }

    public Guid? ActiveTurnId { get; private set; }

    /// <summary>Last sequence number used on this session's timeline.</summary>
    public long LastSeq { get; private set; }

    public DateTime? EndedAt { get; private set; }
    public byte[] RowVersion { get; set; } = [];

    public IReadOnlyCollection<ControlRequest> Requests => _requests;

    public bool IsHeld => DriverUserId is not null;

    public SessionActor? Driver => DriverUserId is { } id ? new SessionActor(id, DriverDisplayName ?? string.Empty) : null;

    /// <summary>Starts a session; the starter becomes the driver (epoch 1).</summary>
    public static (AgentSession Session, ControlChange Change) Start(Guid id, Guid tenantId, Guid workspaceId, Guid roomId, SessionActor starter)
    {
        var session = new AgentSession(id, tenantId, workspaceId, roomId);
        var change = session.SetDriver(ControlChangeKind.Start, starter, null);
        return (session, change);
    }

    public long NextSeq() => ++LastSeq;

    public void EnsureActive()
    {
        if (Status != SessionStatus.Active)
        {
            throw new ConflictException(RoomsErrors.SessionEnded);
        }
    }

    /// <summary>Fencing check for driver-only commands.</summary>
    public void EnsureDriver(Guid userId, long epoch)
    {
        EnsureActive();
        if (epoch != ControlEpoch)
        {
            throw new ConflictException(RoomsErrors.ControlChanged);
        }

        if (DriverUserId != userId)
        {
            throw new ForbiddenException(RoomsErrors.NotDriver);
        }
    }

    /// <summary>Takes free control.</summary>
    public ControlChange Claim(SessionActor actor)
    {
        EnsureActive();
        if (IsHeld)
        {
            throw new ConflictException(DriverUserId == actor.UserId ? RoomsErrors.AlreadyDriver : RoomsErrors.ControlNotFree);
        }

        return SetDriver(ControlChangeKind.Claim, actor, null);
    }

    /// <summary>Asks the current driver for control. Idempotent per user while pending.</summary>
    public ControlRequest RequestControl(Guid requestId, SessionActor actor, DateTime at)
    {
        EnsureActive();
        if (DriverUserId == actor.UserId)
        {
            throw new ConflictException(RoomsErrors.AlreadyDriver);
        }

        if (!IsHeld)
        {
            throw new ConflictException(RoomsErrors.ControlChanged);
        }

        var pending = _requests.FirstOrDefault(r => r.UserId == actor.UserId && r.Status == ControlRequestStatus.Pending);
        if (pending is not null)
        {
            return pending;
        }

        var request = new ControlRequest(requestId, Id, actor.UserId, actor.DisplayName, at);
        _requests.Add(request);
        return request;
    }

    /// <summary>The driver accepts or declines a request. Accepting hands control to the requester.</summary>
    public (ControlRequest Request, ControlChange? Change) ResolveRequest(Guid driverId, long epoch, Guid requestId, bool accept)
    {
        EnsureDriver(driverId, epoch);
        var request = _requests.FirstOrDefault(r => r.Id == requestId && r.Status == ControlRequestStatus.Pending)
            ?? throw new EntityNotFoundException(RoomsErrors.RequestNotFound);

        if (!accept)
        {
            request.Resolve(ControlRequestStatus.Declined);
            return (request, null);
        }

        request.Resolve(ControlRequestStatus.Accepted);
        var change = SetDriver(ControlChangeKind.RequestAccepted, new SessionActor(request.UserId, request.DisplayName), null);
        return (request, change);
    }

    /// <summary>The driver offers control to a colleague, who must accept. Replaces any earlier offer.</summary>
    public void OfferHandOff(Guid driverId, long epoch, SessionActor to, string? note)
    {
        EnsureDriver(driverId, epoch);
        if (to.UserId == driverId)
        {
            throw new CustomValidationException(RoomsErrors.HandOffToSelf);
        }

        PendingHandOffToUserId = to.UserId;
        PendingHandOffToDisplayName = to.DisplayName;
        PendingHandOffNote = note;
    }

    /// <summary>The receiver accepts or declines a hand-off offer.</summary>
    public ControlChange? ResolveHandOff(SessionActor receiver, bool accept)
    {
        EnsureActive();
        if (PendingHandOffToUserId != receiver.UserId)
        {
            throw new ConflictException(RoomsErrors.NoPendingHandOff);
        }

        var note = PendingHandOffNote;
        ClearHandOff();
        return accept ? SetDriver(ControlChangeKind.HandOff, receiver, note) : null;
    }

    /// <summary>Takes control without consent. The caller must hold <see cref="RoomsPermissions.TakeOverSession"/>.</summary>
    public ControlChange TakeOver(SessionActor actor)
    {
        EnsureActive();
        if (DriverUserId == actor.UserId)
        {
            throw new ConflictException(RoomsErrors.AlreadyDriver);
        }

        return SetDriver(ControlChangeKind.TakeOver, actor, null);
    }

    public ControlChange Release(Guid driverId, long epoch)
    {
        EnsureDriver(driverId, epoch);
        return SetDriver(ControlChangeKind.Release, null, null);
    }

    public void MarkDriverAbsent(Guid userId, DateTime at)
    {
        if (DriverUserId == userId && DriverAbsentSince is null)
        {
            DriverAbsentSince = at;
        }
    }

    public void MarkPresent(Guid userId)
    {
        if (DriverUserId == userId)
        {
            DriverAbsentSince = null;
        }
    }

    /// <summary>Frees control when the driver has been away longer than <paramref name="timeout"/>.</summary>
    public ControlChange? FreeIfDriverAbsent(DateTime now, TimeSpan timeout)
    {
        if (Status != SessionStatus.Active || DriverAbsentSince is not { } since || now - since < timeout)
        {
            return null;
        }

        return SetDriver(ControlChangeKind.Timeout, null, null);
    }

    public void BeginTurn(Guid turnId)
    {
        EnsureActive();
        if (ActiveTurnId is not null)
        {
            throw new ConflictException(RoomsErrors.TurnInProgress);
        }

        ActiveTurnId = turnId;
    }

    public void FinishTurn(Guid turnId)
    {
        if (ActiveTurnId == turnId)
        {
            ActiveTurnId = null;
        }
    }

    public void End(DateTime at)
    {
        EnsureActive();
        Status = SessionStatus.Ended;
        EndedAt = at;
        DriverUserId = null;
        DriverDisplayName = null;
        DriverAbsentSince = null;
        ClearHandOff();
        ControlEpoch++;
    }

    private ControlChange SetDriver(ControlChangeKind kind, SessionActor? to, string? note)
    {
        var from = Driver;
        DriverUserId = to?.UserId;
        DriverDisplayName = to?.DisplayName;
        DriverAbsentSince = null;
        ControlEpoch++;

        foreach (var pending in _requests.Where(r => r.Status == ControlRequestStatus.Pending && (to is null || r.UserId == to.Value.UserId)))
        {
            pending.Resolve(ControlRequestStatus.Cancelled);
        }

        if (to is not null && PendingHandOffToUserId == to.Value.UserId)
        {
            ClearHandOff();
        }
        else if (kind is ControlChangeKind.TakeOver or ControlChangeKind.Release or ControlChangeKind.Timeout)
        {
            ClearHandOff();
        }

        return new ControlChange(kind, from, to, ControlEpoch, note);
    }

    private void ClearHandOff()
    {
        PendingHandOffToUserId = null;
        PendingHandOffToDisplayName = null;
        PendingHandOffNote = null;
    }
}

public class ControlRequest : Entity<Guid>
{
    protected ControlRequest()
    {
    }

    internal ControlRequest(Guid id, Guid sessionId, Guid userId, string displayName, DateTime requestedAt)
        : base(id)
    {
        SessionId = sessionId;
        UserId = userId;
        DisplayName = displayName;
        RequestedAt = requestedAt;
        Status = ControlRequestStatus.Pending;
    }

    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public DateTime RequestedAt { get; private set; }
    public ControlRequestStatus Status { get; private set; }

    internal void Resolve(ControlRequestStatus status) => Status = status;
}
