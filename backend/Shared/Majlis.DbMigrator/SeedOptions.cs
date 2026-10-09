namespace Majlis.DbMigrator;

/// <summary>Development seed. Secrets (passwords, client secrets) come from the environment only.</summary>
public sealed class SeedOptions
{
    public Guid TenantId { get; set; } = Guid.Parse("8f2b6c3e-1d4a-4f6b-9a2e-5c7d8e9f0a1b");
    public string TenantNameAr { get; set; } = "جهة تجريبية";
    public string TenantNameEn { get; set; } = "Demo organization";
    public string DataRegion { get; set; } = "local";
    public Guid DemoWorkspaceId { get; set; } = Guid.Parse("3c9e1f2a-7b4d-4e8a-b6c1-0d2e3f4a5b6c");

    /// <summary>Password for every seeded user. Users are not created when empty.</summary>
    public string? Password { get; set; }

    public List<SeedUser> Users { get; set; } = [];

    public string[] WebRedirectUris { get; set; } = [];

    /// <summary>Client secret of the Realtime host (client credentials). The client is not created when empty.</summary>
    public string? RealtimeClientSecret { get; set; }

    /// <summary>Client secret of ai-service (client credentials). The client is not created when empty.</summary>
    public string? AiServiceClientSecret { get; set; }
}

public sealed class SeedUser
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Language { get; set; } = "ar";
    public string[] Roles { get; set; } = [];
}
