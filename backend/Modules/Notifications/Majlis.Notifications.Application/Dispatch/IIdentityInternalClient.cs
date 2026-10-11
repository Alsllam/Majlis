using Refit;

namespace Majlis.Notifications.Application.Dispatch;

/// <summary>Auth host internal API, called with a client-credentials token: email address and language of recipients.</summary>
public interface IIdentityInternalClient
{
    [Post("/internal/users/lookup-by-ids")]
    Task<List<InternalUserDto>> LookupByIdsAsync([Body] InternalUsersQueryDto query, CancellationToken cancellationToken = default);
}

public sealed record InternalUsersQueryDto(Guid TenantId, List<Guid> Ids);

public sealed record InternalUserDto(Guid Id, string DisplayName, string Email, string PreferredLanguage, bool IsActive);
