using Majlis.Framework.Application.Dtos;
using Majlis.Meetings.Application.Meetings.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Majlis.Meetings.Application.Meetings;

/// <summary>Meetings of a room (SRS §4.7): text or recording in, transcript and minutes out, action items to Approvals.</summary>
public interface IMeetingsAppService
{
    Task<PagedResultDto<MeetingListDto>> GetListAsync(FilterMeetingDto input, CancellationToken cancellationToken = default);

    Task<MeetingDto> GetAsync(MeetingIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Creates the meeting; with notes or a transcript it is ready for minutes at once. Returns the id.</summary>
    Task<Guid> CreateAsync(CreateMeetingDto input, CancellationToken cancellationToken = default);

    /// <summary>Pre-signed url for the recording (FR-MTG-001), then <see cref="ConfirmRecordingAsync"/>.</summary>
    Task<RecordingUploadTicketDto> BeginRecordingUploadAsync(BeginRecordingUploadDto input, CancellationToken cancellationToken = default);

    /// <summary>The recording is in storage: transcription starts (FR-MTG-002).</summary>
    Task<MeetingDto> ConfirmRecordingAsync(MeetingIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Asks the agent for the minutes (FR-MTG-003); the result arrives as an event.</summary>
    Task<MeetingDto> GenerateMinutesAsync(MeetingIdDto input, CancellationToken cancellationToken = default);

    Task<MeetingDto> UpdateMinutesAsync(UpdateMinutesDto input, CancellationToken cancellationToken = default);

    Task<MeetingDto> ApproveMinutesAsync(MeetingIdDto input, CancellationToken cancellationToken = default);

    /// <summary>FR-MTG-004: every action item without a request becomes a <c>create_task</c> approval request.</summary>
    Task<ProposedActionItemsDto> ProposeActionItemsAsync(MeetingIdDto input, CancellationToken cancellationToken = default);

    /// <summary>FR-MTG-005: the approved minutes become a workspace document, indexed for the agent.</summary>
    Task<MeetingDto> PublishMinutesAsync(MeetingIdDto input, CancellationToken cancellationToken = default);

    /// <summary>The minutes as Markdown (DOCX/PDF later).</summary>
    Task<FileContentResult> ExportMinutesAsync(MeetingIdDto input, CancellationToken cancellationToken = default);

    Task DeleteAsync(MeetingIdDto input, CancellationToken cancellationToken = default);
}
