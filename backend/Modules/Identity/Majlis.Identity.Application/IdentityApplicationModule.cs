using FluentValidation;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Events;
using Majlis.Identity.Application.Invitations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Majlis.Identity.Application;

public static class IdentityApplicationModule
{
    public const string ModuleName = "identity";

    /// <summary>Groups and invitations go out for Workspaces (no outbox: the Identity context is ASP.NET Identity's).</summary>
    public static MessagingTopology Topology => new MessagingTopology()
        .Publish<GroupMemberAdded>()
        .Publish<GroupMemberRemoved>()
        .Publish<GroupRenamed>()
        .Publish<GroupDeleted>()
        .Publish<UserInvitationAccepted>();

    public static IServiceCollection AddIdentityApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is required for real-time tickets.");
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        services.AddSingleton<RealtimeTicketStore>();
        services.Configure<InvitationOptions>(configuration.GetSection("Invitations"));
        services.AddValidatorsFromAssembly(typeof(IdentityApplicationModule).Assembly);
        services.AddScoped<InvitationAcceptance>();
        return services;
    }
}
