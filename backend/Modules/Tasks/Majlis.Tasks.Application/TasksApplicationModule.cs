using FluentValidation;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Domain.Events;
using Majlis.Tasks.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Tasks.Application;

public static class TasksApplicationModule
{
    public const string ModuleName = "tasks";

    public static MessagingTopology Topology => new MessagingTopology()
        .Publish<ActionExecuted>()
        .Publish<ActionExecutionFailed>()
        .Listen<ActionApproved>()
        .Listen<MemberAdded>()
        .Listen<MemberRoleChanged>()
        .Listen<MemberRemoved>();

    public static IServiceCollection AddTasksApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddValidatorsFromAssembly(typeof(TasksApplicationModule).Assembly);
        return services;
    }
}
