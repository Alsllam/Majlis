namespace Majlis.Notifications.Application;

public sealed class NotificationsOptions
{
    /// <summary>Base url of the web app, prefixed to notification links in emails.</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>Auth host base url for the internal user lookup (email address and language per recipient).</summary>
    public string IdentityUrl { get; set; } = "http://localhost:7001";

    /// <summary>A digest is never sent twice within this many hours (a repeated tick is harmless).</summary>
    public int DigestMinIntervalHours { get; set; } = 20;
}
