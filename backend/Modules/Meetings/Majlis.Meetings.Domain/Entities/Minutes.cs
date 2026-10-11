using System.Text.Json;
using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Meetings.Domain.Constants;
using Majlis.Meetings.Domain.Enums;

namespace Majlis.Meetings.Domain.Entities;

/// <summary>Minutes of one meeting (FR-MTG-003/005): a draft from the agent, edited and approved by people.</summary>
public class Minutes : Entity<Guid>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly List<ActionItem> _actionItems = [];

    protected Minutes()
    {
    }

    public Minutes(Guid id, Guid meetingId)
        : base(id)
    {
        MeetingId = meetingId;
    }

    public Guid MeetingId { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string DecisionsJson { get; private set; } = "[]";
    public string OpenQuestionsJson { get; private set; } = "[]";
    public string Language { get; private set; } = "ar";
    public MinutesStatus Status { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public string? ApprovedByDisplayName { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? ActionItemsProposedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public IReadOnlyCollection<ActionItem> ActionItems => _actionItems;

    public IReadOnlyList<string> Decisions => JsonSerializer.Deserialize<List<string>>(DecisionsJson, Json) ?? [];

    public IReadOnlyList<string> OpenQuestions => JsonSerializer.Deserialize<List<string>>(OpenQuestionsJson, Json) ?? [];

    internal void Replace(string summary, IEnumerable<string> decisions, IEnumerable<string> openQuestions, IEnumerable<(string Text, string? Owner, DateOnly? Due)> items, string language, DateTime at)
    {
        Summary = summary.Trim();
        DecisionsJson = JsonSerializer.Serialize(Clean(decisions), Json);
        OpenQuestionsJson = JsonSerializer.Serialize(Clean(openQuestions), Json);
        Language = language;
        GeneratedAt = at;
        EditedAt = null;
        _actionItems.Clear();
        var order = 0;
        foreach (var (text, owner, due) in items.Where(i => !string.IsNullOrWhiteSpace(i.Text)))
        {
            _actionItems.Add(new ActionItem(Guid.NewGuid(), Id, order++, text.Trim(), string.IsNullOrWhiteSpace(owner) ? null : owner.Trim(), due));
        }
    }

    /// <summary>People edit everything until approval (FR-MTG-005); items keep their ids when they still exist.</summary>
    public void Edit(string summary, IEnumerable<string> decisions, IEnumerable<string> openQuestions, IEnumerable<(Guid? Id, string Text, string? Owner, DateOnly? Due)> items, DateTime at)
    {
        EnsureDraft();
        Summary = summary.Trim();
        DecisionsJson = JsonSerializer.Serialize(Clean(decisions), Json);
        OpenQuestionsJson = JsonSerializer.Serialize(Clean(openQuestions), Json);
        EditedAt = at;
        var kept = new List<ActionItem>();
        var order = 0;
        foreach (var (id, text, owner, due) in items.Where(i => !string.IsNullOrWhiteSpace(i.Text)))
        {
            var existing = id is { } itemId ? _actionItems.FirstOrDefault(i => i.Id == itemId) : null;
            if (existing is null)
            {
                kept.Add(new ActionItem(Guid.NewGuid(), Id, order++, text.Trim(), string.IsNullOrWhiteSpace(owner) ? null : owner.Trim(), due));
            }
            else
            {
                existing.Update(order++, text.Trim(), string.IsNullOrWhiteSpace(owner) ? null : owner.Trim(), due);
                kept.Add(existing);
            }
        }

        _actionItems.RemoveAll(i => !kept.Contains(i));
        _actionItems.AddRange(kept.Where(i => !_actionItems.Contains(i)));
        _actionItems.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public void Approve(Guid userId, string displayName, DateTime at)
    {
        EnsureDraft();
        Status = MinutesStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedByDisplayName = displayName;
        ApprovedAt = at;
    }

    /// <summary>FR-MTG-004: the items go to Approvals in bulk, once.</summary>
    public IReadOnlyList<ActionItem> ItemsToPropose()
    {
        if (Status != MinutesStatus.Approved)
        {
            throw new CustomValidationException(MeetingsErrors.MinutesNotApproved);
        }

        var pending = _actionItems.Where(i => i.ApprovalRequestId is null).OrderBy(i => i.Order).ToList();
        if (pending.Count == 0)
        {
            throw new ConflictException(MeetingsErrors.AlreadyProposed);
        }

        return pending;
    }

    public void MarkProposed(DateTime at) => ActionItemsProposedAt = at;

    public void MarkPublished(DateTime at)
    {
        if (Status != MinutesStatus.Approved)
        {
            throw new CustomValidationException(MeetingsErrors.MinutesNotApproved);
        }

        PublishedAt = at;
    }

    private void EnsureDraft()
    {
        if (Status == MinutesStatus.Approved)
        {
            throw new ConflictException(MeetingsErrors.MinutesApproved);
        }
    }

    private static List<string> Clean(IEnumerable<string> values) => values.Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
}

/// <summary>One action item of the minutes; linked to its approval request and then to the task it became.</summary>
public class ActionItem : Entity<Guid>
{
    protected ActionItem()
    {
    }

    internal ActionItem(Guid id, Guid minutesId, int order, string text, string? ownerName, DateOnly? dueDate)
        : base(id)
    {
        MinutesId = minutesId;
        Order = order;
        Text = text;
        OwnerName = ownerName;
        DueDate = dueDate;
    }

    public Guid MinutesId { get; private set; }
    public int Order { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? OwnerName { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public Guid? TaskId { get; private set; }

    internal void Update(int order, string text, string? ownerName, DateOnly? dueDate)
    {
        Order = order;
        Text = text;
        OwnerName = ownerName;
        DueDate = dueDate;
    }

    public void LinkApproval(Guid requestId) => ApprovalRequestId = requestId;

    public void LinkTask(Guid taskId) => TaskId = taskId;
}
