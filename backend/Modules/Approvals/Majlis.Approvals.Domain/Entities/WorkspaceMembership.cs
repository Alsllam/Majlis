using Majlis.Framework.Domain.Entities;

namespace Majlis.Approvals.Domain.Entities;

/// <summary>Read model of workspace membership fed by the Workspaces member events (see Rooms for the same pattern).</summary>
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
    public string Role { get; private set; } = string.Empty;

    /// <summary>Viewers read; everyone else may upload.</summary>
    public bool CanContribute => Role != "Viewer";

    /// <summary>Owners and admins may delete anyone's documents.</summary>
    public bool CanManage => Role is "Owner" or "Admin";

    public void Update(string displayName, string role)
    {
        DisplayName = displayName;
        Role = role;
    }
}
