namespace Majlis.Approvals.Domain.Enums;

/// <summary>SRS §4.5 risk levels: low = create a task or draft; medium = update/assign; high = anything leaving Majlis or deleting.</summary>
public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Expired = 3,
    Executed = 4,
    Failed = 5,
}
