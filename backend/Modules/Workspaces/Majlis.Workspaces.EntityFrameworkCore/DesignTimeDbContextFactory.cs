using Majlis.Framework.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Majlis.Workspaces.EntityFrameworkCore;

/// <summary>Used only by <c>dotnet ef migrations add</c>. Migrations are applied by the DbMigrator.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WorkspacesDbContext>
{
    public WorkspacesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseSqlServer("Server=localhost;Database=Majlis;Trusted_Connection=False;TrustServerCertificate=True", sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", WorkspacesDbContext.SchemaName))
            .Options;
        return new WorkspacesDbContext(options, new DesignTimeUser(), TimeProvider.System);
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? Id => null;
        public Guid? TenantId => null;
        public string? DisplayName => null;
        public IReadOnlyCollection<string> Roles => [];
        public Guid GetRequiredId() => Guid.Empty;
        public Guid GetRequiredTenantId() => Guid.Empty;
    }
}
