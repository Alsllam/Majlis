using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;

namespace Majlis.Identity.Application.Users;

public sealed record InternalUsersQueryDto(Guid TenantId, List<Guid> Ids);

public sealed record InternalUserDto(Guid Id, string DisplayName, string Email, string PreferredLanguage, bool IsActive);

/// <summary>Service-to-service lookups (client-credentials tokens with the internal scope): who to email, in which language.</summary>
[Route("internal/users")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Policy = InternalServicePolicy.Name)]
public class InternalUsersAppService(Microsoft.AspNetCore.Identity.UserManager<MajlisUser> users) : ApplicationService
{
    [HttpPost("lookup-by-ids")]
    public async Task<List<InternalUserDto>> LookupByIdsAsync(InternalUsersQueryDto input, CancellationToken cancellationToken = default)
    {
        var ids = input.Ids.Distinct().Take(200).ToList();
        return await users.Users
            .Where(u => u.TenantId == input.TenantId && ids.Contains(u.Id))
            .Select(u => new InternalUserDto(u.Id, u.DisplayName, u.Email ?? string.Empty, u.PreferredLanguage, u.IsActive))
            .ToListAsync(cancellationToken);
    }
}
