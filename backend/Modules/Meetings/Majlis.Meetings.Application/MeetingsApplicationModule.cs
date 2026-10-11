using FluentValidation;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Domain.Events;
using Majlis.Meetings.Application.Approvals;
using Majlis.Meetings.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Majlis.Meetings.Application;

public static class MeetingsApplicationModule
{
    public const string ModuleName = "meetings";

    public static MessagingTopology Topology => new MessagingTopology()
        .Publish<TranscriptionRequested>()
        .Publish<MinutesRequested>()
        .Publish<MinutesPublished>()
        .Listen<TranscriptionCompleted>()
        .Listen<TranscriptionFailed>()
        .Listen<MinutesGenerated>()
        .Listen<MinutesFailed>()
        .Listen<ActionExecuted>()
        .Listen<MemberAdded>()
        .Listen<MemberRoleChanged>()
        .Listen<MemberRemoved>();

    public static IServiceCollection AddMeetingsApplicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MeetingsOptions>(configuration.GetSection("Meetings"));
        services.AddValidatorsFromAssembly(typeof(MeetingsApplicationModule).Assembly);
        var approvals = configuration.GetSection("Approvals").Get<ApprovalsClientOptions>() ?? new ApprovalsClientOptions();
        services.AddRefitGeneratedClient<IApprovalsClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(approvals.BaseUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(15);
            });
        return services;
    }
}
