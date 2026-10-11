using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace Majlis.Identity.Application.Realtime;

public sealed record RealtimeTicketDto(string Ticket, int ExpiresInSeconds);

/// <summary>Issues a WebSocket ticket for the caller. Routed by the BFF as <c>/api/realtime/ticket</c>.</summary>
[Route("realtime")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class RealtimeTicketAppService(RealtimeTicketStore store, ICurrentUser currentUser) : ApplicationService
{
    /// <summary>Returns a single-use ticket valid for 30 seconds.</summary>
    [HttpPost("ticket")]
    public async Task<RealtimeTicketDto> IssueAsync(CancellationToken cancellationToken = default)
    {
        var ticket = new RealtimeTicket(
            currentUser.GetRequiredId(),
            currentUser.GetRequiredTenantId(),
            currentUser.DisplayName ?? string.Empty,
            currentUser.Roles.ToList(),
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return new RealtimeTicketDto(await store.IssueAsync(ticket), (int)RealtimeTicketStore.Lifetime.TotalSeconds);
    }
}
