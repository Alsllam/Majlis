using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Notifications.EntityFrameworkCore;

public static class NotificationsEntityFrameworkCoreModule
{
    public static IServiceCollection AddNotificationsEntityFrameworkCoreModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddMajlisDbContext<NotificationsDbContext>(configuration, NotificationsDbContext.SchemaName);
}
