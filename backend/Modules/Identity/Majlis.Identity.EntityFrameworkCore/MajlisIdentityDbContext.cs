using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Localization;
using Majlis.Identity.Domain.Constants;
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
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>Creation stamps and soft deletes for the business entities (the Majlis base context is not in play here).</summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is ICreationAudited created && created.CreationTime == default)
            {
                created.CreationTime = now;
            }
            else if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete soft)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletionTime = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

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

        builder.Entity<Group>(g =>
        {
            g.ToTable("Groups");
            g.HasKey(x => x.Id);
            g.Property(x => x.Id).ValueGeneratedNever();
            g.Property(x => x.Name).HasMaxLength(IdentityFieldDefinitions.MaxGroupNameLength).IsRequired();
            g.Property(x => x.Description).HasMaxLength(IdentityFieldDefinitions.MaxDescriptionLength);
            g.HasIndex(x => new { x.TenantId, x.Name });
            g.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
            g.Navigation(x => x.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
            g.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<GroupMember>(m =>
        {
            m.ToTable("GroupMembers");
            m.HasKey(x => x.Id);
            m.Property(x => x.Id).ValueGeneratedNever();
            m.Property(x => x.DisplayName).HasMaxLength(FieldDefinitions.MaxNameLength).IsRequired();
            m.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
            m.HasIndex(x => x.UserId);
        });

        builder.Entity<Invitation>(i =>
        {
            i.ToTable("Invitations");
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).ValueGeneratedNever();
            i.Property(x => x.Email).HasMaxLength(IdentityFieldDefinitions.MaxEmailLength).IsRequired();
            i.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            i.Property(x => x.InvitedByDisplayName).HasMaxLength(FieldDefinitions.MaxNameLength).IsRequired();
            i.Property(x => x.WorkspaceName).HasMaxLength(FieldDefinitions.MaxNameLength);
            i.Property(x => x.WorkspaceRole).HasMaxLength(32);
            i.Property(x => x.Language).HasMaxLength(FieldDefinitions.LanguageCodeLength).IsRequired();
            i.Property(x => x.Status).HasConversion<int>();
            i.HasIndex(x => x.TokenHash).IsUnique();
            i.HasIndex(x => new { x.TenantId, x.Email, x.Status });
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
