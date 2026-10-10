using FluentValidation;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Domain.Events;
using Majlis.Workspaces.Application.Security;
using Majlis.Workspaces.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Workspaces.Application;

public static class WorkspacesApplicationModule
{
    public const string ModuleName = "workspaces";

    /// <summary>What Workspaces publishes (exchange names derive from the types; ADR-0009). It listens to nothing yet.</summary>
    public static MessagingTopology Topology => new MessagingTopology()
        .Publish<WorkspaceCreated>()
        .Publish<WorkspaceArchived>()
        .Publish<WorkspaceInstructionsChanged>()
        .Publish<MemberAdded>()
        .Publish<MemberRoleChanged>()
        .Publish<MemberRemoved>()
        .Publish<AccessRevoked>();

    public static IServiceCollection AddWorkspacesApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddValidatorsFromAssembly(typeof(WorkspacesApplicationModule).Assembly);
        services.AddScoped<WorkspacePermissionCache>();
        return services;
    }
}
