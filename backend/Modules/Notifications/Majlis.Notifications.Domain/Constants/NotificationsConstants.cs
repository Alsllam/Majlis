namespace Majlis.Notifications.Domain.Constants;

/// <summary>Permission names; identical strings are used by the web and mobile apps.</summary>
public static class NotificationsPermissions
{
    public const string ViewNotification = "Permissions.Notifications.ViewNotification";
}

/// <summary>Notification types (FR-NTF-001). The web client renders the text from the type and the params, in its language.</summary>
public static class NotificationTypes
{
    public const string ApprovalRequested = "approval.requested";
    public const string ApprovalDecided = "approval.decided";
    public const string ApprovalExecuted = "approval.executed";
    public const string TaskAssigned = "task.assigned";
    public const string HandOffOffered = "handoff.offered";

    public static readonly IReadOnlyList<string> All = [ApprovalRequested, ApprovalDecided, ApprovalExecuted, TaskAssigned, HandOffOffered];
}

public static class NotificationsFieldDefinitions
{
    public const int MaxTypeLength = 64;
    public const int MaxLinkLength = 512;
    public const int MaxParamsLength = 4000;
    public const int MaxDedupeKeyLength = 200;
    public const int MaxMutedTypesLength = 1000;
}
