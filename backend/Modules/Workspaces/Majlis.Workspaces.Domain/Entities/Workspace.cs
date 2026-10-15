using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Workspaces.Domain.Constants;
using Majlis.Workspaces.Domain.Enums;

namespace Majlis.Workspaces.Domain.Entities;

/// <summary>A team area inside a tenant (SRS §2: "Legal Affairs"); owns rooms, documents and members.</summary>
public class Workspace : FullAuditedEntity<Guid>, IMultiTenant
{
    private readonly List<WorkspaceMember> _members = [];
    private readonly List<WorkspaceGroupGrant> _groups = [];

    protected Workspace()
    {
    }

    public Workspace(Guid id, Guid tenantId, string name, string? description, string? icon, string? color)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        Icon = icon;
        Color = color;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>A short emoji or icon name shown on the workspace card.</summary>
    public string? Icon { get; private set; }

    /// <summary>A brand token name (<see cref="WorkspacesFieldDefinitions.Colors"/>), never a raw hex value.</summary>
    public string? Color { get; private set; }

    /// <summary>House style, tone and glossary notes added to every session's system context (FR-WSP-006).</summary>
    public string? AgentInstructions { get; private set; }

    /// <summary>Archived workspaces are read-only (FR-WSP-005).</summary>
    public bool IsArchived { get; private set; }

    public IReadOnlyCollection<WorkspaceMember> Members => _members;

    /// <summary>Tenant groups granted a role here (FR-WSP-002); their members are expanded into <see cref="Members"/>.</summary>
    public IReadOnlyCollection<WorkspaceGroupGrant> Groups => _groups;

    public void Update(string name, string? description, string? icon, string? color)
    {
        EnsureNotArchived();
        Name = name;
        Description = description;
        Icon = icon;
        Color = color;
    }

    public void SetAgentInstructions(string? instructions)
    {
        EnsureNotArchived();
        AgentInstructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
    }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    public WorkspaceMember? FindMember(Guid userId) => _members.FirstOrDefault(m => m.UserId == userId);

    /// <summary>Throws unless the user is a member; returns the membership.</summary>
    public WorkspaceMember EnsureMember(Guid userId)
        => FindMember(userId) ?? throw new ForbiddenException(WorkspacesErrors.NotMember);

    /// <summary>Throws unless the user is an owner or admin.</summary>
    public WorkspaceMember EnsureManager(Guid userId)
    {
        var member = EnsureMember(userId);
        if (!WorkspaceRolePermissions.CanManage(member.Role))
        {
            throw new ForbiddenException(WorkspacesErrors.NotManager);
        }

        return member;
    }

    /// <summary>Adds a member or changes the role of an existing one. Returns true when the member is new.</summary>
    public bool AddOrChangeMember(Guid actorId, Guid userId, string displayName, WorkspaceRole role)
    {
        EnsureNotArchived();
        var actor = EnsureManager(actorId);
        if (role == WorkspaceRole.Owner && actor.Role != WorkspaceRole.Owner)
        {
            throw new ForbiddenException(WorkspacesErrors.OnlyOwnerChangesOwner);
        }

        var existing = FindMember(userId);
        if (existing is null)
        {
            _members.Add(new WorkspaceMember(Guid.NewGuid(), Id, userId, displayName, role));
            return true;
        }

        if (existing.ViaGroupId is not null)
        {
            // A direct membership replaces the one the group gave.
            existing.MakeDirect();
        }

        if (existing.Role == WorkspaceRole.Owner && role != WorkspaceRole.Owner)
        {
            if (actor.Role != WorkspaceRole.Owner)
            {
                throw new ForbiddenException(WorkspacesErrors.OnlyOwnerChangesOwner);
            }

            EnsureNotLastOwner(userId);
        }

        existing.Change(role, displayName);
        return false;
    }

    public void RemoveMember(Guid actorId, Guid userId)
    {
        EnsureNotArchived();
        var actor = EnsureManager(actorId);
        var member = FindMember(userId) ?? throw new EntityNotFoundException(WorkspacesErrors.MemberNotFound);
        if (member.Role == WorkspaceRole.Owner)
        {
            if (actor.Role != WorkspaceRole.Owner)
            {
                throw new ForbiddenException(WorkspacesErrors.OnlyOwnerChangesOwner);
            }

            EnsureNotLastOwner(userId);
        }

        _members.Remove(member);
    }

    /// <summary>The creator becomes the first owner (no actor check: the workspace has no members yet).</summary>
    public void AddFirstOwner(Guid userId, string displayName)
    {
        if (_members.Count > 0)
        {
            throw new InvalidOperationException("The workspace already has members.");
        }

        _members.Add(new WorkspaceMember(Guid.NewGuid(), Id, userId, displayName, WorkspaceRole.Owner));
    }

