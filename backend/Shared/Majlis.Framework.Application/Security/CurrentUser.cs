using System.Security.Claims;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace Majlis.Framework.Application.Security;

/// <summary>Reads the caller from the validated token on the current request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? Id => ReadGuid(MajlisClaimTypes.Subject) ?? ReadGuid(ClaimTypes.NameIdentifier);

    public Guid? TenantId => ReadGuid(MajlisClaimTypes.Tenant);

    public string? DisplayName => Principal?.FindFirst(MajlisClaimTypes.Name)?.Value ?? Principal?.Identity?.Name;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(MajlisClaimTypes.Role).Concat(Principal.FindAll(ClaimTypes.Role)).Select(c => c.Value).Distinct().ToList() ?? [];

    public Guid GetRequiredId() => Id ?? throw new ForbiddenException();

    public Guid GetRequiredTenantId() => TenantId ?? throw new ForbiddenException();

    private Guid? ReadGuid(string type) => Guid.TryParse(Principal?.FindFirst(type)?.Value, out var value) ? value : null;
}

/// <summary>Used by message consumers and jobs: an explicit system identity with an optional tenant scope.</summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;
    public Guid? Id => null;
    public Guid? TenantId => null;
    public string? DisplayName => "system";
    public IReadOnlyCollection<string> Roles => [];
    public Guid GetRequiredId() => throw new ForbiddenException();
    public Guid GetRequiredTenantId() => throw new ForbiddenException();
}
