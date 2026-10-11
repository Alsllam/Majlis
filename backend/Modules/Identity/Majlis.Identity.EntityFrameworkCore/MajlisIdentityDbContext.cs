using Majlis.Framework.Domain.Localization;
using Majlis.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Majlis.Identity.EntityFrameworkCore;

/// <summary>
/// Identity, tenants and the OpenIddict stores, in the <c>identity</c> schema. It derives from IdentityDbContext
/// (not MajlisDbContext) because ASP.NET Core Identity owns its base class; it holds no tenant-filtered business data.
/// </summary>
public class MajlisIdentityDbContext(DbContextOptions<MajlisIdentityDbContext> options)
    : IdentityDbContext<MajlisUser, MajlisRole, Guid>(options)
{
    public const string SchemaName = "identity";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(SchemaName);
        builder.UseOpenIddict<Guid>();

        builder.Entity<Tenant>(t =>
        {
            t.ToTable("Tenants");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).ValueGeneratedNever();
            t.Property(x => x.NameAr).HasMaxLength(FieldDefinitions.MaxNameLength).IsRequired();
            t.Property(x => x.NameEn).HasMaxLength(FieldDefinitions.MaxNameLength).IsRequired();
            t.Property(x => x.DataRegion).HasMaxLength(32).IsRequired();
            t.Property(x => x.IsActive).HasDefaultValue(true);
            t.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<MajlisUser>(u =>
        {
            u.Property(x => x.DisplayName).HasMaxLength(FieldDefinitions.MaxNameLength).IsRequired();
            u.Property(x => x.PreferredLanguage).HasMaxLength(FieldDefinitions.LanguageCodeLength).IsRequired();
            u.HasIndex(x => x.TenantId);
        });
    }
}

/// <summary>Used only by <c>dotnet ef migrations add</c>.</summary>
public sealed class DesignTimeIdentityDbContextFactory : IDesignTimeDbContextFactory<MajlisIdentityDbContext>
{
    public MajlisIdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MajlisIdentityDbContext>();
        options.UseSqlServer("Server=localhost;Database=Majlis;TrustServerCertificate=True", sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", MajlisIdentityDbContext.SchemaName));
        options.UseOpenIddict<Guid>();
        return new MajlisIdentityDbContext(options.Options);
    }
}