    /// <summary>
    /// Grants a group a role (owner is never granted to a group) and adds its members who are not members yet.
    /// Returns the users added. Members the group already brought in follow the new role.
    /// </summary>
    public IReadOnlyList<WorkspaceMember> GrantGroup(Guid actorId, Guid groupId, string groupName, WorkspaceRole role, IEnumerable<(Guid UserId, string DisplayName)> groupMembers)
    {
        EnsureNotArchived();
        EnsureManager(actorId);
        if (role == WorkspaceRole.Owner)
        {
            throw new CustomValidationException(WorkspacesErrors.GroupCannotOwn);
        }

        var grant = _groups.FirstOrDefault(g => g.GroupId == groupId);
        if (grant is null)
        {
            _groups.Add(new WorkspaceGroupGrant(Guid.NewGuid(), Id, groupId, groupName, role));
        }
        else
        {
            grant.Change(groupName, role);
            foreach (var member in _members.Where(m => m.ViaGroupId == groupId))
            {
                member.Change(role, member.DisplayName);
            }
        }

        return SyncGroupMembers(groupId, groupMembers);
    }

    /// <summary>Adds the group's members who are absent, with the grant's role. Returns the users added.</summary>
    public IReadOnlyList<WorkspaceMember> SyncGroupMembers(Guid groupId, IEnumerable<(Guid UserId, string DisplayName)> groupMembers)
    {
        var grant = _groups.FirstOrDefault(g => g.GroupId == groupId);
        if (grant is null || IsArchived)
        {
            return [];
        }

        var added = new List<WorkspaceMember>();
        foreach (var (userId, displayName) in groupMembers)
        {
            if (FindMember(userId) is not null)
            {
                continue;
            }

            var member = new WorkspaceMember(Guid.NewGuid(), Id, userId, displayName, grant.Role) { ViaGroupId = groupId };
            _members.Add(member);
            added.Add(member);
        }

        return added;
    }

    /// <summary>Removes the grant and the members it brought in (direct members stay). Returns the users removed.</summary>
    public IReadOnlyList<Guid> RevokeGroup(Guid? actorId, Guid groupId)
    {
        if (actorId is { } actor)
        {
            EnsureNotArchived();
            EnsureManager(actor);
        }

        var grant = _groups.FirstOrDefault(g => g.GroupId == groupId);
        if (grant is null)
        {
            return [];
        }

        _groups.Remove(grant);
        return RemoveViaGroup(groupId, null);
    }

    /// <summary>A user left the group: drop the membership the group gave (a direct membership stays).</summary>
    public IReadOnlyList<Guid> RemoveGroupMember(Guid groupId, Guid userId) => RemoveViaGroup(groupId, userId);

    public void RenameGroup(Guid groupId, string groupName) => _groups.FirstOrDefault(g => g.GroupId == groupId)?.Change(groupName, null);

    private List<Guid> RemoveViaGroup(Guid groupId, Guid? userId)
    {
        var gone = _members.Where(m => m.ViaGroupId == groupId && (userId is null || m.UserId == userId)).ToList();
        _members.RemoveAll(gone.Contains);
        return gone.Select(m => m.UserId).ToList();
    }

    private void EnsureNotLastOwner(Guid userId)
    {
        if (!_members.Any(m => m.Role == WorkspaceRole.Owner && m.UserId != userId))
        {
            throw new CustomValidationException(WorkspacesErrors.LastOwner);
        }
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new CustomValidationException(WorkspacesErrors.Archived);
        }
    }
}

public class WorkspaceMember : CreationAuditedEntity<Guid>
{
    protected WorkspaceMember()
    {
    }

    internal WorkspaceMember(Guid id, Guid workspaceId, Guid userId, string displayName, WorkspaceRole role)
        : base(id)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        DisplayName = displayName;
        Role = role;
    }

    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Snapshot of the user's name; refreshed whenever the membership is updated.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public WorkspaceRole Role { get; private set; }

    /// <summary>Set when the membership comes from a group grant; null for a direct member.</summary>
    public Guid? ViaGroupId { get; internal set; }

    internal void Change(WorkspaceRole role, string displayName)
    {
        Role = role;
        DisplayName = displayName;
    }

    internal void MakeDirect() => ViaGroupId = null;
}

/// <summary>A tenant group with a role in the workspace; the group name is a snapshot refreshed on rename.</summary>
public class WorkspaceGroupGrant : CreationAuditedEntity<Guid>
{
    protected WorkspaceGroupGrant()
    {
    }

    internal WorkspaceGroupGrant(Guid id, Guid workspaceId, Guid groupId, string groupName, WorkspaceRole role)
        : base(id)
    {
        WorkspaceId = workspaceId;
        GroupId = groupId;
        GroupName = groupName;
        Role = role;
    }

    public Guid WorkspaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public string GroupName { get; private set; } = string.Empty;
    public WorkspaceRole Role { get; private set; }

    internal void Change(string groupName, WorkspaceRole? role)
    {
        GroupName = groupName;
        Role = role ?? Role;
    }
}
