using FluentValidation;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Majlis.Workspaces.Application.Security;
using Majlis.Workspaces.Application.Workspaces.DTOs;
using Majlis.Workspaces.Domain.Constants;
using Majlis.Workspaces.Domain.Entities;
using Majlis.Workspaces.Domain.Enums;
using Majlis.Workspaces.Domain.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Workspaces.Application.Workspaces;

[Route("workspaces")]
public class WorkspacesAppService(
    IRepository<Workspace, Guid> workspaces,
    IUnitOfWork unitOfWork,
    IEventPublisher publisher,
    WorkspacePermissionCache permissionCache,
    ICurrentUser currentUser,
    IValidator<CreateWorkspaceDto> createValidator,
    IValidator<UpdateWorkspaceDto> updateValidator,
    IValidator<UpdateAgentInstructionsDto> instructionsValidator,
    IValidator<SetWorkspaceMemberDto> setMemberValidator) : ApplicationService, IWorkspacesAppService
{
    /// <inheritdoc />
    [HttpPost("list")]
    [HasPermission(WorkspacesPermissions.ViewWorkspace)]
    public async Task<PagedResultDto<WorkspaceListDto>> GetListAsync(FilterWorkspaceDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var query = workspaces.Query()
            .Where(w => w.Members.Any(m => m.UserId == userId)
                && (input.IncludeArchived || !w.IsArchived)
                && (string.IsNullOrEmpty(input.FilterText) || w.Name.Contains(input.FilterText)));

        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderByDescending(w => w.CreationTime)
            .Skip(input.SkipCount)
            .Take(Math.Clamp(input.MaxResultCount, 1, 100))
            .Select(w => new WorkspaceListDto(
                w.Id, w.Name, w.Description, w.Icon, w.Color, w.IsArchived,
                w.Members.Where(m => m.UserId == userId).Select(m => m.Role).First(),
                w.Members.Count,
                w.CreationTime))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<WorkspaceListDto>(page, total);
    }

    /// <inheritdoc />
    [HttpPost("getbyid")]
    [HasPermission(WorkspacesPermissions.ViewWorkspace)]
    public async Task<WorkspaceDto> GetAsync(WorkspaceIdDto input, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadAsync(input.Id, tracked: false, cancellationToken);
        var me = workspace.EnsureMember(currentUser.GetRequiredId());
        return workspace.ToDto(me.Role);
    }

    /// <inheritdoc />
    [HttpPost("")]
    [HasPermission(WorkspacesPermissions.CreateWorkspace)]
    public async Task<Guid> CreateAsync(CreateWorkspaceDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(createValidator, input, cancellationToken);
        var tenantId = currentUser.GetRequiredTenantId();
        var userId = currentUser.GetRequiredId();

        var workspace = new Workspace(Guid.NewGuid(), tenantId, input.Name.Trim(), Clean(input.Description), Clean(input.Icon), input.Color);
        workspace.AddFirstOwner(userId, currentUser.DisplayName ?? string.Empty);
        await workspaces.InsertAsync(workspace, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new WorkspaceCreated(tenantId, workspace.Id, workspace.Name, userId), cancellationToken);
        await publisher.PublishAsync(new MemberAdded(tenantId, workspace.Id, userId, currentUser.DisplayName ?? string.Empty, nameof(WorkspaceRole.Owner)), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissionCache.RefreshAsync(userId, cancellationToken);
        return workspace.Id;
    }

    /// <inheritdoc />
    [HttpPut("")]
    [HasPermission(WorkspacesPermissions.UpdateWorkspace)]
    public async Task UpdateAsync(UpdateWorkspaceDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(updateValidator, input, cancellationToken);
        var workspace = await LoadAsync(input.Id, tracked: true, cancellationToken);
        workspace.EnsureManager(currentUser.GetRequiredId());
        workspace.Update(input.Name.Trim(), Clean(input.Description), Clean(input.Icon), input.Color);
        await workspaces.UpdateAsync(workspace, autoSave: true, cancellationToken);
    }

    /// <inheritdoc />
    [HttpPut("instructions")]
    [HasPermission(WorkspacesPermissions.UpdateWorkspace)]
    public async Task UpdateAgentInstructionsAsync(UpdateAgentInstructionsDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(instructionsValidator, input, cancellationToken);
        var workspace = await LoadAsync(input.Id, tracked: true, cancellationToken);
        workspace.EnsureManager(currentUser.GetRequiredId());
        workspace.SetAgentInstructions(input.AgentInstructions);
        await workspaces.UpdateAsync(workspace, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new WorkspaceInstructionsChanged(workspace.TenantId, workspace.Id, workspace.AgentInstructions), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    [HttpPost("archive")]
    [HasPermission(WorkspacesPermissions.ArchiveWorkspace)]
    public Task ArchiveAsync(WorkspaceIdDto input, CancellationToken cancellationToken = default)
        => SetArchivedAsync(input.Id, archived: true, cancellationToken);

    /// <inheritdoc />
    [HttpPost("restore")]
    [HasPermission(WorkspacesPermissions.ArchiveWorkspace)]
    public Task RestoreAsync(WorkspaceIdDto input, CancellationToken cancellationToken = default)
        => SetArchivedAsync(input.Id, archived: false, cancellationToken);

    /// <inheritdoc />
    [HttpPost("members")]
    [HasPermission(WorkspacesPermissions.ManageMembers)]
    public async Task SetMemberAsync(SetWorkspaceMemberDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(setMemberValidator, input, cancellationToken);
        var workspace = await LoadAsync(input.WorkspaceId, tracked: true, cancellationToken);
        var displayName = input.DisplayName.Trim();
        var added = workspace.AddOrChangeMember(currentUser.GetRequiredId(), input.UserId, displayName, input.Role);
        await workspaces.UpdateAsync(workspace, autoSave: false, cancellationToken);
        // Published with the concrete type so the message alias (ADR-0009) is the event's own name.
        if (added)
        {
            await publisher.PublishAsync(new MemberAdded(workspace.TenantId, workspace.Id, input.UserId, displayName, input.Role.ToString()), cancellationToken);
        }
        else
        {
            await publisher.PublishAsync(new MemberRoleChanged(workspace.TenantId, workspace.Id, input.UserId, displayName, input.Role.ToString()), cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissionCache.RefreshAsync(input.UserId, cancellationToken);
    }

    /// <inheritdoc />
    [HttpDelete("members")]
    [HasPermission(WorkspacesPermissions.ManageMembers)]
    public async Task RemoveMemberAsync(RemoveWorkspaceMemberDto input, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadAsync(input.WorkspaceId, tracked: true, cancellationToken);
        workspace.RemoveMember(currentUser.GetRequiredId(), input.UserId);
        await workspaces.UpdateAsync(workspace, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new MemberRemoved(workspace.TenantId, workspace.Id, input.UserId), cancellationToken);
        await publisher.PublishAsync(new AccessRevoked(workspace.TenantId, input.UserId, "workspace", workspace.Id), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissionCache.RefreshAsync(input.UserId, cancellationToken);
    }

    private async Task SetArchivedAsync(Guid id, bool archived, CancellationToken cancellationToken)
    {
        var workspace = await LoadAsync(id, tracked: true, cancellationToken);
        var me = workspace.EnsureMember(currentUser.GetRequiredId());
        if (me.Role != WorkspaceRole.Owner)
        {
            throw new ForbiddenException(WorkspacesErrors.OnlyOwnerChangesOwner);
        }

        if (archived)
        {
            workspace.Archive();
        }
        else
        {
            workspace.Restore();
        }

        await workspaces.UpdateAsync(workspace, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new WorkspaceArchived(workspace.TenantId, workspace.Id, archived), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissionCache.RefreshAllAsync(workspace.Id, cancellationToken); // archived workspaces grant nothing
    }

    private async Task<Workspace> LoadAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? workspaces.QueryTracked() : workspaces.Query();
        return await query.Include(w => w.Members).FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
