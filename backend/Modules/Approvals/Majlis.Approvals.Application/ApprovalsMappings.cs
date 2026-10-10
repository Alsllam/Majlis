using System.Text.Json;
using Majlis.Approvals.Application.Requests.DTOs;
using Majlis.Approvals.Domain.Entities;

namespace Majlis.Approvals.Application;

/// <summary>Explicit entity → DTO mapping (no AutoMapper; ADR-0008).</summary>
public static class ApprovalsMappings
{
    public static ApprovalRequestDto ToDto(this ApprovalRequest r, bool canDecide)
    {
        using var args = JsonDocument.Parse(r.ArgsJson);
        using var edited = r.EditedArgsJson is null ? null : JsonDocument.Parse(r.EditedArgsJson);
        return new ApprovalRequestDto(
            r.Id,
            r.WorkspaceId,
            r.RoomId,
            r.SessionId,
            r.TurnId,
            r.Tool,
            args.RootElement.Clone(),
            edited?.RootElement.Clone(),
            r.Summary,
            r.Reason,
            r.Risk,
            r.Status,
            new ApprovalActorDto(r.RequestedByUserId, r.RequestedByDisplayName),
            r.ExpiresAt,
            r.DecidedByUserId is { } d ? new ApprovalActorDto(d, r.DecidedByDisplayName ?? string.Empty) : null,
            r.DecidedAt,
            r.DecisionNote,
            r.ResultSummary,
            r.ResultEntityId,
            r.FailureReasonKey,
            r.CreationTime,
            canDecide);
    }
}
