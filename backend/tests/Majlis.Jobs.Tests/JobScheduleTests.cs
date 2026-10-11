using Cronos;
using FakeItEasy;
using Majlis.Framework.Domain.Events;
using Majlis.Jobs.Host.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Majlis.Jobs.Tests;

public class JobScheduleTests
{
    [Fact]
    public void Schedule_ShouldHaveUniqueIds_ValidCrons_AndOneTickMessageEach()
    {
        Assert.Equal(JobSchedule.Jobs.Count, JobSchedule.Jobs.Select(j => j.Id).Distinct().Count());
        foreach (var job in JobSchedule.Jobs)
        {
            var next = CronExpression.Parse(job.Cron).GetNextOccurrence(DateTime.UtcNow);
            Assert.NotNull(next);
            Assert.False(string.IsNullOrWhiteSpace(job.Description));
            var at = new DateTime(2026, 10, 12, 6, 0, 0, DateTimeKind.Utc);
            var message = job.Message(at);
            Assert.Equal(at, (DateTime)message.GetType().GetProperty("At")!.GetValue(message)!);
        }

        Assert.Equal(["approvals-expiry", "notifications-daily-digest", "audit-chain-verification", "meetings-recording-retention"], JobSchedule.Jobs.Select(j => j.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => JobSchedule.Get("nope"));
    }

    [Fact]
    public async Task Publisher_ShouldPublishTheJobsMessage_WithTheCurrentTime()
    {
        var publisher = A.Fake<IEventPublisher>();
        var sut = new JobPublisher(publisher, TimeProvider.System, NullLogger<JobPublisher>.Instance);

        await sut.PublishAsync(JobSchedule.AuditVerification, TestContext.Current.CancellationToken);

        A.CallTo(() => publisher.PublishAsync(A<IEvent>.That.Matches(m => IsFreshTick(m)), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static bool IsFreshTick(IEvent message) => message is AuditChainVerificationRequested r && (DateTime.UtcNow - r.At).Duration() < TimeSpan.FromMinutes(1);

    private static void Unused()
    {
    }
}
