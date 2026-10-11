namespace Majlis.Rooms.Domain.Enums;

public enum RoomVisibility
{
    Private = 0,
    Open = 1,
}

public enum ParticipantRole
{
    /// <summary>Can watch, comment, suggest, drive and request control.</summary>
    Contributor = 0,

    /// <summary>Can only watch.</summary>
    Observer = 1,
}

public enum SessionStatus
{
    Active = 0,
    Ended = 1,
}

public enum TurnStatus
{
    Streaming = 0,
    Completed = 1,
    Stopped = 2,
    Failed = 3,
}

public enum ControlRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Cancelled = 3,
}

/// <summary>Why control changed, recorded in <c>control.changed</c> and the audit log.</summary>
public enum ControlChangeKind
{
    Start = 0,
    Claim = 1,
    RequestAccepted = 2,
    HandOff = 3,
    TakeOver = 4,
    Release = 5,
    Timeout = 6,
}
