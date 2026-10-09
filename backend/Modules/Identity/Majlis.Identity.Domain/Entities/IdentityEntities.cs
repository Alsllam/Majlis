using Majlis.Framework.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Majlis.Identity.Domain.Entities;

/// <summary>One customer organization. Owns users and workspaces; lives in one data region.</summary>
public class Tenant : FullAuditedEntity<Guid>, IActivableEntity
{
    protected Tenant()
    {
    }

    public Tenant(Guid id, string nameAr, string nameEn, string dataRegion)
        : base(id)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        DataRegion = dataRegion;
        IsActive = true;
    }

    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;

    /// <summary>Data region chosen at provisioning (SRS §6.2), e.g. <c>uaenorth</c>. Read-only for tenant admins.</summary>
    public string DataRegion { get; private set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class MajlisUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    /// <summary><c>ar</c> or <c>en</c>.</summary>
    public string PreferredLanguage { get; set; } = "ar";

    public bool IsActive { get; set; } = true;
}

public class MajlisRole : IdentityRole<Guid>
{
    public MajlisRole()
    {
    }

    public MajlisRole(string name)
        : base(name)
    {
    }
}
