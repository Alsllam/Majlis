using FakeItEasy;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Meetings.Application;
using Majlis.Meetings.Application.Approvals;
using Majlis.Meetings.Application.EventHandlers;
using Majlis.Meetings.Application.Meetings.DTOs;
using Majlis.Meetings.Domain.Constants;
using Majlis.Meetings.Domain.Entities;
using Majlis.Meetings.Domain.Enums;
using Majlis.Meetings.Tests.Application.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Meetings.Tests.Application.TestInfrastructure.MeetingsTestHost;

namespace Majlis.Meetings.Tests.Application.AppServices;

public class MeetingsAppServiceTests : IDisposable
{
    private readonly MeetingsTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private static CreateMeetingDto Notes(string title = "اجتماع المتابعة الأسبوعي") => new()
    {
        WorkspaceId = WorkspaceId,
        RoomId = RoomId,
        Title = title,
        HeldAt = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc),
        Language = "ar",
        Attendees = ["سارة", "خالد"],
        Source = MeetingSource.Notes,
        Text = "ناقشنا العقد الجديد. اتفقنا على مراجعة البنود. خالد يرسل المسودة الأسبوع القادم.",
    };

    private async Task ReceiveMinutesAsync(Guid meetingId)
    {
        using var scope = _host.Scope(Sara);
        var sp = scope.ServiceProvider;
        var message = new MinutesGenerated(TenantId, meetingId, "ملخص الاجتماع", ["مراجعة بنود العقد"], ["هل نحتاج مستشاراً خارجياً؟"],
            [new ActionItemContract("إرسال مسودة العقد", "خالد", new DateOnly(2026, 10, 12)), new ActionItemContract("تحديد موعد مع المورد", null, null)], "ar");
        await MinutesGeneratedHandler.Handle(message, sp.GetRequiredService<IRepository<Meeting, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), TimeProvider.System, Ct);
    }

    [Fact]
    public async Task Flow_ShouldGoFromNotesToApprovedMinutesTasksAndKnowledge()
    {
        await _host.SeedMembershipsAsync();
        using var khalid = _host.Scope(Khalid);
        var service = _host.Meetings(khalid);

        // 1. Create from notes: the text is the transcript, nothing to transcribe.
        var id = await service.CreateAsync(Notes(), Ct);
        var meeting = await service.GetAsync(new MeetingIdDto(id), Ct);
        Assert.Equal((MeetingStatus.Transcribed, MeetingSource.Notes, false), (meeting.Status, meeting.Source, meeting.HasRecording));
        Assert.Equal(["سارة", "خالد"], meeting.Attendees);
        Assert.Null(meeting.Minutes);

        // 2. Ask for the minutes: the request goes to ai-service, the meeting is busy until the result arrives.
        var busy = await service.GenerateMinutesAsync(new MeetingIdDto(id), Ct);
        Assert.Equal(MeetingStatus.Summarizing, busy.Status);
        A.CallTo(() => _host.Publisher.PublishAsync(A<MinutesRequested>.That.Matches(m => m.MeetingId == id && m.Language == "ar" && m.Attendees.Count == 2), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        await Assert.ThrowsAsync<ConflictException>(() => service.GenerateMinutesAsync(new MeetingIdDto(id), Ct));

        // 3. The draft arrives.
        await ReceiveMinutesAsync(id);
        meeting = await service.GetAsync(new MeetingIdDto(id), Ct);
        Assert.Equal(MeetingStatus.MinutesReady, meeting.Status);
        var minutes = Assert.IsType<MinutesDto>(meeting.Minutes);
        Assert.Equal((MinutesStatus.Draft, "ملخص الاجتماع", 2), (minutes.Status, minutes.Summary, minutes.ActionItems.Count));

        // 4. People edit: keep the first item (same id), drop the second, add a third.
        var first = minutes.ActionItems[0];
        var edited = await service.UpdateMinutesAsync(new UpdateMinutesDto
        {
            MeetingId = id,
            Summary = "ملخص معدّل",
            Decisions = ["مراجعة بنود العقد", "  "],
            OpenQuestions = [],
            ActionItems = [new ActionItemInputDto { Id = first.Id, Text = "إرسال مسودة العقد النهائية", OwnerName = "خالد", DueDate = first.DueDate }, new ActionItemInputDto { Text = "إبلاغ الإدارة" }],
        }, Ct);
        Assert.Equal("ملخص معدّل", edited.Minutes!.Summary);
        Assert.Equal(["مراجعة بنود العقد"], edited.Minutes.Decisions);
        Assert.Collection(edited.Minutes.ActionItems,
            i => Assert.Equal((first.Id, "إرسال مسودة العقد النهائية", 0), (i.Id, i.Text, i.Order)),
            i => Assert.Equal(("إبلاغ الإدارة", 1), (i.Text, i.Order)));
        Assert.NotNull(edited.Minutes.EditedAt);

        // Not before approval: proposing items and publishing need approved minutes.
        await Assert.ThrowsAsync<CustomValidationException>(() => service.ProposeActionItemsAsync(new MeetingIdDto(id), Ct));
        await Assert.ThrowsAsync<CustomValidationException>(() => service.PublishMinutesAsync(new MeetingIdDto(id), Ct));

        // 5. Approve: edits and regeneration are closed.
        var approved = await service.ApproveMinutesAsync(new MeetingIdDto(id), Ct);
        Assert.Equal((MinutesStatus.Approved, Khalid.Name), (approved.Minutes!.Status, approved.Minutes.ApprovedByDisplayName));
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateMinutesAsync(new UpdateMinutesDto { MeetingId = id, Summary = "x" }, Ct));
        await Assert.ThrowsAsync<ConflictException>(() => service.GenerateMinutesAsync(new MeetingIdDto(id), Ct));
        await ReceiveMinutesAsync(id); // a late result never overwrites approved minutes
        Assert.Equal("ملخص معدّل", (await service.GetAsync(new MeetingIdDto(id), Ct)).Minutes!.Summary);

        // 6. Action items become one create_task approval request each, with the caller's token, once.
        var proposed = await service.ProposeActionItemsAsync(new MeetingIdDto(id), Ct);
        Assert.Equal(2, proposed.Proposed);
        A.CallTo(() => _host.Approvals.CreateAsync(
                A<CreateApprovalRequestBody>.That.Matches(b => b.Tool == "create_task" && b.WorkspaceId == WorkspaceId && b.RoomId == RoomId && b.Args.GetProperty("title").GetString() == "إرسال مسودة العقد النهائية" && b.Args.GetProperty("assigneeName").GetString() == "خالد"),
                "Bearer test-token", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Approvals.CreateAsync(A<CreateApprovalRequestBody>._, A<string>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        meeting = await service.GetAsync(new MeetingIdDto(id), Ct);
        Assert.All(meeting.Minutes!.ActionItems, i => Assert.NotNull(i.ApprovalRequestId));
        Assert.NotNull(meeting.Minutes.ActionItemsProposedAt);
        await Assert.ThrowsAsync<ConflictException>(() => service.ProposeActionItemsAsync(new MeetingIdDto(id), Ct));

        // 7. Tasks reports the approval ran: the item links to its task.
        var requestId = proposed.ApprovalRequestIds[0];
        var taskId = Guid.NewGuid();
        using (var scope = _host.Scope(Sara))
        {
            var sp = scope.ServiceProvider;
            await ActionExecutedHandler.Handle(new ActionExecuted(TenantId, requestId, "create_task", taskId, "created"), sp.GetRequiredService<IRepository<ActionItem, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
            await ActionExecutedHandler.Handle(new ActionExecuted(TenantId, requestId, "create_task", Guid.NewGuid(), "created"), sp.GetRequiredService<IRepository<ActionItem, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), Ct); // redelivered with another id: ignored
        }

        meeting = await service.GetAsync(new MeetingIdDto(id), Ct);
        Assert.Equal(taskId, meeting.Minutes!.ActionItems.Single(i => i.ApprovalRequestId == requestId).TaskId);

        // 8. Publish to knowledge: the markdown travels in the event; export gives the same markdown.
        var published = await service.PublishMinutesAsync(new MeetingIdDto(id), Ct);
        Assert.NotNull(published.Minutes!.PublishedAt);
        A.CallTo(() => _host.Publisher.PublishAsync(
                A<MinutesPublished>.That.Matches(m => m.MeetingId == id && m.WorkspaceId == WorkspaceId && m.Title == "محضر اجتماع: اجتماع المتابعة الأسبوعي" && m.UserId == Khalid.Id && m.Markdown.Contains("## القرارات") && m.Markdown.Contains("- إبلاغ الإدارة")),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        var export = await service.ExportMinutesAsync(new MeetingIdDto(id), Ct);
        Assert.Equal("minutes-20261005.md", export.FileDownloadName);
        var markdown = System.Text.Encoding.UTF8.GetString(export.FileContents);
        Assert.StartsWith("# اجتماع المتابعة الأسبوعي", markdown, StringComparison.Ordinal);
        Assert.Contains("- إرسال مسودة العقد النهائية — خالد (2026-10-12)", markdown, StringComparison.Ordinal);

        // The list shows the state at a glance.
        var list = await service.GetListAsync(new FilterMeetingDto { WorkspaceId = WorkspaceId, SkipCount = 0, MaxResultCount = 10 }, Ct);
        var row = Assert.Single(list.Items);
        Assert.Equal((MeetingStatus.MinutesReady, MinutesStatus.Approved, 2), (row.Status, row.MinutesStatus, row.ActionItemCount));
    }

    [Fact]
    public async Task Recording_ShouldStartTranscription_WhenTheUploadIsConfirmed()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Sara);
        var service = _host.Meetings(scope);
        var id = await service.CreateAsync(Notes() with { Source = MeetingSource.Recording, Text = null }, Ct);
        Assert.Equal(MeetingStatus.Created, (await service.GetAsync(new MeetingIdDto(id), Ct)).Status);
        await Assert.ThrowsAsync<CustomValidationException>(() => service.GenerateMinutesAsync(new MeetingIdDto(id), Ct)); // no transcript yet
        await Assert.ThrowsAsync<CustomValidationException>(() => service.ConfirmRecordingAsync(new MeetingIdDto(id), Ct)); // nothing attached

        var tooLarge = new BeginRecordingUploadDto { MeetingId = id, FileName = "meeting.mp3", ContentType = "audio/mpeg", SizeBytes = 2 * 1024 * 1024 };
        await Assert.ThrowsAsync<CustomValidationException>(() => service.BeginRecordingUploadAsync(tooLarge, Ct));
        var ticket = await service.BeginRecordingUploadAsync(tooLarge with { SizeBytes = 1000 }, Ct);
        Assert.Contains($"/recordings/{TenantId:D}/{WorkspaceId:D}/{id:D}/recording.mp3", ticket.UploadUrl.ToString(), StringComparison.Ordinal);
        Assert.Equal("BlockBlob", ticket.Headers["x-ms-blob-type"]);

        // Confirm before the bytes are there: refused. After: transcription requested.
        A.CallTo(() => _host.Blobs.GetPropertiesAsync(MeetingsFieldDefinitions.RecordingsContainer, A<string>._, A<CancellationToken>._)).Returns((StoredBlobInfo?)null);
        await Assert.ThrowsAsync<CustomValidationException>(() => service.ConfirmRecordingAsync(new MeetingIdDto(id), Ct));
        A.CallTo(() => _host.Blobs.GetPropertiesAsync(MeetingsFieldDefinitions.RecordingsContainer, A<string>._, A<CancellationToken>._)).Returns(new StoredBlobInfo(1000, "audio/mpeg"));
        var confirmed = await service.ConfirmRecordingAsync(new MeetingIdDto(id), Ct);
        Assert.Equal((MeetingStatus.Transcribing, true), (confirmed.Status, confirmed.HasRecording));
        A.CallTo(() => _host.Publisher.PublishAsync(A<TranscriptionRequested>.That.Matches(m => m.MeetingId == id && m.ContentType == "audio/mpeg" && m.BlobPath.EndsWith("recording.mp3")), A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        // ai-service answers.
        var sp = scope.ServiceProvider;
        await TranscriptionFailedHandler.Handle(new TranscriptionFailed(TenantId, id, "Meetings:Transcription:Unavailable", null), sp.GetRequiredService<IRepository<Meeting, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
        var failed = await service.GetAsync(new MeetingIdDto(id), Ct);
        Assert.Equal((MeetingStatus.Failed, "Meetings:Transcription:Unavailable"), (failed.Status, failed.FailureReasonKey));
        await TranscriptionCompletedHandler.Handle(new TranscriptionCompleted(TenantId, id, " نص التفريغ ", "ar"), sp.GetRequiredService<IRepository<Meeting, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), TimeProvider.System, Ct);
        var done = await service.GetAsync(new MeetingIdDto(id), Ct);
        Assert.Equal((MeetingStatus.Transcribed, "نص التفريغ", null), (done.Status, done.TranscriptText, done.FailureReasonKey));

        // Minutes failure keeps the transcript and reports the reason.
        await service.GenerateMinutesAsync(new MeetingIdDto(id), Ct);
        await MinutesFailedHandler.Handle(new MinutesFailed(TenantId, id, "Meetings:Minutes:ModelError", "boom"), sp.GetRequiredService<IRepository<Meeting, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
        var minutesFailed = await service.GetAsync(new MeetingIdDto(id), Ct);
        Assert.Equal((MeetingStatus.Transcribed, "Meetings:Minutes:ModelError"), (minutesFailed.Status, minutesFailed.FailureReasonKey));
    }

    [Fact]
    public async Task RecordingRetention_ShouldDeleteOldRecordings_AndKeepTheTranscript()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Sara);
        var service = _host.Meetings(scope);
        var sp = scope.ServiceProvider;
        var id = await service.CreateAsync(Notes() with { Source = MeetingSource.Recording, Text = null }, Ct);
        await service.BeginRecordingUploadAsync(new BeginRecordingUploadDto { MeetingId = id, FileName = "m.mp3", ContentType = "audio/mpeg", SizeBytes = 100 }, Ct);
        A.CallTo(() => _host.Blobs.GetPropertiesAsync(MeetingsFieldDefinitions.RecordingsContainer, A<string>._, A<CancellationToken>._)).Returns(new StoredBlobInfo(100, "audio/mpeg"));
        await service.ConfirmRecordingAsync(new MeetingIdDto(id), Ct);
        var transcribedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        await TranscriptionCompletedHandler.Handle(new TranscriptionCompleted(TenantId, id, "نص", "ar"), sp.GetRequiredService<IRepository<Meeting, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), new FixedClock(transcribedAt), Ct);

        Task SweepAsync(DateTime at, int days) => RecordingRetentionHandler.Handle(new RecordingRetentionSweepRequested(at), sp.GetRequiredService<IRepository<Meeting, Guid>>(), _host.Blobs, sp.GetRequiredService<IUnitOfWork>(), Microsoft.Extensions.Options.Options.Create(new MeetingsOptions { RecordingRetentionDays = days }), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Meeting>>(), Ct);

        await SweepAsync(transcribedAt.AddDays(29), 30); // not yet
        await SweepAsync(transcribedAt.AddDays(40), 0); // retention off
        Assert.True((await service.GetAsync(new MeetingIdDto(id), Ct)).HasRecording);
        A.CallTo(() => _host.Blobs.DeleteAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();

        await SweepAsync(transcribedAt.AddDays(31), 30);
        await SweepAsync(transcribedAt.AddDays(32), 30); // nothing left
        var meeting = await service.GetAsync(new MeetingIdDto(id), Ct);
        Assert.Equal((false, "نص", MeetingStatus.Transcribed), (meeting.HasRecording, meeting.TranscriptText, meeting.Status));
        A.CallTo(() => _host.Blobs.DeleteAsync(MeetingsFieldDefinitions.RecordingsContainer, A<string>.That.EndsWith("recording.mp3"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    [Fact]
    public async Task Access_ShouldFollowTheWorkspaceRole()
    {
        await _host.SeedMembershipsAsync();
        Guid id;
        using (var sara = _host.Scope(Sara))
        {
            id = await _host.Meetings(sara).CreateAsync(Notes(), Ct);
        }

        // A viewer reads and exports but changes nothing; an outsider sees nothing.
        using (var noura = _host.Scope(Noura))
        {
            var service = _host.Meetings(noura);
            Assert.Equal(id, (await service.GetAsync(new MeetingIdDto(id), Ct)).Id);
            Assert.Single((await service.GetListAsync(new FilterMeetingDto { WorkspaceId = WorkspaceId, SkipCount = 0, MaxResultCount = 10 }, Ct)).Items);
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(Notes(), Ct));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GenerateMinutesAsync(new MeetingIdDto(id), Ct));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(new MeetingIdDto(id), Ct));
        }

        using (var outsider = _host.Scope(Outsider))
        {
            var service = _host.Meetings(outsider);
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetAsync(new MeetingIdDto(id), Ct));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetListAsync(new FilterMeetingDto { WorkspaceId = WorkspaceId, SkipCount = 0, MaxResultCount = 10 }, Ct));
        }

        // A contributor deletes only their own meetings; the owner deletes any.
        using (var khalid = _host.Scope(Khalid))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Meetings(khalid).DeleteAsync(new MeetingIdDto(id), Ct));
        }

        using (var sara = _host.Scope(Sara))
        {
            await _host.Meetings(sara).DeleteAsync(new MeetingIdDto(id), Ct);
            await Assert.ThrowsAsync<EntityNotFoundException>(() => _host.Meetings(sara).GetAsync(new MeetingIdDto(id), Ct));
        }
    }

    [Fact]
    public async Task Create_ShouldRejectNotesWithoutText()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Sara);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _host.Meetings(scope).CreateAsync(Notes() with { Text = " " }, Ct));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _host.Meetings(scope).CreateAsync(Notes() with { Language = "fr" }, Ct));
    }
}
