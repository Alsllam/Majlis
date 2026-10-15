using Majlis.Framework.Domain.Entities;

namespace Majlis.Workspaces.Domain.Entities;

/// <summary>Read model of tenant group members, fed by Identity's <c>GroupMemberAdded/Removed</c>; used to expand group grants.</summary>
public class GroupMembership : Entity<Guid>, IMultiTenant
{
    protected GroupMembership()
    {
    }

    public GroupMembership(Guid tenantId, Guid groupId, string groupName, Guid userId, string displayName)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        GroupId = groupId;
        GroupName = groupName;
        UserId = userId;
        DisplayName = displayName;
    }

    public Guid TenantId { get; private set; }
    public Guid GroupId { get; private set; }
    public string GroupName { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    public void Rename(string groupName) => GroupName = groupName;
}
