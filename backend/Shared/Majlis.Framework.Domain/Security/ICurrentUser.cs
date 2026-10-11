namespace Majlis.Framework.Domain.Security;

/// <summary>The caller, read from the validated token. Tenant and user never come from a request body.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? Id { get; }
    Guid? TenantId { get; }
    string? DisplayName { get; }
    IReadOnlyCollection<string> Roles { get; }

    Guid GetRequiredId();
    Guid GetRequiredTenantId();
}

/// <summary>Checks a permission name (e.g. <c>Permissions.Rooms.CreateRoom</c>) for the current user.</summary>
public interface IPermissionChecker
{
    Task<bool> IsGrantedAsync(string permission, CancellationToken cancellationToken = default);
}

public static class MajlisClaimTypes
{
    public const string Subject = "sub";
    public const string Tenant = "tenant_id";
    public const string DataRegion = "data_region";
    public const string Name = "name";
    public const string Role = "role";
}
