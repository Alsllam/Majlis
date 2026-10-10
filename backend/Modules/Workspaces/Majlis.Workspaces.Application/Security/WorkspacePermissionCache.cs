using System.Text.Json;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Repositories;
using Majlis.Workspaces.Domain.Constants;
using Majlis.Workspaces.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Majlis.Workspaces.Application.Security;

/// <summary>
/// Writes a user's effective workspace grants (the union of <see cref="WorkspaceRolePermissions"/> over their active
/// memberships) to the shared permission cache every host's <c>PermissionChecker</c> reads. Called after every
/// membership change; cheap enough to recompute fully each time.
/// </summary>
public sealed class WorkspacePermissionCache(IReadOnlyRepository<WorkspaceMember, Guid> members, IReadOnlyRepository<Workspace, Guid> workspaces, IDistributedCache cache)
{
    /// <summary>Grants live at most this long without a refresh, so a missed message heals itself.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public async Task RefreshAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roles = await members.Query()
            .Where(m => m.UserId == userId)
            .Join(workspaces.Query().Where(w => !w.IsArchived), m => m.WorkspaceId, w => w.Id, (m, _) => m.Role)
            .Distinct()
            .ToListAsync(cancellationToken);

        var grants = roles.SelectMany(WorkspaceRolePermissions.For).Distinct().Order().ToArray();
        var key = PermissionCacheKeys.ForUser(userId);
        if (grants.Length == 0)
        {
            await cache.RemoveAsync(key, cancellationToken);
            return;
        }

        await cache.SetStringAsync(key, JsonSerializer.Serialize(grants), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime }, cancellationToken);
    }

    public async Task RefreshAllAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var userIds = await members.Query().Where(m => m.WorkspaceId == workspaceId).Select(m => m.UserId).ToListAsync(cancellationToken);
        foreach (var userId in userIds)
        {
            await RefreshAsync(userId, cancellationToken);
        }
    }
}
