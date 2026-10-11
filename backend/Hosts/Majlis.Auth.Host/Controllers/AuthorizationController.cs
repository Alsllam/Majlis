using System.Security.Claims;
using Majlis.Framework.Domain.Security;
using Majlis.Identity.Domain.Entities;
using Majlis.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Majlis.Auth.Host.Controllers;

/// <summary>
/// OpenIddict endpoints for first-party clients: authorization code + PKCE (web, mobile), refresh tokens, and
/// client credentials for internal services. First-party apps skip the consent screen.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AuthorizationController(
    UserManager<MajlisUser> users,
    SignInManager<MajlisUser> signIn,
    MajlisIdentityDbContext db,
    IOpenIddictApplicationManager applications,
    IOpenIddictScopeManager scopes) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Not an OpenID Connect request.");

        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!result.Succeeded || request.HasPromptValue(PromptValues.Login))
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Forbid(Error(Errors.LoginRequired, "The user is not signed in."), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var prompt = string.Join(' ', request.GetPromptValues().Remove(PromptValues.Login));
            var parameters = Request.HasFormContentType ? Request.Form.Where(p => p.Key != Parameters.Prompt).ToList() : Request.Query.Where(p => p.Key != Parameters.Prompt).ToList();
            parameters.Add(KeyValuePair.Create(Parameters.Prompt, new Microsoft.Extensions.Primitives.StringValues(prompt)));
            return Challenge(new AuthenticationProperties { RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parameters) }, IdentityConstants.ApplicationScheme);
        }

        var user = await users.GetUserAsync(result.Principal);
        if (user is null || !user.IsActive)
        {
            return Forbid(Error(Errors.AccessDenied, "The user account is not active."), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return SignIn(await CreateUserPrincipalAsync(user, request.GetScopes()), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Not an OpenID Connect request.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var user = await users.FindByIdAsync(result.Principal?.GetClaim(Claims.Subject) ?? string.Empty);
            if (user is null || !user.IsActive || !await signIn.CanSignInAsync(user))
            {
                return Forbid(Error(Errors.InvalidGrant, "The user can no longer sign in."), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // Rebuild the principal so role and tenant changes apply at the next refresh.
            return SignIn(await CreateUserPrincipalAsync(user, result.Principal!.GetScopes()), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            var application = await applications.FindByClientIdAsync(request.ClientId!) ?? throw new InvalidOperationException("Unknown client.");
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
            identity.SetClaim(Claims.Subject, await applications.GetClientIdAsync(application));
            identity.SetClaim(Claims.Name, await applications.GetDisplayNameAsync(application));
            identity.SetScopes(request.GetScopes());
            identity.SetResources(await ResourcesAsync(request.GetScopes()));
            identity.SetDestinations(_ => [Destinations.AccessToken]);
            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new { error = Errors.UnsupportedGrantType });
    }

    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<IActionResult> UserInfo()
    {
        var user = await users.FindByIdAsync(User.GetClaim(Claims.Subject) ?? string.Empty);
        if (user is null)
        {
            return Challenge(Error(Errors.InvalidToken, "The user no longer exists."), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Ok(new Dictionary<string, object?>
        {
            [Claims.Subject] = user.Id.ToString(),
            [Claims.Name] = user.DisplayName,
            [Claims.Email] = user.Email,
            [Claims.Locale] = user.PreferredLanguage,
            [MajlisClaimTypes.Tenant] = user.TenantId.ToString(),
            [Claims.Role] = await users.GetRolesAsync(user),
        });
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return SignOut(new AuthenticationProperties { RedirectUri = "/" }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsPrincipal> CreateUserPrincipalAsync(MajlisUser user, IEnumerable<string> requestedScopes)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == user.TenantId);
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Locale, user.PreferredLanguage)
            .SetClaim(MajlisClaimTypes.Tenant, user.TenantId.ToString())
            .SetClaim(MajlisClaimTypes.DataRegion, tenant.DataRegion)
            .SetClaims(Claims.Role, [.. await users.GetRolesAsync(user)]);

        var scopeList = requestedScopes.ToList();
        identity.SetScopes(scopeList);
        identity.SetResources(await ResourcesAsync(scopeList));
        identity.SetDestinations(DestinationsFor);
        return new ClaimsPrincipal(identity);
    }

    private async Task<List<string>> ResourcesAsync(IEnumerable<string> requestedScopes)
    {
        var resources = new List<string>();
        await foreach (var resource in scopes.ListResourcesAsync([.. requestedScopes]))
        {
            resources.Add(resource);
        }

        return resources;
    }

    /// <summary>Profile claims go to both tokens; nothing sensitive is added to tokens.</summary>
    private static IEnumerable<string> DestinationsFor(Claim claim) => claim.Type switch
    {
        Claims.Subject or Claims.Name or Claims.Role or MajlisClaimTypes.Tenant or MajlisClaimTypes.DataRegion or Claims.Locale
            => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Email => [Destinations.IdentityToken],
        _ => [Destinations.AccessToken],
    };

    private static AuthenticationProperties Error(string error, string description) => new(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
    });
}
