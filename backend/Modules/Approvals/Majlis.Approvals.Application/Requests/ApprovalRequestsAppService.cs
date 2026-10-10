using FluentValidation;
using Majlis.Approvals.Application.Requests.DTOs;
using Majlis.Approvals.Domain.Constants;
using Majlis.Approvals.Domain.DomainServices;
using Majlis.Approvals.Domain.Entities;
using Majlis.Approvals.Domain.Enums;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Majlis.Approvals.Application.Requests;

[Route("requests")]
public class ApprovalRequestsAppService(
    IRepository<ApprovalRequest, Guid> requests,
    IReadOnlyRepository<WorkspaceMembership, Guid> memberships,
    IUnitOfWork unitOfWork,
    IEventPublisher publisher,
    ICurrentUser currentUser,
    TimeProvider clock,
    IOptions<ApprovalsOptions> options,
    IValidator<CreateApprovalRequestDto> createValidator,
    IValidator<ApproveRequestDto> approveValidator,
    IValidator<RejectRequestDto> rejectValidator) : ApplicationService, IApprovalRequestsAppService
{
    /// <inheritdoc />
    [HttpPost("list")]
    [HasPermission(ApprovalsPermissions.ViewApproval)]
    public async Task<PagedResultDto<ApprovalRequestDto>> GetListAsync(FilterApprovalRequestDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var mine = await memberships.Query().Where(m => m.UserId == userId).ToListAsync(cancellationToken);
        var workspaceIds = mine.Select(m => m.WorkspaceId).ToList();
        var query = requests.Query()
            .Where(r => workspaceIds.Contains(r.WorkspaceId)
                && (input.WorkspaceId == null || r.WorkspaceId == input.WorkspaceId)
                && (input.SessionId == null || r.SessionId == input.SessionId)
                && (input.Status == null || r.Status == input.Status)
                && (!input.ForMe || r.Status == ApprovalStatus.Pending));

        var rows = await query.OrderByDescending(r => r.CreationTime).Take(500).ToListAsync(cancellationToken);
        var byWorkspace = mine.ToDictionary(m => m.WorkspaceId);
        var items = rows
            .Select(r => r.ToDto(byWorkspace.TryGetValue(r.WorkspaceId, out var m) && ApprovalPolicyManager.MayDecide(r, m)))
            .Where(d => !input.ForMe || d.CanDecide)
            .ToList();
        var page = items.Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)).ToList();
        return new PagedResultDto<ApprovalRequestDto>(page, items.Count);
    }

    /// <inheritdoc />
    [HttpPost("getbyid")]
    [HasPermission(ApprovalsPermissions.ViewApproval)]
    public async Task<ApprovalRequestDto> GetAsync(ApprovalRequestIdDto input, CancellationToken cancellationToken = default)
    {
        var request = await requests.Query().FirstOrDefaultAsync(r => r.Id == input.Id, cancellationToken) ?? throw new EntityNotFoundException();
        var member = await RequireMemberAsync(request.WorkspaceId, cancellationToken);
        return request.ToDto(ApprovalPolicyManager.MayDecide(request, member));
    }

    /// <inheritdoc />
    [HttpPost("")]
    [HasPermission(ApprovalsPermissions.RequestAction)]
    public async Task<Guid> CreateAsync(CreateApprovalRequestDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(createValidator, input, cancellationToken);
        var member = await RequireMemberAsync(input.WorkspaceId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var request = new ApprovalRequest(
            Guid.NewGuid(), currentUser.GetRequiredTenantId(), input.WorkspaceId, input.RoomId, input.SessionId, input.TurnId,
            input.Tool, input.Args.GetRawText(), input.Summary.Trim(), string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim(),
            KnownTools.Risk[input.Tool], member.UserId, member.DisplayName, now.AddHours(options.Value.ExpiryHours));

        await requests.InsertAsync(request, autoSave: false, cancellationToken);
        await publisher.PublishAsync(
            new ApprovalRequested(
                request.TenantId, request.WorkspaceId, request.RoomId, request.SessionId, request.TurnId, request.Id, request.Tool,
                request.Summary, request.Reason, request.Risk.ToString(), request.ArgsJson, request.RequestedByUserId, request.RequestedByDisplayName,
                request.ExpiresAt),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.Id;
    }

    /// <inheritdoc />
    [HttpPost("approve")]
    [HasPermission(ApprovalsPermissions.ApproveAction)]
    public async Task<ApprovalRequestDto> ApproveAsync(ApproveRequestDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(approveValidator, input, cancellationToken);
        var request = await LoadTrackedAsync(input.Id, cancellationToken);
        var member = await RequireMemberAsync(request.WorkspaceId, cancellationToken);
        ApprovalPolicyManager.EnsureMayDecide(request, member);
        request.Approve(member.UserId, member.DisplayName, input.EditedArgs?.GetRawText(), input.Note?.Trim(), clock.GetUtcNow().UtcDateTime);

        await requests.UpdateAsync(request, autoSave: false, cancellationToken);
        await publisher.PublishAsync(
            new ApprovalDecided(request.TenantId, request.SessionId, request.Id, request.Tool, "Approved", member.UserId, member.DisplayName, request.DecisionNote, request.EditedArgsJson),
            cancellationToken);
        await publisher.PublishAsync(
            new ActionApproved(
                request.TenantId, request.WorkspaceId, request.RoomId, request.SessionId, request.TurnId, request.Id, request.Tool, request.EffectiveArgsJson,
                request.RequestedByUserId, request.RequestedByDisplayName, member.UserId, member.DisplayName),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToDto(false);
    }

    /// <inheritdoc />
    [HttpPost("reject")]
    [HasPermission(ApprovalsPermissions.ApproveAction)]
    public async Task<ApprovalRequestDto> RejectAsync(RejectRequestDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(rejectValidator, input, cancellationToken);
        var request = await LoadTrackedAsync(input.Id, cancellationToken);
        var member = await RequireMemberAsync(request.WorkspaceId, cancellationToken);
        ApprovalPolicyManager.EnsureMayDecide(request, member);
        request.Reject(member.UserId, member.DisplayName, input.Reason, clock.GetUtcNow().UtcDateTime);

        await requests.UpdateAsync(request, autoSave: false, cancellationToken);
        await publisher.PublishAsync(
            new ApprovalDecided(request.TenantId, request.SessionId, request.Id, request.Tool, "Rejected", member.UserId, member.DisplayName, request.DecisionNote, null),
            cancellationToken);
        await publisher.PublishAsync(new ActionRejected(request.TenantId, request.SessionId, request.Id, request.Tool, member.UserId, request.DecisionNote), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToDto(false);
    }

    private async Task<ApprovalRequest> LoadTrackedAsync(Guid id, CancellationToken cancellationToken)
        => await requests.QueryTracked().FirstOrDefaultAsync(r => r.Id == id, cancellationToken) ?? throw new EntityNotFoundException();

    private async Task<WorkspaceMembership> RequireMemberAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredId();
        return await memberships.Query().FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken)
            ?? throw new ForbiddenException(ApprovalsErrors.NotWorkspaceMember);
    }
}
