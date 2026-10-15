using Majlis.Framework.Domain.Entities;

namespace Majlis.Identity.Domain.Entities;

/// <summary>A tenant-level group of users (FR-IDN-005): workspaces grant roles to groups, documents can be limited to them later.</summary>
public class Group : FullAuditedEntity<Guid>, IMultiTenant
{
    private readonly List<GroupMember> _members = [];

    protected Group()
    {
    }

    public Group(Guid id, Guid tenantId, string name, string? description)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
    }

    public Guid TenantId { get; private set; }

    /// <summary>User content: stored as written (one name, not an ar/en pair).</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public IReadOnlyCollection<GroupMember> Members => _members;

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>Returns true when the user was not a member yet.</summary>
    public bool AddMember(Guid userId, string displayName)
    {
        var existing = _members.FirstOrDefault(m => m.UserId == userId);
        if (existing is not null)
        {
            existing.Rename(displayName);
            return false;
        }

        _members.Add(new GroupMember(Guid.NewGuid(), Id, userId, displayName));
        return true;
    }

    public bool RemoveMember(Guid userId) => _members.RemoveAll(m => m.UserId == userId) > 0;
}

public class GroupMember : CreationAuditedEntity<Guid>
{
    protected GroupMember()
    {
    }

    internal GroupMember(Guid id, Guid groupId, Guid userId, string displayName)
        : base(id)
    {
        GroupId = groupId;
        UserId = userId;
        DisplayName = displayName;
    }

    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    internal void Rename(string displayName) => DisplayName = displayName;
}
