namespace Majlis.Identity.Domain.Constants;

/// <summary>Tenant-level roles. Their permissions are configured per host in <c>RolePermissions</c>.</summary>
public static class MajlisRoles
{
    public const string TenantOwner = "TenantOwner";
    public const string TenantAdmin = "TenantAdmin";
    public const string Member = "Member";

    public static readonly string[] All = [TenantOwner, TenantAdmin, Member];
}

public static class MajlisScopes
{
    public const string Api = "majlis-api";
    public const string AiApi = "ai-api";
    public const string Internal = "majlis-internal";
}

public static class MajlisClients
{
    public const string Web = "majlis-web";
    public const string Mobile = "majlis-mobile";
    public const string Realtime = "majlis-realtime";
    public const string AiService = "majlis-ai-service";
    public const string Notifications = "majlis-notifications";
}

/// <summary>Permission names; identical strings are used by the web and mobile apps.</summary>
public static class IdentityPermissions
{
    public const string ViewUsers = "Permissions.Identity.ViewUsers";

    /// <summary>Tenant admins: create and change groups and their members.</summary>
    public const string ManageGroups = "Permissions.Identity.ManageGroups";

    /// <summary>Invite people by email (tenant admins and, through the workspace roles, owners and admins).</summary>
    public const string InviteUsers = "Permissions.Identity.InviteUsers";
}

public static class IdentityErrors
{
    public const string InvitationNotOpen = "Identity:Invitation:NotOpen";
    public const string InvitationPending = "Identity:Invitation:AlreadyPending";
    public const string GroupNameTaken = "Identity:Group:NameTaken";
    public const string UserNotInTenant = "Identity:User:NotInTenant";
    public const string RegistrationFailed = "Identity:Register:Failed";
}

public static class IdentityFieldDefinitions
{
    public const int MaxGroupNameLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
    public const int MaxDescriptionLength = Framework.Domain.Localization.FieldDefinitions.MaxDescriptionLength;
    public const int MaxEmailLength = 256;
    public const int InvitationDays = 7;
}
