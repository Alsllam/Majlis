using Refit;

namespace Majlis.Realtime.Host.Clients;

/// <summary>Rooms internal API, called with a client-credentials token.</summary>
public interface IRoomsInternalClient
{
    [Post("/internal/sessions/access")]
    Task<SessionAccess> GetAccessAsync([Body] SessionAccessQuery query, CancellationToken cancellationToken = default);
}

public sealed record SessionAccessQuery(Guid SessionId, Guid UserId);

public sealed record SessionAccess(bool Allowed, Guid? TenantId, Guid? WorkspaceId, Guid? RoomId, string? Role);
