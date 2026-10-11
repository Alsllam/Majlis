using Majlis.Meetings.Application.Meetings.DTOs;
using Majlis.Meetings.Domain.Entities;

namespace Majlis.Meetings.Application;

/// <summary>Explicit entity → DTO mapping (no AutoMapper; ADR-0008).</summary>
public static class MeetingsMappings
{
    public static ActionItemDto ToDto(this ActionItem i) => new(i.Id, i.Order, i.Text, i.OwnerName, i.DueDate, i.ApprovalRequestId, i.TaskId);

    public static MinutesDto ToDto(this Minutes m) => new(
        m.Id, m.Summary, m.Decisions, m.OpenQuestions, m.ActionItems.OrderBy(i => i.Order).Select(i => i.ToDto()).ToList(), m.Language, m.Status,
        m.GeneratedAt, m.EditedAt, m.ApprovedByDisplayName, m.ApprovedAt, m.ActionItemsProposedAt, m.PublishedAt);

    public static MeetingDto ToDto(this Meeting m) => new(
        m.Id, m.WorkspaceId, m.RoomId, m.Title, m.HeldAt, m.Language, m.Attendees, m.Source, m.Status, m.TranscriptText, m.RecordingBlobPath is not null,
        m.FailureReasonKey, m.Minutes?.ToDto(), m.CreationTime, m.CreatorId);
}
