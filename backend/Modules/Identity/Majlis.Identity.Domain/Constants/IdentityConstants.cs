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
}
