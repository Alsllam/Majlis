using Majlis.Framework.Domain.Events;

namespace Majlis.Jobs.Host.Jobs;

/// <summary>One recurring job: a Hangfire id, a cron (UTC) and the tick message the owning module handles.</summary>
public sealed record ScheduledJob(string Id, string Cron, Func<DateTime, IEvent> Message, string Description);

/// <summary>
/// The schedule. The Jobs host owns *when*; each module owns *what* (the handler of the tick message), so no module
/// code runs here and the host references only the framework. Crons are overridable per job in <c>Jobs:Crons</c>.
/// </summary>
public static class JobSchedule
{
    public const string ApprovalExpiry = "approvals-expiry";
    public const string DailyDigest = "notifications-daily-digest";
    public const string AuditVerification = "audit-chain-verification";
    public const string RecordingRetention = "meetings-recording-retention";

    public static readonly IReadOnlyList<ScheduledJob> Jobs =
    [
        new(ApprovalExpiry, "* * * * *", at => new ApprovalExpirySweepRequested(at), "Expire approval requests past their deadline (FR-APR-006)"),
        new(DailyDigest, "0 6 * * *", at => new DailyDigestRequested(at), "Email each user a digest of unread notifications (FR-NTF-002); 06:00 UTC = 09:00 Riyadh"),
        new(AuditVerification, "30 1 * * *", at => new AuditChainVerificationRequested(at), "Recompute every tenant's audit hash chain (invariant 7)"),
        new(RecordingRetention, "0 2 * * *", at => new RecordingRetentionSweepRequested(at), "Delete meeting recordings past the retention period"),
    ];

    public static ScheduledJob Get(string id) => Jobs.FirstOrDefault(j => j.Id == id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown job.");
}
