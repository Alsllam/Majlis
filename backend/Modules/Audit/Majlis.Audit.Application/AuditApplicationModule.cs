using Majlis.Audit.Application.Recording;
using Majlis.Audit.Application.Verification;
using Majlis.Audit.Domain.Events;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Audit.Application;

public static class AuditApplicationModule
{
    public const string ModuleName = "audit";

    /// <summary>Audit only listens; it publishes nothing (ADR-0009).</summary>
    public static MessagingTopology Topology => new MessagingTopology()
        .Listen<SessionEventAppended>()
        .Listen<ApprovalRequested>()
        .Listen<ApprovalDecided>()
        .Listen<ApprovalExecuted>()
        .Listen<DocumentUploaded>()
        .Listen<DocumentDeleted>()
        .Listen<DocumentAccessed>()
        .Listen<TaskAssigned>()
        .Listen<MemberAdded>()
        .Listen<MemberRoleChanged>()
        .Listen<MemberRemoved>()
        .Listen<AuditChainVerificationRequested>();

    public static IServiceCollection AddAuditApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuditOptions>(configuration.GetSection("Audit"));
        services.AddScoped<AuditRecorder>();
        services.AddScoped<ChainVerifier>();
        return services;
    }
}
