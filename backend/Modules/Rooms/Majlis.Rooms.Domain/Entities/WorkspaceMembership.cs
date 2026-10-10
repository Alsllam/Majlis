using Majlis.Framework.Domain.Entities;

namespace Majlis.Rooms.Domain.Entities;

/// <summary>
/// Read model of workspace membership, kept up to date from the Workspaces module's member events. Rooms checks it
/// before creating rooms or adding participants, so it never calls Workspaces on the hot path (backend-modules.md).
/// </summary>
public class WorkspaceMembership : Entity<Guid>, IMultiTenant
{
    protected WorkspaceMembership()
    {
    }

    public WorkspaceMembership(Guid tenantId, Guid workspaceId, Guid userId, string displayName, string role)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        UserId = userId;
        DisplayName = displayName;
        Role = role;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Workspace role name as published by Workspaces (Owner, Admin, Contributor, Viewer).</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Viewers watch only; everyone else may create rooms and drive.</summary>
    public bool CanContribute => Role != "Viewer";

    public void Update(string displayName, string role)
    {
        DisplayName = displayName;
        Role = role;
    }
}
