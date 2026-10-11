namespace Majlis.Audit.Domain.Constants;

/// <summary>Permission names; identical strings are used by the web and mobile apps. Granted to tenant admins only.</summary>
public static class AuditPermissions
{
    public const string ViewAudit = "Permissions.Audit.ViewAudit";
    public const string ExportAudit = "Permissions.Audit.ExportAudit";
}

/// <summary>What an entry is about. The web client filters and labels by it.</summary>
public static class AuditCategories
{
    public const string Session = "session";
    public const string Approval = "approval";
    public const string Tool = "tool";
    public const string Document = "document";
    public const string Membership = "membership";
    public const string Task = "task";

    public static readonly IReadOnlyList<string> All = [Session, Approval, Tool, Document, Membership, Task];
}

public static class AuditErrors
{
    public const string ExportTooLarge = "Audit:Export:TooLarge";
}

public static class AuditFieldDefinitions
{
    public const int MaxCategoryLength = 32;
    public const int MaxActionLength = 64;
    public const int MaxActorKindLength = 16;
    public const int MaxEntityTypeLength = 64;
    public const int MaxDetailsLength = 4000;
    public const int MaxSourceKeyLength = 200;
    public const int HashLength = 64;
}
