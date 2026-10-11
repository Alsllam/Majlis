using System.Text;
using System.Text.Json;
using FluentValidation;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Majlis.Meetings.Application.Approvals;
using Majlis.Meetings.Application.Meetings.DTOs;
using Majlis.Meetings.Domain.Constants;
using Majlis.Meetings.Domain.Entities;
using Majlis.Meetings.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Majlis.Meetings.Application.Meetings;

[Route("meetings")]
public class MeetingsAppService(
    IRepository<Meeting, Guid> meetings,
    IReadOnlyRepository<WorkspaceMembership, Guid> memberships,
    IBlobStorage blobs,
    IApprovalsClient approvals,
    IUnitOfWork unitOfWork,
    IEventPublisher publisher,
    ICurrentUser currentUser,
    TimeProvider clock,
    IOptions<MeetingsOptions> options,
    IValidator<CreateMeetingDto> createValidator,
    IValidator<BeginRecordingUploadDto> uploadValidator,
    IValidator<UpdateMinutesDto> minutesValidator) : ApplicationService, IMeetingsAppService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <inheritdoc />
    [HttpPost("list")]
    [HasPermission(MeetingsPermissions.ViewMeeting)]
    public async Task<PagedResultDto<MeetingListDto>> GetListAsync(FilterMeetingDto input, CancellationToken cancellationToken = default)
    {
        await RequireMemberAsync(input.WorkspaceId, cancellationToken);
        var query = meetings.Query().Where(m => m.WorkspaceId == input.WorkspaceId
            && (input.RoomId == null || m.RoomId == input.RoomId)
            && (string.IsNullOrEmpty(input.FilterText) || m.Title.Contains(input.FilterText)));
        var total = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(m => m.HeldAt).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100))
            .Select(m => new MeetingListDto(m.Id, m.WorkspaceId, m.RoomId, m.Title, m.HeldAt, m.Source, m.Status, m.Minutes != null ? m.Minutes.Status : null, m.Minutes != null ? m.Minutes.ActionItems.Count : 0, m.CreationTime))
            .ToListAsync(cancellationToken);
        return new PagedResultDto<MeetingListDto>(page, total);
    }

    /// <inheritdoc />
    [HttpPost("getbyid")]
    [HasPermission(MeetingsPermissions.ViewMeeting)]
    public async Task<MeetingDto> GetAsync(MeetingIdDto input, CancellationToken cancellationToken = default)
    {
        var meeting = await LoadAsync(input.Id, tracked: false, cancellationToken);
        await RequireMemberAsync(meeting.WorkspaceId, cancellationToken);
        return meeting.ToDto();
    }

    /// <inheritdoc />
    [HttpPost("")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task<Guid> CreateAsync(CreateMeetingDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(createValidator, input, cancellationToken);
        await RequireContributorAsync(input.WorkspaceId, cancellationToken);
        var meeting = new Meeting(Guid.NewGuid(), currentUser.GetRequiredTenantId(), input.WorkspaceId, input.RoomId, input.Title.Trim(), DateTime.SpecifyKind(input.HeldAt, DateTimeKind.Utc), input.Language, input.Attendees, input.Source);
        if (input.Source != MeetingSource.Recording)
        {
            meeting.SetText(input.Text!);
        }

        await meetings.InsertAsync(meeting, autoSave: true, cancellationToken);
        return meeting.Id;
    }

    /// <inheritdoc />
    [HttpPost("begin-recording-upload")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task<RecordingUploadTicketDto> BeginRecordingUploadAsync(BeginRecordingUploadDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(uploadValidator, input, cancellationToken);
        var meeting = await LoadAsync(input.MeetingId, tracked: true, cancellationToken);
        await RequireContributorAsync(meeting.WorkspaceId, cancellationToken);
        if (input.SizeBytes > options.Value.MaxRecordingMb * 1024L * 1024L)
        {
            throw new CustomValidationException(MeetingsErrors.RecordingTooLarge, options.Value.MaxRecordingMb);
        }

        var extension = MeetingsFieldDefinitions.RecordingContentTypes[input.ContentType];
        var blobPath = $"{meeting.TenantId:D}/{meeting.WorkspaceId:D}/{meeting.Id:D}/recording{extension}";
        meeting.AttachRecording(blobPath, input.ContentType, input.SizeBytes);
        await meetings.UpdateAsync(meeting, autoSave: true, cancellationToken);
        var url = await blobs.CreateUploadUrlAsync(MeetingsFieldDefinitions.RecordingsContainer, blobPath, input.ContentType, TimeSpan.FromMinutes(options.Value.UploadUrlMinutes), cancellationToken);
        return new RecordingUploadTicketDto(meeting.Id, url.Url, url.ExpiresAt, new Dictionary<string, string> { ["x-ms-blob-type"] = "BlockBlob", ["Content-Type"] = input.ContentType });
    }

    /// <inheritdoc />
    [HttpPost("confirm-recording")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task<MeetingDto> ConfirmRecordingAsync(MeetingIdDto input, CancellationToken cancellationToken = default)
    {
        var meeting = await LoadAsync(input.Id, tracked: true, cancellationToken);
        await RequireContributorAsync(meeting.WorkspaceId, cancellationToken);
        var stored = meeting.RecordingBlobPath is null ? null : await blobs.GetPropertiesAsync(MeetingsFieldDefinitions.RecordingsContainer, meeting.RecordingBlobPath, cancellationToken);
        if (stored is null)
        {
            throw new CustomValidationException(MeetingsErrors.RecordingNotUploaded);
        }

        meeting.StartTranscription(stored.SizeBytes);
        await meetings.UpdateAsync(meeting, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new TranscriptionRequested(meeting.TenantId, meeting.Id, meeting.RecordingBlobPath!, meeting.RecordingContentType ?? string.Empty, meeting.Language), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return meeting.ToDto();
    }

    /// <inheritdoc />
    [HttpPost("generate-minutes")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task<MeetingDto> GenerateMinutesAsync(MeetingIdDto input, CancellationToken cancellationToken = default)
    {
        var meeting = await LoadAsync(input.Id, tracked: true, cancellationToken);
        await RequireContributorAsync(meeting.WorkspaceId, cancellationToken);
        meeting.StartSummarizing();
        await meetings.UpdateAsync(meeting, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new MinutesRequested(meeting.TenantId, meeting.Id, meeting.Title, meeting.HeldAt, meeting.Attendees, meeting.Language, meeting.TranscriptText!), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return meeting.ToDto();
    }

    /// <inheritdoc />
    [HttpPut("minutes")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task<MeetingDto> UpdateMinutesAsync(UpdateMinutesDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(minutesValidator, input, cancellationToken);
        var meeting = await LoadAsync(input.MeetingId, tracked: true, cancellationToken);
        await RequireContributorAsync(meeting.WorkspaceId, cancellationToken);
        meeting.RequireMinutes().Edit(input.Summary, input.Decisions, input.OpenQuestions, input.ActionItems.Select(i => (i.Id, i.Text, i.OwnerName, i.DueDate)), clock.GetUtcNow().UtcDateTime);
        await meetings.UpdateAsync(meeting, autoSave: true, cancellationToken);
        return meeting.ToDto();
    }

    /// <inheritdoc />
    [HttpPost("approve-minutes")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task<MeetingDto> ApproveMinutesAsync(MeetingIdDto input, CancellationToken cancellationToken = default)
    {
        var meeting = await LoadAsync(input.Id, tracked: true, cancellationToken);
        var member = await RequireContributorAsync(meeting.WorkspaceId, cancellationToken);
        meeting.RequireMinutes().Approve(member.UserId, member.DisplayName, clock.GetUtcNow().UtcDateTime);
        await meetings.UpdateAsync(meeting, autoSave: true, cancellationToken);
        return meeting.ToDto();
    }

    /// <inheritdoc />
    [HttpPost("propose-action-items")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task<ProposedActionItemsDto> ProposeActionItemsAsync(MeetingIdDto input, CancellationToken cancellationToken = default)
    {
        var meeting = await LoadAsync(input.Id, tracked: true, cancellationToken);
        await RequireContributorAsync(meeting.WorkspaceId, cancellationToken);
        var minutes = meeting.RequireMinutes();
        var items = minutes.ItemsToPropose();
        var authorization = Request.Headers.Authorization.ToString();
        var ids = new List<Guid>();
        foreach (var item in items)
        {
            var args = JsonSerializer.SerializeToElement(new { title = item.Text, description = $"{meeting.Title} — {meeting.HeldAt:yyyy-MM-dd}", assigneeName = item.OwnerName, dueDate = item.DueDate, priority = "Normal" }, Json);
            var summary = meeting.Language == "en" ? $"Create task: {item.Text}" : $"إنشاء مهمة: {item.Text}";
            var reason = meeting.Language == "en" ? $"Action item of the meeting \"{meeting.Title}\"" : $"بند عمل من اجتماع «{meeting.Title}»";
            var requestId = await approvals.CreateAsync(new CreateApprovalRequestBody(meeting.WorkspaceId, meeting.RoomId, Guid.Empty, null, "create_task", args, summary, reason), authorization, cancellationToken);
            item.LinkApproval(requestId);
            ids.Add(requestId);
            await meetings.UpdateAsync(meeting, autoSave: true, cancellationToken); // one at a time: a failure half-way leaves the linked ones linked
        }

        minutes.MarkProposed(clock.GetUtcNow().UtcDateTime);
        await meetings.UpdateAsync(meeting, autoSave: true, cancellationToken);
        return new ProposedActionItemsDto(ids.Count, ids);
    }

    /// <inheritdoc />
    [HttpPost("publish-minutes")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task<MeetingDto> PublishMinutesAsync(MeetingIdDto input, CancellationToken cancellationToken = default)
    {
        var meeting = await LoadAsync(input.Id, tracked: true, cancellationToken);
        var member = await RequireContributorAsync(meeting.WorkspaceId, cancellationToken);
        meeting.RequireMinutes().MarkPublished(clock.GetUtcNow().UtcDateTime);
        await meetings.UpdateAsync(meeting, autoSave: false, cancellationToken);
        var title = (meeting.Language == "en" ? "Minutes: " : "محضر اجتماع: ") + meeting.Title;
        await publisher.PublishAsync(new MinutesPublished(meeting.TenantId, meeting.WorkspaceId, meeting.RoomId, meeting.Id, title, meeting.ToMarkdown(), meeting.Language, member.UserId, member.DisplayName), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return meeting.ToDto();
    }

    /// <inheritdoc />
    [HttpPost("export-minutes")]
    [HasPermission(MeetingsPermissions.ViewMeeting)]
    public async Task<FileContentResult> ExportMinutesAsync(MeetingIdDto input, CancellationToken cancellationToken = default)
    {
        var meeting = await LoadAsync(input.Id, tracked: false, cancellationToken);
        await RequireMemberAsync(meeting.WorkspaceId, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(meeting.ToMarkdown());
        return new FileContentResult(bytes, "text/markdown; charset=utf-8") { FileDownloadName = $"minutes-{meeting.HeldAt:yyyyMMdd}.md" };
    }

    /// <inheritdoc />
    [HttpDelete("")]
    [HasPermission(MeetingsPermissions.ManageMeeting)]
    public async Task DeleteAsync(MeetingIdDto input, CancellationToken cancellationToken = default)
    {
        var meeting = await LoadAsync(input.Id, tracked: true, cancellationToken);
        var member = await RequireContributorAsync(meeting.WorkspaceId, cancellationToken);
        if (!member.CanManage && meeting.CreatorId != currentUser.GetRequiredId())
        {
            throw new ForbiddenException();
        }

        await meetings.DeleteAsync(meeting, autoSave: true, cancellationToken);
    }

    private async Task<Meeting> LoadAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? meetings.QueryTracked() : meetings.Query();
        return await query.Include(m => m.Minutes!).ThenInclude(x => x.ActionItems).FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException();
    }

    private async Task<WorkspaceMembership> RequireMemberAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredId();
        return await memberships.Query().FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken)
            ?? throw new ForbiddenException(MeetingsErrors.NotWorkspaceMember);
    }

    private async Task<WorkspaceMembership> RequireContributorAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var member = await RequireMemberAsync(workspaceId, cancellationToken);
        return member.CanContribute ? member : throw new ForbiddenException(MeetingsErrors.NotWorkspaceMember);
    }
}
