using FluentValidation;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Domain.Events;
using Majlis.Rooms.Domain.Events;
using Majlis.Rooms.Application.Ai;
using Majlis.Rooms.Application.Background;
using Majlis.Rooms.Application.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refit;
using StackExchange.Redis;

namespace Majlis.Rooms.Application;

public static class RoomsApplicationModule
{
    public const string ModuleName = "rooms";

    /// <summary>What Rooms publishes and listens to (exchange and queue names derive from the types; ADR-0009).</summary>
    public static MessagingTopology Topology => new MessagingTopology()
        .Publish<SessionEventAppended>()
        .Publish<TurnStopRequested>()
        .Publish<AccessRevoked>()
        .Listen<TurnCompleted>()
        .Listen<TurnStopped>()
        .Listen<TurnFailed>()
        .Listen<SessionPresenceLost>()
        .Listen<SessionPresenceRestored>()
        .Listen<MemberAdded>()
        .Listen<MemberRoleChanged>()
        .Listen<MemberRemoved>();

    public static IServiceCollection AddRoomsApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RoomsOptions>(configuration.GetSection("Rooms"));
        services.Configure<AiServiceOptions>(configuration.GetSection("AiService"));

        services.AddValidatorsFromAssembly(typeof(RoomsApplicationModule).Assembly);
        services.AddScoped<SessionTimeline>();

        var ai = configuration.GetSection("AiService").Get<AiServiceOptions>() ?? new AiServiceOptions();
        services.AddRefitGeneratedClient<IAiTurnClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(ai.BaseUrl);
                c.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
            });

        var redis = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            services.TryAddSingleton<ITurnSignals, NoTurnSignals>();
        }
        else
        {
            services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
            services.TryAddSingleton<ITurnSignals, RedisTurnSignals>();
        }

        return services;
    }

    /// <summary>The sweeper runs in the Rooms host (moves to the Jobs host later).</summary>
    public static IServiceCollection AddRoomsBackgroundServices(this IServiceCollection services)
    {
        services.AddSingleton<SessionSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<SessionSweeper>());
        return services;
    }
}
