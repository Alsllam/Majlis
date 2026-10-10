using Majlis.Approvals.Application.Requests.DTOs;
using Majlis.Framework.Application.Dtos;

namespace Majlis.Approvals.Application.Requests;

/// <summary>Approval requests for agent actions (SRS §4.5). The only door for agent-originated writes.</summary>
public interface IApprovalRequestsAppService
{
    /// <summary>Requests in the caller's workspaces, newest first; <c>ForMe</c> narrows to the ones the caller may decide.</summary>
    Task<PagedResultDto<ApprovalRequestDto>> GetListAsync(FilterApprovalRequestDto input, CancellationToken cancellationToken = default);

    Task<ApprovalRequestDto> GetAsync(ApprovalRequestIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Called by ai-service with the driver's token while a turn runs; publishes <c>ApprovalRequested</c>. Returns the id.</summary>
    Task<Guid> CreateAsync(CreateApprovalRequestDto input, CancellationToken cancellationToken = default);

    /// <summary>Approve, optionally with edited arguments; publishes <c>ApprovalDecided</c> and <c>ActionApproved</c>.</summary>
    Task<ApprovalRequestDto> ApproveAsync(ApproveRequestDto input, CancellationToken cancellationToken = default);

    /// <summary>Reject with a reason; publishes <c>ApprovalDecided</c> and <c>ActionRejected</c>.</summary>
    Task<ApprovalRequestDto> RejectAsync(RejectRequestDto input, CancellationToken cancellationToken = default);
}
