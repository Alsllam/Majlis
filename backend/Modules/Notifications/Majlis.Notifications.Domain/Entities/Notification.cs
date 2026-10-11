using Majlis.Framework.Domain.Entities;

namespace Majlis.Notifications.Domain.Entities;

/// <summary>One in-app notification for one user (FR-NTF-001). Text is rendered by the client from <see cref="Type"/> + <see cref="ParamsJson"/>.</summary>
public class Notification : Entity<Guid>, IMultiTenant
{
    protected Notification()
    {
    }

    public Notification(Guid id, Guid tenantId, Guid userId, string type, string paramsJson, string? link, string dedupeKey, DateTime createdAt)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Type = type;
        ParamsJson = paramsJson;
        Link = link;
        DedupeKey = dedupeKey;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string ParamsJson { get; private set; } = "{}";

    /// <summary>In-app route to open (e.g. <c>/rooms/{id}</c>).</summary>
    public string? Link { get; private set; }

    /// <summary>Unique per (user, source event); a redelivered message writes nothing.</summary>
    public string DedupeKey { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime? EmailSentAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public bool MarkRead(DateTime at)
    {
        if (ReadAt is not null)
        {
            return false;
        }

        ReadAt = at;
        return true;
    }

    public void MarkEmailSent(DateTime at) => EmailSentAt = at;
}
