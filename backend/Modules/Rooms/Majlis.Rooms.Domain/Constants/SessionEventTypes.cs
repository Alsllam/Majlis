namespace Majlis.Rooms.Domain.Constants;

/// <summary>Durable session event types (docs/architecture/realtime-collaboration.md §4.2).</summary>
public static class SessionEventTypes
{
    public const string SessionStarted = "session.started";
    public const string SessionEnded = "session.ended";
    public const string ControlChanged = "control.changed";
    public const string ControlRequested = "control.requested";
    public const string ControlRequestResolved = "control.request.resolved";
    public const string HandOffOffered = "handoff.offered";
    public const string HandOffResolved = "handoff.resolved";
    public const string TurnStarted = "turn.started";
    public const string TurnCompleted = "turn.completed";
    public const string TurnStopped = "turn.stopped";
    public const string TurnFailed = "turn.failed";
}

public static class ActorKinds
{
    public const string User = "user";
    public const string Agent = "agent";
    public const string System = "system";
}
