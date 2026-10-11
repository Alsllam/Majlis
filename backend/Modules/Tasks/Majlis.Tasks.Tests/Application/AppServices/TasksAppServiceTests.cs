using Majlis.Framework.Domain.Exceptions;
using Majlis.Tasks.Application.Tasks.DTOs;
using Majlis.Tasks.Domain.Enums;
using Majlis.Tasks.Tests.Application.TestInfrastructure;
using TaskStatus = Majlis.Tasks.Domain.Enums.TaskStatus;
using static Majlis.Tasks.Tests.Application.TestInfrastructure.TasksTestHost;

namespace Majlis.Tasks.Tests.Application.AppServices;

public class TasksAppServiceTests : IDisposable
{
    private readonly TasksTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private static CreateTaskDto Create(Guid? assignee = null) => new()
    {
        WorkspaceId = WorkspaceId, RoomId = RoomId, Title = "  مراجعة العقد  ", Description = " ", AssigneeUserId = assignee, Priority = TaskPriority.High, DueDate = new DateOnly(2026, 11, 1),
    };

    [Fact]
    public async Task CreateAsync_ShouldTrimAndAssign_WhenContributorCreates()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Khalid);

        var id = await _host.Tasks(scope).CreateAsync(Create(Sara.Id), Ct);

        var dto = await _host.Tasks(scope).GetAsync(new TaskIdDto(id), Ct);
        Assert.Equal("مراجعة العقد", dto.Title);
        Assert.Null(dto.Description);
        Assert.Equal(Sara.Name, dto.AssigneeDisplayName);
        Assert.Equal(TaskStatus.ToDo, dto.Status);
        Assert.Equal(TaskOrigin.Manual, dto.Origin);
        Assert.Equal(Khalid.Id, dto.CreatorId);
    }

    [Fact]
    public async Task CreateAsync_ShouldForbidViewersAndOutsiders_AndRejectNonMemberAssignee()
    {
        await _host.SeedMembershipsAsync();

        using (var scope = _host.Scope(Noura))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Tasks(scope).CreateAsync(Create(), Ct));
        }

        using (var scope = _host.Scope(Outsider))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Tasks(scope).CreateAsync(Create(), Ct));
        }

        using (var scope = _host.Scope(Khalid))
        {
            await Assert.ThrowsAsync<CustomValidationException>(() => _host.Tasks(scope).CreateAsync(Create(Outsider.Id), Ct));
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _host.Tasks(scope).CreateAsync(Create() with { Title = " " }, Ct));
        }
    }

    [Fact]
    public async Task SetStatusAsync_ShouldChangeStatus_AndListFiltersByIt()
    {
        await _host.SeedMembershipsAsync();
        Guid id;
        using (var scope = _host.Scope(Khalid))
        {
            id = await _host.Tasks(scope).CreateAsync(Create(), Ct);
            await _host.Tasks(scope).CreateAsync(Create() with { Title = "أخرى" }, Ct);
            await _host.Tasks(scope).SetStatusAsync(new SetTaskStatusDto { Id = id, Status = TaskStatus.Done }, Ct);
        }

        using (var scope = _host.Scope(Noura))
        {
            var done = await _host.Tasks(scope).GetListAsync(new FilterTaskDto { WorkspaceId = WorkspaceId, Status = TaskStatus.Done }, Ct);
            Assert.Equal([id], done.Items.Select(t => t.Id));
            var all = await _host.Tasks(scope).GetListAsync(new FilterTaskDto { FilterText = "أخرى" }, Ct);
            Assert.Equal(1, all.TotalCount);
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Tasks(scope).SetStatusAsync(new SetTaskStatusDto { Id = id, Status = TaskStatus.ToDo }, Ct));
        }

        using (var scope = _host.Scope(Outsider))
        {
            Assert.Equal(0, (await _host.Tasks(scope).GetListAsync(new FilterTaskDto(), Ct)).TotalCount);
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Tasks(scope).GetAsync(new TaskIdDto(id), Ct));
        }
    }

    [Fact]
    public async Task UpdateAsync_ShouldReplaceFieldsAndClearAssignee_WhenNoneGiven()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Sara);
        var id = await _host.Tasks(scope).CreateAsync(Create(Khalid.Id), Ct);

        await _host.Tasks(scope).UpdateAsync(new UpdateTaskDto { Id = id, Title = "عنوان جديد", Description = "تفاصيل", Priority = TaskPriority.Low }, Ct);

        var dto = await _host.Tasks(scope).GetAsync(new TaskIdDto(id), Ct);
        Assert.Equal("عنوان جديد", dto.Title);
        Assert.Equal("تفاصيل", dto.Description);
        Assert.Equal(TaskPriority.Low, dto.Priority);
        Assert.Null(dto.AssigneeUserId);
        Assert.Null(dto.DueDate);
    }
}
