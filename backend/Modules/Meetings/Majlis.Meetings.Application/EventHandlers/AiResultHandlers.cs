using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Meetings.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Meetings.Application.EventHandlers;

internal static class MeetingLoader
{
    public static Task<Meeting?> LoadAsync(IRepository<Meeting, Guid> meetings, Guid id, CancellationToken cancellationToken)
        => meetings.QueryTracked().IgnoreQueryFilters().Include(m => m.Minutes!).ThenInclude(x => x.ActionItems).FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken);
}

[WolverineHandler]
public static class TranscriptionCompletedHandler
{
    public static async Task Handle(TranscriptionCompleted m, IRepository<Meeting, Guid> meetings, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var meeting = await MeetingLoader.LoadAsync(meetings, m.MeetingId, cancellationToken);
        if (meeting is null)
        {
            return;
        }

        meeting.CompleteTranscription(m.Text, m.Language);
        await meetings.UpdateAsync(meeting, autoSave: false, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

[WolverineHandler]
public static class TranscriptionFailedHandler
{
    public static async Task Handle(TranscriptionFailed m, IRepository<Meeting, Guid> meetings, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var meeting = await MeetingLoader.LoadAsync(meetings, m.MeetingId, cancellationToken);
        if (meeting is null)
        {
            return;
        }

        meeting.Fail(m.ReasonKey);
        await meetings.UpdateAsync(meeting, autoSave: false, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

[WolverineHandler]
public static class MinutesGeneratedHandler
{
    public static async Task Handle(MinutesGenerated m, IRepository<Meeting, Guid> meetings, IUnitOfWork unitOfWork, TimeProvider clock, CancellationToken cancellationToken)
    {
        var meeting = await MeetingLoader.LoadAsync(meetings, m.MeetingId, cancellationToken);
        if (meeting is null)
        {
            return;
        }

        meeting.ReceiveMinutes(m.Summary, m.Decisions, m.OpenQuestions, m.ActionItems.Select(i => (i.Text, i.OwnerName, i.DueDate)), m.Language, clock.GetUtcNow().UtcDateTime);
        await meetings.UpdateAsync(meeting, autoSave: false, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

[WolverineHandler]
public static class MinutesFailedHandler
{
    public static async Task Handle(MinutesFailed m, IRepository<Meeting, Guid> meetings, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var meeting = await MeetingLoader.LoadAsync(meetings, m.MeetingId, cancellationToken);
        if (meeting is null)
        {
            return;
        }

        meeting.FailMinutes(m.ReasonKey);
        await meetings.UpdateAsync(meeting, autoSave: false, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>An action item's approval ran in Tasks: remember the task it became (FR-MTG-004).</summary>
[WolverineHandler]
public static class ActionExecutedHandler
{
    public static async Task Handle(ActionExecuted m, IRepository<ActionItem, Guid> items, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (m.Tool != "create_task" || m.EntityId is null)
        {
            return;
        }

        var item = await items.QueryTracked().FirstOrDefaultAsync(i => i.ApprovalRequestId == m.RequestId, cancellationToken);
        if (item is null || item.TaskId is not null)
        {
            return;
        }

        item.LinkTask(m.EntityId.Value);
        await items.UpdateAsync(item, autoSave: false, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
