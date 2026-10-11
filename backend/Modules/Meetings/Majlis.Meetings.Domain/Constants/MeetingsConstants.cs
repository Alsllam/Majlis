namespace Majlis.Meetings.Domain.Constants;

/// <summary>Permission names; identical strings are used by the web and mobile apps.</summary>
public static class MeetingsPermissions
{
    public const string ViewMeeting = "Permissions.Meetings.ViewMeeting";
    public const string ManageMeeting = "Permissions.Meetings.ManageMeeting";
}

public static class MeetingsErrors
{
    public const string NotWorkspaceMember = "Meetings:Meeting:NotWorkspaceMember";
    public const string TextRequired = "Meetings:Meeting:TextRequired";
    public const string NoTranscript = "Meetings:Meeting:NoTranscript";
    public const string NoMinutes = "Meetings:Meeting:NoMinutes";
    public const string MinutesApproved = "Meetings:Minutes:Approved";
    public const string MinutesNotApproved = "Meetings:Minutes:NotApproved";
    public const string AlreadyProposed = "Meetings:ActionItems:AlreadyProposed";
    public const string RecordingTooLarge = "Meetings:Recording:TooLarge";
    public const string RecordingTypeNotSupported = "Meetings:Recording:TypeNotSupported";
    public const string RecordingNotUploaded = "Meetings:Recording:NotUploaded";
    public const string Busy = "Meetings:Meeting:Busy";
}

public static class MeetingsFieldDefinitions
{
    public const string RecordingsContainer = "recordings";
    public const int MaxTitleLength = 200;
    public const int MaxAttendeesLength = 2000;
    public const int MaxTranscriptLength = 400_000;
    public const int MaxSummaryLength = 20_000;
    public const int MaxListJsonLength = 20_000;
    public const int MaxItemTextLength = 500;
    public const int MaxOwnerLength = 100;

    /// <summary>FR-MTG-001: m4a, mp3, wav, mp4, webm.</summary>
    public static readonly IReadOnlyDictionary<string, string> RecordingContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mp4"] = ".m4a",
        ["audio/x-m4a"] = ".m4a",
        ["audio/mpeg"] = ".mp3",
        ["audio/wav"] = ".wav",
        ["audio/x-wav"] = ".wav",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["audio/webm"] = ".webm",
    };
}
