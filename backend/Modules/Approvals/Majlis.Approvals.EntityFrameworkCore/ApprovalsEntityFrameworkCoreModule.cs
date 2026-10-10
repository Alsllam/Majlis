using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Approvals.EntityFrameworkCore;

public static class ApprovalsEntityFrameworkCoreModule
{
    public static IServiceCollection AddApprovalsEntityFrameworkCoreModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddMajlisDbContext<ApprovalsDbContext>(configuration, ApprovalsDbContext.SchemaName);
}
