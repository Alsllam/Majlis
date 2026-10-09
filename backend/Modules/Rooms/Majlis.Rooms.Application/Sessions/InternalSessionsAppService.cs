using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Application.Sessions.DTOs;
using Majlis.Rooms.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Rooms.Application.Sessions;

/// <summary>Service-to-service endpoints (client-credentials tokens with the internal scope only).</summary>
[Route("internal/sessions")]
[Authorize(Policy = InternalServicePolicy.Name)]
public class InternalSessionsAppService(IReadOnlyRepository<AgentSession, Guid> sessions, IReadOnlyRepository<Room, Guid> rooms) : ApplicationService
{
    /// <summary>The Realtime host asks before adding a connection to <c>session:{id}</c>.</summary>
    [HttpPost("access")]
    public async Task<SessionAccessDto> GetAccessAsync(SessionAccessQueryDto input, CancellationToken cancellationToken = default)
    {
        var session = await sessions.Query().FirstOrDefaultAsync(s => s.Id == input.SessionId, cancellationToken);
        if (session is null)
        {
            return new SessionAccessDto(false, null, null, null, null);
        }

        var participant = await rooms.Query()
            .Where(r => r.Id == session.RoomId)
            .SelectMany(r => r.Participants)
            .FirstOrDefaultAsync(p => p.UserId == input.UserId, cancellationToken);

        return new SessionAccessDto(participant is not null, session.TenantId, session.WorkspaceId, session.RoomId, participant?.Role);
    }
}
