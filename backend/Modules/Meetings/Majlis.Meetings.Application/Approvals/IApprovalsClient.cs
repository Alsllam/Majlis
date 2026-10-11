using System.Text.Json;
using Refit;

namespace Majlis.Meetings.Application.Approvals;

/// <summary>Approvals module API (FR-MTG-004: action items become task approval requests). Called with the caller's bearer token.</summary>
public interface IApprovalsClient
{
    [Post("/requests")]
    Task<Guid> CreateAsync([Body] CreateApprovalRequestBody body, [Header("Authorization")] string authorization, CancellationToken cancellationToken = default);
}

public sealed record CreateApprovalRequestBody(Guid WorkspaceId, Guid RoomId, Guid SessionId, Guid? TurnId, string Tool, JsonElement Args, string Summary, string? Reason);
