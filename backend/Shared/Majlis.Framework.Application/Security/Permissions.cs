using Majlis.Framework.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Majlis.Framework.Application.Security;

/// <summary>Requires a permission such as <c>Permissions.Rooms.CreateRoom</c> (constants live in each module).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PolicyPrefix + permission)
{
    public const string PolicyPrefix = "Permission:";

    public string Permission { get; } = permission;
}

/// <summary>Tenant-level role → permissions. Workspace-level grants come from the cache written by the Workspaces module.</summary>
public sealed class RolePermissionOptions
{
    public Dictionary<string, string[]> Roles { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class PermissionCacheKeys
{
    /// <summary>Effective workspace grants for a user, written by Workspaces (JSON array of permission names).</summary>
    public static string ForUser(Guid userId) => $"majlis:permissions:{userId:N}";
}

/// <summary>Union of the caller's role permissions and cached workspace grants.</summary>
public sealed class PermissionChecker(ICurrentUser currentUser, IOptions<RolePermissionOptions> roles, IDistributedCache cache) : IPermissionChecker
{
    public async Task<bool> IsGrantedAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.Id is not { } userId)
        {
            return false;
        }

        foreach (var role in currentUser.Roles)
        {
            if (roles.Value.Roles.TryGetValue(role, out var granted) && (granted.Contains("*") || granted.Contains(permission)))
            {
                return true;
            }
        }

        var cached = await cache.GetStringAsync(PermissionCacheKeys.ForUser(userId), cancellationToken);
        return cached is not null && System.Text.Json.JsonSerializer.Deserialize<string[]>(cached)?.Contains(permission) == true;
    }
}

internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

internal sealed class PermissionHandler(IPermissionChecker checker) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await checker.IsGrantedAsync(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>Builds <c>Permission:*</c> policies on demand.</summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[HasPermissionAttribute.PolicyPrefix.Length..]))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

/// <summary>Policy for service-to-service calls: a client-credentials token carrying the internal scope.</summary>
public static class InternalServicePolicy
{
    public const string Name = "InternalService";
    public const string Scope = "majlis-internal";
}
