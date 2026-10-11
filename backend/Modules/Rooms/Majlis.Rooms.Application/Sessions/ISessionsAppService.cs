using Majlis.Rooms.Application.Sessions.DTOs;

namespace Majlis.Rooms.Application.Sessions;

/// <summary>
/// Commands and reads for a shared agent session. Every state change is an HTTP command here (ADR-0002);
/// the Realtime host only pushes the resulting events. Driver-only commands carry the control epoch (ADR-0004).
/// </summary>
public interface ISessionsAppService
{
    /// <summary>Starts a session in a room; the caller becomes the driver.</summary>
    Task<SessionStateDto> StartAsync(StartSessionDto input, CancellationToken cancellationToken = default);

    /// <summary>Session state plus the latest events and <c>lastSeq</c>, for opening the room screen.</summary>
    Task<SessionDto> GetAsync(SessionIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Events after or before a seq (gap fetch, reconnect, older history).</summary>
    Task<IReadOnlyList<SessionEventDto>> GetEventsAsync(SessionEventsFilterDto input, CancellationToken cancellationToken = default);

    /// <summary>The driver instructs the agent. Idempotent on <c>clientRequestId</c>.</summary>
    Task<InstructResultDto> InstructAsync(InstructSessionDto input, CancellationToken cancellationToken = default);

    /// <summary>The driver stops the running turn.</summary>
    Task StopAsync(StopTurnDto input, CancellationToken cancellationToken = default);

    /// <summary>Takes control when it is free.</summary>
    Task<SessionStateDto> ClaimControlAsync(SessionIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Asks the driver for control.</summary>
    Task<SessionStateDto> RequestControlAsync(SessionIdDto input, CancellationToken cancellationToken = default);

    /// <summary>The driver accepts or declines a control request.</summary>
    Task<SessionStateDto> ResolveControlRequestAsync(ResolveControlRequestDto input, CancellationToken cancellationToken = default);

    /// <summary>The driver offers control to a colleague.</summary>
    Task<SessionStateDto> OfferHandOffAsync(OfferHandOffDto input, CancellationToken cancellationToken = default);

    /// <summary>The receiver accepts or declines a hand-off.</summary>
    Task<SessionStateDto> ResolveHandOffAsync(ResolveHandOffDto input, CancellationToken cancellationToken = default);

    /// <summary>Takes control without consent (needs the take-over permission).</summary>
    Task<SessionStateDto> TakeOverAsync(SessionIdDto input, CancellationToken cancellationToken = default);

    /// <summary>The driver gives control up.</summary>
    Task<SessionStateDto> ReleaseControlAsync(SessionEpochDto input, CancellationToken cancellationToken = default);

    /// <summary>The driver ends the session.</summary>
    Task<SessionStateDto> EndAsync(SessionEpochDto input, CancellationToken cancellationToken = default);
}
