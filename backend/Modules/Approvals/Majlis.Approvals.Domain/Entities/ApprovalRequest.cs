using Majlis.Approvals.Domain.Constants;
using Majlis.Approvals.Domain.Enums;
using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;

namespace Majlis.Approvals.Domain.Entities;

/// <summary>A mutating agent action waiting for a person (FR-APR-001…007). Nothing runs before <see cref="Approve"/>.</summary>
public class ApprovalRequest : FullAuditedEntity<Guid>, IMultiTenant
{
    protected ApprovalRequest()
    {
    }

    public ApprovalRequest(
        Guid id,
        Guid tenantId,
        Guid workspaceId,
        Guid roomId,
        Guid sessionId,
        Guid? turnId,
        string tool,
        string argsJson,
        string summary,
        string? reason,
        RiskLevel risk,
        Guid requestedByUserId,
        string requestedByDisplayName,
        DateTime expiresAt)
        : base(id)
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        RoomId = roomId;
        SessionId = sessionId;
        TurnId = turnId;
        Tool = tool;
        ArgsJson = argsJson;
        Summary = summary;
        Reason = reason;
        Risk = risk;
        RequestedByUserId = requestedByUserId;
        RequestedByDisplayName = requestedByDisplayName;
        ExpiresAt = expiresAt;
        Status = ApprovalStatus.Pending;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid? TurnId { get; private set; }
    public string Tool { get; private set; } = string.Empty;

    /// <summary>Arguments as the agent proposed them.</summary>
    public string ArgsJson { get; private set; } = string.Empty;

    /// <summary>Arguments after "edit then approve" (FR-APR-003); null when approved as proposed.</summary>
    public string? EditedArgsJson { get; private set; }

    /// <summary>Plain-language description of the action, in the session's language.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>Why the agent proposed it.</summary>
    public string? Reason { get; private set; }

    public RiskLevel Risk { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string RequestedByDisplayName { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? DecidedByDisplayName { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }
    public string? ResultSummary { get; private set; }
    public Guid? ResultEntityId { get; private set; }
    public string? FailureReasonKey { get; private set; }

    /// <summary>The arguments the owning module must run.</summary>
    public string EffectiveArgsJson => EditedArgsJson ?? ArgsJson;

    public bool IsPending => Status == ApprovalStatus.Pending;

    public void Approve(Guid userId, string displayName, string? editedArgsJson, string? note, DateTime at)
    {
        EnsurePending();
        EditedArgsJson = string.IsNullOrWhiteSpace(editedArgsJson) ? null : editedArgsJson;
        Decide(ApprovalStatus.Approved, userId, displayName, note, at);
    }

    public void Reject(Guid userId, string displayName, string reason, DateTime at)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new CustomValidationException(ApprovalsErrors.ReasonRequired);
        }

        Decide(ApprovalStatus.Rejected, userId, displayName, reason.Trim(), at);
    }

    /// <summary>FR-APR-006: expired requests never execute. Returns false when nothing changed.</summary>
    public bool Expire(DateTime now)
    {
        if (!IsPending || ExpiresAt > now)
        {
            return false;
        }

        Status = ApprovalStatus.Expired;
        DecidedAt = now;
        return true;
    }

    /// <summary>FR-APR-007: outcome reported by the owning module. Idempotent: a second report changes nothing.</summary>
    public bool MarkExecuted(Guid? entityId, string? resultSummary)
    {
        if (Status != ApprovalStatus.Approved)
        {
            return false;
        }

        Status = ApprovalStatus.Executed;
        ResultEntityId = entityId;
        ResultSummary = resultSummary;
        return true;
    }

    public bool MarkFailed(string reasonKey, string? detail)
    {
        if (Status != ApprovalStatus.Approved)
        {
            return false;
        }

        Status = ApprovalStatus.Failed;
        FailureReasonKey = reasonKey;
        ResultSummary = detail;
        return true;
    }

    private void Decide(ApprovalStatus status, Guid userId, string displayName, string? note, DateTime at)
    {
        Status = status;
        DecidedByUserId = userId;
        DecidedByDisplayName = displayName;
        DecisionNote = note;
        DecidedAt = at;
    }

    private void EnsurePending()
    {
        if (!IsPending)
        {
            throw new ConflictException(ApprovalsErrors.NotPending);
        }
    }
}
