using FluentValidation;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Majlis.Tasks.Application.Tasks.DTOs;
using Majlis.Tasks.Domain.Constants;
using Majlis.Tasks.Domain.Entities;
using Majlis.Tasks.Domain.Enums;
using TaskStatus = Majlis.Tasks.Domain.Enums.TaskStatus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Tasks.Application.Tasks;

[Route("tasks")]
public class TasksAppService(
    IRepository<TaskItem, Guid> tasks,
    IReadOnlyRepository<WorkspaceMembership, Guid> memberships,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IValidator<CreateTaskDto> createValidator,
    IValidator<UpdateTaskDto> updateValidator,
    IValidator<SetTaskStatusDto> statusValidator) : ApplicationService, ITasksAppService
{
    /// <inheritdoc />
    [HttpPost("list")]
    [HasPermission(TasksPermissions.ViewTask)]
    public async Task<PagedResultDto<TaskDto>> GetListAsync(FilterTaskDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var workspaceIds = await memberships.Query().Where(m => m.UserId == userId).Select(m => m.WorkspaceId).ToListAsync(cancellationToken);
        var query = tasks.Query()
            .Where(t => workspaceIds.Contains(t.WorkspaceId)
                && (input.WorkspaceId == null || t.WorkspaceId == input.WorkspaceId)
                && (input.RoomId == null || t.RoomId == input.RoomId)
                && (input.Status == null || t.Status == input.Status)
                && (!input.AssignedToMe || t.AssigneeUserId == userId)
                && (string.IsNullOrEmpty(input.FilterText) || t.Title.Contains(input.FilterText)));

        var total = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(t => t.CreationTime).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)).ToListAsync(cancellationToken);
        return new PagedResultDto<TaskDto>(page.Select(t => t.ToDto()).ToList(), total);
    }

    /// <inheritdoc />
    [HttpPost("getbyid")]
    [HasPermission(TasksPermissions.ViewTask)]
    public async Task<TaskDto> GetAsync(TaskIdDto input, CancellationToken cancellationToken = default)
    {
        var task = await tasks.Query().FirstOrDefaultAsync(t => t.Id == input.Id, cancellationToken) ?? throw new EntityNotFoundException();
        await RequireMemberAsync(task.WorkspaceId, cancellationToken);
        return task.ToDto();
    }

    /// <inheritdoc />
    [HttpPost("")]
    [HasPermission(TasksPermissions.CreateTask)]
    public async Task<Guid> CreateAsync(CreateTaskDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(createValidator, input, cancellationToken);
        var member = await RequireMemberAsync(input.WorkspaceId, cancellationToken);
        if (!member.CanContribute)
        {
            throw new ForbiddenException(TasksErrors.NotWorkspaceMember);
        }

        var task = new TaskItem(Guid.NewGuid(), currentUser.GetRequiredTenantId(), input.WorkspaceId, input.RoomId, null, input.Title.Trim(), Clean(input.Description), input.Priority, input.DueDate, TaskOrigin.Manual);
        if (input.AssigneeUserId is { } assigneeId)
        {
            var assignee = await memberships.Query().FirstOrDefaultAsync(m => m.WorkspaceId == input.WorkspaceId && m.UserId == assigneeId, cancellationToken)
                ?? throw new CustomValidationException(TasksErrors.AssigneeNotMember);
            task.Assign(assignee.UserId, assignee.DisplayName);
        }

        await tasks.InsertAsync(task, autoSave: false, cancellationToken);
        await PublishAssignedAsync(task, member, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return task.Id;
    }

    /// <inheritdoc />
    [HttpPut("")]
    [HasPermission(TasksPermissions.UpdateTask)]
    public async Task UpdateAsync(UpdateTaskDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(updateValidator, input, cancellationToken);
        var task = await LoadTrackedAsync(input.Id, cancellationToken);
        var member = await RequireContributorAsync(task.WorkspaceId, cancellationToken);
        var previousAssignee = task.AssigneeUserId;
        task.Update(input.Title.Trim(), Clean(input.Description), input.Priority, input.DueDate);
        if (input.AssigneeUserId is { } assigneeId)
        {
            var assignee = await memberships.Query().FirstOrDefaultAsync(m => m.WorkspaceId == task.WorkspaceId && m.UserId == assigneeId, cancellationToken)
                ?? throw new CustomValidationException(TasksErrors.AssigneeNotMember);
            task.Assign(assignee.UserId, assignee.DisplayName);
        }
        else
        {
            task.Assign(null, null);
        }

        await tasks.UpdateAsync(task, autoSave: false, cancellationToken);
        if (task.AssigneeUserId != previousAssignee)
        {
            await PublishAssignedAsync(task, member, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    [HttpPost("status")]
    [HasPermission(TasksPermissions.UpdateTask)]
    public async Task SetStatusAsync(SetTaskStatusDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(statusValidator, input, cancellationToken);
        var task = await LoadTrackedAsync(input.Id, cancellationToken);
        await RequireContributorAsync(task.WorkspaceId, cancellationToken);
        task.SetStatus(input.Status);
        await tasks.UpdateAsync(task, autoSave: true, cancellationToken);
    }

    private async Task<TaskItem> LoadTrackedAsync(Guid id, CancellationToken cancellationToken)
        => await tasks.QueryTracked().FirstOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw new EntityNotFoundException();

    private async Task<WorkspaceMembership> RequireMemberAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredId();
        return await memberships.Query().FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken)
            ?? throw new ForbiddenException(TasksErrors.NotWorkspaceMember);
    }

    private async Task<WorkspaceMembership> RequireContributorAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var member = await RequireMemberAsync(workspaceId, cancellationToken);
        if (!member.CanContribute)
        {
            throw new ForbiddenException(TasksErrors.NotWorkspaceMember);
        }

        return member;
    }

    /// <summary>Notifications tells the assignee (FR-NTF-001); nothing is published when the task has no assignee.</summary>
    private async Task PublishAssignedAsync(TaskItem task, WorkspaceMembership by, CancellationToken cancellationToken)
    {
        if (task.AssigneeUserId is { } assignee)
        {
            await publisher.PublishAsync(new TaskAssigned(task.TenantId, task.WorkspaceId, task.RoomId, task.Id, task.Title, assignee, by.UserId, by.DisplayName), cancellationToken);
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
