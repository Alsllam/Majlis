using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Security;
using Majlis.Identity.Domain.Constants;
using Majlis.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;

namespace Majlis.Identity.Application.Users;

public sealed record UserLookupDto(Guid Id, string DisplayName, string Email);

public sealed record FilterUserDto : BaseFilterRequestDto;

/// <summary>Users of the caller's tenant, for member pickers. Routed by the BFF as <c>/api/identity/users/*</c>.</summary>
[Route("users")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class UsersAppService(Microsoft.AspNetCore.Identity.UserManager<MajlisUser> users, ICurrentUser currentUser) : ApplicationService
{
    /// <summary>Active users of the caller's tenant matching the filter (name or email), by display name.</summary>
    [HttpPost("lookup")]
    [HasPermission(IdentityPermissions.ViewUsers)]
    public async Task<PagedResultDto<UserLookupDto>> LookupAsync(FilterUserDto input, CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetRequiredTenantId();
        var filter = input.FilterText?.Trim();
        var query = users.Users.Where(u => u.TenantId == tenantId && u.IsActive
            && (string.IsNullOrEmpty(filter) || u.DisplayName.Contains(filter) || (u.Email != null && u.Email.Contains(filter))));

        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderBy(u => u.DisplayName)
            .Skip(input.SkipCount)
            .Take(Math.Clamp(input.MaxResultCount, 1, 100))
            .Select(u => new UserLookupDto(u.Id, u.DisplayName, u.Email ?? string.Empty))
            .ToListAsync(cancellationToken);
        return new PagedResultDto<UserLookupDto>(page, total);
    }
}
