using FluentValidation;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Domain.Events;
using Majlis.Knowledge.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Knowledge.Application;

public static class KnowledgeApplicationModule
{
    public const string ModuleName = "knowledge";

    /// <summary>Publishes upload/delete for ai-service; listens to its results and to the Workspaces member events (ADR-0009).</summary>
    public static MessagingTopology Topology => new MessagingTopology()
        .Publish<DocumentUploaded>()
        .Publish<DocumentDeleted>()
        .Listen<DocumentIndexed>()
        .Listen<DocumentIndexingFailed>()
        .Listen<MemberAdded>()
        .Listen<MemberRoleChanged>()
        .Listen<MemberRemoved>();

    public static IServiceCollection AddKnowledgeApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KnowledgeOptions>(configuration.GetSection("Knowledge"));
        services.AddValidatorsFromAssembly(typeof(KnowledgeApplicationModule).Assembly);
        return services;
    }
}
