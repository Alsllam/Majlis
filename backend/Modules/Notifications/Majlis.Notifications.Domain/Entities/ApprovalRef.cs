using Majlis.Framework.Domain.Entities;

namespace Majlis.Notifications.Domain.Entities;

/// <summary>Read model of an approval request: who asked and where, so decisions and outcomes can be routed back to the requester.</summary>
public class ApprovalRef : Entity<Guid>, IMultiTenant
{
    protected ApprovalRef()
    {
    }

    public ApprovalRef(Guid requestId, Guid tenantId, Guid workspaceId, Guid roomId, Guid requesterUserId, string tool, string summary)
        : base(requestId)
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        RoomId = roomId;
        RequesterUserId = requesterUserId;
        Tool = tool;
        Summary = summary;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public string Tool { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
}
