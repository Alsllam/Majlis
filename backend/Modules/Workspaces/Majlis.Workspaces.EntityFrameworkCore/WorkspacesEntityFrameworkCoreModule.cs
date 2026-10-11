using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Workspaces.EntityFrameworkCore;

public static class WorkspacesEntityFrameworkCoreModule
{
    public static IServiceCollection AddWorkspacesEntityFrameworkCoreModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddMajlisDbContext<WorkspacesDbContext>(configuration, WorkspacesDbContext.SchemaName);
}
