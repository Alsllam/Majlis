using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Rooms.EntityFrameworkCore;

public static class RoomsEntityFrameworkCoreModule
{
    public static IServiceCollection AddRoomsEntityFrameworkCoreModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddMajlisDbContext<RoomsDbContext>(configuration, RoomsDbContext.SchemaName);
}
