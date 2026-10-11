using FluentValidation;
using Majlis.Approvals.Application.Background;
using Majlis.Approvals.Domain.Events;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Approvals.Application;

public static class ApprovalsApplicationModule
{
    public const string ModuleName = "approvals";

    public static MessagingTopology Topology => new MessagingTopology()
        .Publish<ApprovalRequested>()
        .Publish<ApprovalDecided>()
        .Publish<ApprovalExecuted>()
        .Publish<ActionApproved>()
        .Publish<ActionRejected>()
        .Listen<ActionExecuted>()
        .Listen<ActionExecutionFailed>()
        .Listen<MemberAdded>()
        .Listen<MemberRoleChanged>()
        .Listen<MemberRemoved>();

    public static IServiceCollection AddApprovalsApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApprovalsOptions>(configuration.GetSection("Approvals"));
        services.AddValidatorsFromAssembly(typeof(ApprovalsApplicationModule).Assembly);
        return services;
    }

    public static IServiceCollection AddApprovalsBackgroundServices(this IServiceCollection services)
    {
        services.AddSingleton<ExpirySweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<ExpirySweeper>());
        return services;
    }
}
