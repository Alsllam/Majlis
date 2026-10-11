namespace Majlis.Approvals.Application;

public sealed class ApprovalsOptions
{
    /// <summary>FR-APR-006: requests expire after 24 hours by default.</summary>
    public int ExpiryHours { get; set; } = 24;

    public int SweepIntervalSeconds { get; set; } = 60;
}
