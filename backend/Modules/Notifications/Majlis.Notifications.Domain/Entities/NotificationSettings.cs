using Majlis.Framework.Domain.Entities;
using Majlis.Notifications.Domain.Constants;

namespace Majlis.Notifications.Domain.Entities;

/// <summary>Per-user preferences (FR-NTF-002): email on/off and muted types. Created on first use with defaults.</summary>
public class NotificationSettings : Entity<Guid>, IMultiTenant
{
    protected NotificationSettings()
    {
    }

    public NotificationSettings(Guid tenantId, Guid userId)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        UserId = userId;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public bool EmailEnabled { get; private set; } = true;

    /// <summary>Comma-separated notification types the user does not want at all.</summary>
    public string MutedTypes { get; private set; } = string.Empty;

    public IReadOnlyList<string> MutedTypeList => MutedTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsMuted(string type) => MutedTypeList.Contains(type, StringComparer.Ordinal);

    public void Update(bool emailEnabled, IEnumerable<string> mutedTypes)
    {
        EmailEnabled = emailEnabled;
        MutedTypes = string.Join(',', mutedTypes.Where(t => NotificationTypes.All.Contains(t)).Distinct(StringComparer.Ordinal));
    }
}
