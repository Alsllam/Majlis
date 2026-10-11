using System.Text.Json;
using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Meetings.Domain.Constants;
using Majlis.Meetings.Domain.Enums;

namespace Majlis.Meetings.Domain.Entities;

/// <summary>A meeting of a room (FR-MTG-001): notes, a transcript or a recording in, minutes out.</summary>
public class Meeting : FullAuditedEntity<Guid>, IMultiTenant
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    protected Meeting()
    {
    }

    public Meeting(Guid id, Guid tenantId, Guid workspaceId, Guid roomId, string title, DateTime heldAt, string language, IEnumerable<string> attendees, MeetingSource source)
        : base(id)
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        RoomId = roomId;
        Title = title;
        HeldAt = heldAt;
        Language = language;
        AttendeesJson = JsonSerializer.Serialize(attendees.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct().ToList(), Json);
        Source = source;
        Status = MeetingStatus.Created;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid RoomId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime HeldAt { get; private set; }

    /// <summary>Language of the minutes (`ar` / `en`), chosen by the person (FR-MTG-003).</summary>
    public string Language { get; private set; } = "ar";

    public string AttendeesJson { get; private set; } = "[]";
    public MeetingSource Source { get; private set; }
    public MeetingStatus Status { get; private set; }
    public string? RecordingBlobPath { get; private set; }
    public string? RecordingContentType { get; private set; }
    public long RecordingSizeBytes { get; private set; }

    /// <summary>Notes, the uploaded transcript, or the transcription result. User content: stored as written.</summary>
    public string? TranscriptText { get; private set; }

    public string? FailureReasonKey { get; private set; }
    public Minutes? Minutes { get; private set; }

    public IReadOnlyList<string> Attendees => JsonSerializer.Deserialize<List<string>>(AttendeesJson, Json) ?? [];

    public bool HasTranscript => !string.IsNullOrWhiteSpace(TranscriptText);

    public void SetText(string text)
    {
        TranscriptText = text.Trim();
        Status = MeetingStatus.Transcribed;
        FailureReasonKey = null;
    }

    public void AttachRecording(string blobPath, string contentType, long sizeBytes)
    {
        RecordingBlobPath = blobPath;
        RecordingContentType = contentType;
        RecordingSizeBytes = sizeBytes;
    }

    /// <summary>The recording is in storage; transcription starts (FR-MTG-002).</summary>
    public void StartTranscription(long sizeBytes)
    {
        if (RecordingBlobPath is null)
        {
            throw new CustomValidationException(MeetingsErrors.RecordingNotUploaded);
        }

        RecordingSizeBytes = sizeBytes;
        Status = MeetingStatus.Transcribing;
        FailureReasonKey = null;
    }

    public void CompleteTranscription(string text, string? language)
    {
        TranscriptText = text.Trim();
        Status = MeetingStatus.Transcribed;
        FailureReasonKey = null;
        _ = language; // the minutes language stays the person's choice
    }

    public void Fail(string reasonKey)
    {
        Status = MeetingStatus.Failed;
        FailureReasonKey = reasonKey;
    }

    /// <summary>Asks ai-service for the minutes (FR-MTG-003); only from a transcript, and never while one is being made.</summary>
    public void StartSummarizing()
    {
        if (!HasTranscript)
        {
            throw new CustomValidationException(MeetingsErrors.NoTranscript);
        }

        if (Status == MeetingStatus.Summarizing)
        {
            throw new ConflictException(MeetingsErrors.Busy);
        }

        if (Minutes?.Status == MinutesStatus.Approved)
        {
            throw new ConflictException(MeetingsErrors.MinutesApproved);
        }

        Status = MeetingStatus.Summarizing;
        FailureReasonKey = null;
    }

    public void ReceiveMinutes(string summary, IEnumerable<string> decisions, IEnumerable<string> openQuestions, IEnumerable<(string Text, string? Owner, DateOnly? Due)> items, string language, DateTime at)
    {
        if (Minutes?.Status == MinutesStatus.Approved)
        {
            return; // a late result never overwrites approved minutes
        }

        Minutes ??= new Minutes(Guid.NewGuid(), Id);
        Minutes.Replace(summary, decisions, openQuestions, items, language, at);
        Status = MeetingStatus.MinutesReady;
        FailureReasonKey = null;
    }

    public void FailMinutes(string reasonKey)
    {
        Status = HasTranscript ? MeetingStatus.Transcribed : MeetingStatus.Failed;
        FailureReasonKey = reasonKey;
    }

    public Minutes RequireMinutes() => Minutes ?? throw new CustomValidationException(MeetingsErrors.NoMinutes);

    /// <summary>The minutes as a document (export and publishing to knowledge).</summary>
    public string ToMarkdown()
    {
        var m = RequireMinutes();
        var ar = Language != "en";
        var lines = new List<string> { $"# {Title}", string.Empty, $"{(ar ? "التاريخ" : "Date")}: {HeldAt:yyyy-MM-dd}" };
        if (Attendees.Count > 0)
        {
            lines.Add($"{(ar ? "الحضور" : "Attendees")}: {string.Join("، ", Attendees)}");
        }

        lines.AddRange([string.Empty, $"## {(ar ? "الملخص" : "Summary")}", string.Empty, m.Summary, string.Empty, $"## {(ar ? "القرارات" : "Decisions")}", string.Empty]);
        lines.AddRange(m.Decisions.Select(d => $"- {d}"));
        lines.AddRange([string.Empty, $"## {(ar ? "بنود العمل" : "Action items")}", string.Empty]);
        lines.AddRange(m.ActionItems.OrderBy(i => i.Order).Select(i => $"- {i.Text}{(i.OwnerName is null ? string.Empty : $" — {i.OwnerName}")}{(i.DueDate is null ? string.Empty : $" ({i.DueDate:yyyy-MM-dd})")}"));
        if (m.OpenQuestions.Count > 0)
        {
            lines.AddRange([string.Empty, $"## {(ar ? "أسئلة مفتوحة" : "Open questions")}", string.Empty]);
            lines.AddRange(m.OpenQuestions.Select(q => $"- {q}"));
        }

        return string.Join('\n', lines) + "\n";
    }
}
