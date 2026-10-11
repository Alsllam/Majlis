using Majlis.Framework.Domain.Events;

namespace Majlis.Rooms.Domain.Events;

// Consumer-side copies of the Workspaces member events (same alias and JSON shape; the role travels as its name).
// Modules never reference each other's assemblies, so each consumer declares the contract it reads.

public sealed record MemberAdded(Guid TenantId, Guid WorkspaceId, Guid UserId, string DisplayName, string Role) : IEvent;

public sealed record MemberRoleChanged(Guid TenantId, Guid WorkspaceId, Guid UserId, string DisplayName, string Role) : IEvent;

public sealed record MemberRemoved(Guid TenantId, Guid WorkspaceId, Guid UserId) : IEvent;
