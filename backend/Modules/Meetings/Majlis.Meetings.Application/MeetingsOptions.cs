namespace Majlis.Meetings.Application;

public sealed class MeetingsOptions
{
    /// <summary>FR-MTG-001: recordings up to 500 MB.</summary>
    public int MaxRecordingMb { get; set; } = 500;

    public int UploadUrlMinutes { get; set; } = 30;
}

public sealed class ApprovalsClientOptions
{
    /// <summary>Approvals through the BFF, called with the caller's own token (the requester is the person).</summary>
    public string BaseUrl { get; set; } = "http://localhost:7000/api/approvals";
}
