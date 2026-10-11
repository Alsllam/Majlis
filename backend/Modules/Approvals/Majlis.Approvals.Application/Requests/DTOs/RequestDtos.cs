using System.Text.Json;
using Majlis.Approvals.Domain.Enums;
using Majlis.Framework.Application.Dtos;

namespace Majlis.Approvals.Application.Requests.DTOs;

public sealed record ApprovalActorDto(Guid UserId, string DisplayName);

/// <summary><c>CanDecide</c>: whether the caller may approve or reject it now (default policy).</summary>
public sealed record ApprovalRequestDto(
    Guid Id,
    Guid WorkspaceId,
    Guid RoomId,
    Guid SessionId,
    Guid? TurnId,
    string Tool,
    JsonElement Args,
    JsonElement? EditedArgs,
    string Summary,
    string? Reason,
    RiskLevel Risk,
    ApprovalStatus Status,
    ApprovalActorDto RequestedBy,
    DateTime ExpiresAt,
    ApprovalActorDto? DecidedBy,
    DateTime? DecidedAt,
    string? DecisionNote,
    string? ResultSummary,
    Guid? ResultEntityId,
    string? FailureReasonKey,
    DateTime CreationTime,
    bool CanDecide);

public sealed record ApprovalRequestIdDto(Guid Id);

public sealed record FilterApprovalRequestDto : BaseFilterRequestDto
{
    public Guid? WorkspaceId { get; init; }
    public Guid? SessionId { get; init; }
    public ApprovalStatus? Status { get; init; }

    /// <summary>Only requests the caller may decide (the approver inbox, FR-APR-009).</summary>
    public bool ForMe { get; init; }
}

/// <summary>ai-service → Approvals, with the driver's token (the requester is the token's user).</summary>
public sealed record CreateApprovalRequestDto
{
    public Guid WorkspaceId { get; init; }
    public Guid RoomId { get; init; }
    public Guid SessionId { get; init; }
    public Guid? TurnId { get; init; }
    public string Tool { get; init; } = string.Empty;
    public JsonElement Args { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public sealed record ApproveRequestDto
{
    public Guid Id { get; init; }

    /// <summary>Edit then approve (FR-APR-003).</summary>
    public JsonElement? EditedArgs { get; init; }

    public string? Note { get; init; }
}

public sealed record RejectRequestDto
{
    public Guid Id { get; init; }
    public string Reason { get; init; } = string.Empty;
}
