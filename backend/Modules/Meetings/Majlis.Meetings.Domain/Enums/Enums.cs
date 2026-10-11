namespace Majlis.Meetings.Domain.Enums;

/// <summary>Where the text came from (FR-MTG-001).</summary>
public enum MeetingSource
{
    Notes = 0,
    Transcript = 1,
    Recording = 2,
}

public enum MeetingStatus
{
    /// <summary>Created; a recording may still be on its way.</summary>
    Created = 0,
    Transcribing = 1,
    Transcribed = 2,
    Summarizing = 3,
    MinutesReady = 4,
    Failed = 5,
}

public enum MinutesStatus
{
    Draft = 0,
    Approved = 1,
}
