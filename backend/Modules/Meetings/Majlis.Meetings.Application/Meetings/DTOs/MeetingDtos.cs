using Majlis.Framework.Application.Dtos;
using Majlis.Meetings.Domain.Enums;

namespace Majlis.Meetings.Application.Meetings.DTOs;

public sealed record ActionItemDto(Guid Id, int Order, string Text, string? OwnerName, DateOnly? DueDate, Guid? ApprovalRequestId, Guid? TaskId);

public sealed record MinutesDto(
    Guid Id,
    string Summary,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<ActionItemDto> ActionItems,
    string Language,
    MinutesStatus Status,
    DateTime GeneratedAt,
    DateTime? EditedAt,
    string? ApprovedByDisplayName,
    DateTime? ApprovedAt,
    DateTime? ActionItemsProposedAt,
    DateTime? PublishedAt);

public sealed record MeetingDto(
    Guid Id,
    Guid WorkspaceId,
    Guid RoomId,
    string Title,
    DateTime HeldAt,
    string Language,
    IReadOnlyList<string> Attendees,
    MeetingSource Source,
    MeetingStatus Status,
    string? TranscriptText,
    bool HasRecording,
    string? FailureReasonKey,
    MinutesDto? Minutes,
    DateTime CreationTime,
    Guid? CreatorId);

public sealed record MeetingListDto(Guid Id, Guid WorkspaceId, Guid RoomId, string Title, DateTime HeldAt, MeetingSource Source, MeetingStatus Status, MinutesStatus? MinutesStatus, int ActionItemCount, DateTime CreationTime);

public sealed record MeetingIdDto(Guid Id);

public sealed record FilterMeetingDto : BaseFilterRequestDto
{
    public Guid WorkspaceId { get; init; }
    public Guid? RoomId { get; init; }
}

/// <summary>FR-MTG-001: notes or a transcript are pasted; a recording is uploaded afterwards with a pre-signed url.</summary>
public sealed record CreateMeetingDto
{
    public Guid WorkspaceId { get; init; }
    public Guid RoomId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime HeldAt { get; init; }
    public string Language { get; init; } = "ar";
    public List<string> Attendees { get; init; } = [];
    public MeetingSource Source { get; init; } = MeetingSource.Notes;

    /// <summary>Notes or transcript text; required unless the source is a recording.</summary>
    public string? Text { get; init; }
}

public sealed record BeginRecordingUploadDto
{
    public Guid MeetingId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

public sealed record RecordingUploadTicketDto(Guid MeetingId, Uri UploadUrl, DateTimeOffset ExpiresAt, IReadOnlyDictionary<string, string> Headers);

public sealed record ActionItemInputDto
{
    public Guid? Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? OwnerName { get; init; }
    public DateOnly? DueDate { get; init; }
}

public sealed record UpdateMinutesDto
{
    public Guid MeetingId { get; init; }
    public string Summary { get; init; } = string.Empty;
    public List<string> Decisions { get; init; } = [];
    public List<string> OpenQuestions { get; init; } = [];
    public List<ActionItemInputDto> ActionItems { get; init; } = [];
}

/// <summary>Result of proposing the action items: one approval request per item.</summary>
public sealed record ProposedActionItemsDto(int Proposed, IReadOnlyList<Guid> ApprovalRequestIds);
