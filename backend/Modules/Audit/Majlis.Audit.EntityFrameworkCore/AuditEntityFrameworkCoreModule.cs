using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Audit.EntityFrameworkCore;

public static class AuditEntityFrameworkCoreModule
{
    public static IServiceCollection AddAuditEntityFrameworkCoreModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddMajlisDbContext<AuditDbContext>(configuration, AuditDbContext.SchemaName);
}
