using Majlis.Framework.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Majlis.Rooms.EntityFrameworkCore;

/// <summary>Used only by <c>dotnet ef migrations add</c>. Migrations are applied by the DbMigrator.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RoomsDbContext>
{
    public RoomsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RoomsDbContext>()
            .UseSqlServer("Server=localhost;Database=Majlis;Trusted_Connection=False;TrustServerCertificate=True", sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", RoomsDbContext.SchemaName))
            .Options;
        return new RoomsDbContext(options, new DesignTimeUser(), TimeProvider.System);
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
