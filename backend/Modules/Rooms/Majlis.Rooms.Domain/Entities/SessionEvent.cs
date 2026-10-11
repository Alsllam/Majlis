using Majlis.Framework.Domain.Entities;

namespace Majlis.Rooms.Domain.Entities;

/// <summary>
/// One entry of a session timeline. <see cref="Seq"/> is gap-free per session; clients apply events strictly by seq,
/// and late joiners replay from it. Written only by Rooms (ADR-0003).
/// </summary>
public class SessionEvent : Entity<Guid>, IMultiTenant
{
    protected SessionEvent()
    {
    }

    public SessionEvent(Guid id, Guid tenantId, Guid sessionId, long seq, string type, Guid? turnId, string actorKind, Guid? actorId, string? actorDisplayName, string dataJson, DateTime at)
        : base(id)
    {
        TenantId = tenantId;
        SessionId = sessionId;
        Seq = seq;
        Type = type;
        TurnId = turnId;
        ActorKind = actorKind;
        ActorId = actorId;
        ActorDisplayName = actorDisplayName;
        DataJson = dataJson;
        At = at;
    }

    public Guid TenantId { get; private set; }
    public Guid SessionId { get; private set; }
    public long Seq { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public Guid? TurnId { get; private set; }
    public string ActorKind { get; private set; } = string.Empty;
    public Guid? ActorId { get; private set; }
    public string? ActorDisplayName { get; private set; }
    public string DataJson { get; private set; } = "{}";
    public DateTime At { get; private set; }
}
