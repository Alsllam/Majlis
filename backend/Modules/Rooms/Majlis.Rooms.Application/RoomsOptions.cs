namespace Majlis.Rooms.Application;

public sealed class RoomsOptions
{
    /// <summary>Driver-absence timeout. Tenant setting later (FR-SES-010): default 2 min, range 30 s – 30 min.</summary>
    public int DriverAbsenceTimeoutSeconds { get; set; } = 120;

    /// <summary>How often the sweeper checks absent drivers and stuck turns.</summary>
    public int SweepIntervalSeconds { get; set; } = 5;

    /// <summary>A streaming turn with no ai-service heartbeat for this long is failed.</summary>
    public int TurnHeartbeatTimeoutSeconds { get; set; } = 30;

    /// <summary>Events returned with the session on open.</summary>
    public int InitialEventCount { get; set; } = 200;
}

public sealed class AiServiceOptions
{
    /// <summary>Internal base URL of ai-service (not through the BFF).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8000";

    public int TimeoutSeconds { get; set; } = 10;
}
