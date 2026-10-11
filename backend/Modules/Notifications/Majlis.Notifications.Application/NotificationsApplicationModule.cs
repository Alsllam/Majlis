using Majlis.Framework.Application.Email;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Events;
using Majlis.Notifications.Application.Dispatch;
using Majlis.Notifications.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Notifications.Application;

public static class NotificationsApplicationModule
{
    public const string ModuleName = "notifications";

    public static MessagingTopology Topology => new MessagingTopology()
        .Publish<NotificationCreated>()
        .Listen<ApprovalRequested>()
        .Listen<ApprovalDecided>()
        .Listen<ApprovalExecuted>()
        .Listen<TaskAssigned>()
        .Listen<SessionEventAppended>()
        .Listen<MemberAdded>()
        .Listen<MemberRoleChanged>()
        .Listen<MemberRemoved>()
        .Listen<DailyDigestRequested>();

    public static IServiceCollection AddNotificationsApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationsOptions>(configuration.GetSection("Notifications"));
        services.AddMajlisEmail(configuration);
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<DigestSender>();

        services.Configure<InternalAuthOptions>(configuration.GetSection("InternalAuth"));
        services.AddSingleton<IRecipientLookup, IdentityRecipientLookup>();
        return services;
    }
}
