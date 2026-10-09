using Majlis.Framework.Application.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Majlis.Identity.Application;

public static class IdentityApplicationModule
{
    public static IServiceCollection AddIdentityApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is required for real-time tickets.");
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        services.AddSingleton<RealtimeTicketStore>();
        return services;
    }
}
