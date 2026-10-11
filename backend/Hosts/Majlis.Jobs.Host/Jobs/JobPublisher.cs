using Hangfire;
using Majlis.Framework.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Majlis.Jobs.Host.Jobs;

/// <summary>What Hangfire runs: publish the job's tick message. A failed publish is retried by Hangfire.</summary>
public sealed partial class JobPublisher(IEventPublisher publisher, TimeProvider clock, ILogger<JobPublisher> logger)
{
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task PublishAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = JobSchedule.Get(jobId);
        var at = clock.GetUtcNow().UtcDateTime;
        await publisher.PublishAsync(job.Message(at), cancellationToken);
        Log.Published(logger, jobId, at);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "Job {JobId} ticked at {At}")]
        public static partial void Published(ILogger logger, string jobId, DateTime at);
    }
}
