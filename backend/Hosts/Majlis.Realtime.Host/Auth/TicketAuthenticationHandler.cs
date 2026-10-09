using System.Security.Claims;
using System.Text.Encodings.Web;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Majlis.Realtime.Host.Auth;

/// <summary>
/// Authenticates the hub connection with a short-lived ticket (<c>?ticket=</c>) issued by the Auth host.
/// Access tokens never travel in URLs (docs/architecture/realtime-collaboration.md §7).
/// </summary>
public sealed class TicketAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, RealtimeTicketStore tickets)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "RealtimeTicket";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = Request.Query["ticket"].ToString();
        if (string.IsNullOrEmpty(value))
        {
            return AuthenticateResult.NoResult();
        }

        var ticket = await tickets.ValidateAsync(value, Context.Connection.RemoteIpAddress?.ToString());
        if (ticket is null)
        {
            return AuthenticateResult.Fail("Invalid or expired ticket.");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(ToPrincipal(ticket), SchemeName));
    }

    public static ClaimsPrincipal ToPrincipal(RealtimeTicket ticket)
    {
        var identity = new ClaimsIdentity(SchemeName, MajlisClaimTypes.Name, MajlisClaimTypes.Role);
        identity.AddClaim(new Claim(MajlisClaimTypes.Subject, ticket.UserId.ToString()));
        identity.AddClaim(new Claim(MajlisClaimTypes.Tenant, ticket.TenantId.ToString()));
        identity.AddClaim(new Claim(MajlisClaimTypes.Name, ticket.DisplayName));
        foreach (var role in ticket.Roles)
        {
            identity.AddClaim(new Claim(MajlisClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(identity);
    }
}
