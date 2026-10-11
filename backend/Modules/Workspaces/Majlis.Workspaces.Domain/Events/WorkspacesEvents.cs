using Majlis.Framework.Domain.Events;

namespace Majlis.Workspaces.Domain.Events;

// Membership events are consumed by Rooms (membership read model) and later by Knowledge and Approvals.
// Their JSON shapes are in docs/architecture/events.schema.json. The role travels as its name (consumers have no enum).

public sealed record WorkspaceCreated(Guid TenantId, Guid WorkspaceId, string Name, Guid CreatedBy) : IEvent;

public sealed record WorkspaceArchived(Guid TenantId, Guid WorkspaceId, bool IsArchived) : IEvent;

public sealed record WorkspaceInstructionsChanged(Guid TenantId, Guid WorkspaceId, string? AgentInstructions) : IEvent;

public sealed record MemberAdded(Guid TenantId, Guid WorkspaceId, Guid UserId, string DisplayName, string Role) : IEvent;

public sealed record MemberRoleChanged(Guid TenantId, Guid WorkspaceId, Guid UserId, string DisplayName, string Role) : IEvent;

public sealed record MemberRemoved(Guid TenantId, Guid WorkspaceId, Guid UserId) : IEvent;
