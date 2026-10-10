using FakeItEasy;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Tasks.Application.EventHandlers;
using Majlis.Tasks.Application.Tasks.DTOs;
using Majlis.Tasks.Domain.Entities;
using Majlis.Tasks.Domain.Enums;
using Majlis.Tasks.Tests.Application.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Tasks.Tests.Application.TestInfrastructure.TasksTestHost;

namespace Majlis.Tasks.Tests.Application.EventHandlers;

public class ActionApprovedHandlerTests : IDisposable
{
    private readonly TasksTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ActionApproved Approved(Guid requestId, (Guid Id, string Name) requester, string args, string tool = "create_task")
        => new(TenantId, WorkspaceId, RoomId, SessionId, Guid.NewGuid(), requestId, tool, args, requester.Id, requester.Name, Sara.Id, Sara.Name);

    private async Task HandleAsync(ActionApproved message)
    {
        using var scope = _host.Scope(Sara);
        var sp = scope.ServiceProvider;
        await ActionApprovedHandler.Handle(
            message,
            sp.GetRequiredService<IRepository<TaskItem, Guid>>(),
            sp.GetRequiredService<IReadOnlyRepository<WorkspaceMembership, Guid>>(),
            sp.GetRequiredService<IEventPublisher>(),
            sp.GetRequiredService<IUnitOfWork>(),
            Ct);
    }

    [Fact]
    public async Task Handle_ShouldCreateAgentTaskOnce_WhenDeliveredTwice()
    {
        await _host.SeedMembershipsAsync();
        var requestId = Guid.NewGuid();
        var message = Approved(requestId, Khalid, """{"title":"مراجعة العقد","description":"قبل الاجتماع","assigneeName":"سارة","dueDate":"2026-11-01","priority":"high"}""");

        await HandleAsync(message);
        await HandleAsync(message);

        using var scope = _host.Scope(Khalid);
        var list = await _host.Tasks(scope).GetListAsync(new FilterTaskDto(), Ct);
        var task = Assert.Single(list.Items);
        Assert.Equal("مراجعة العقد", task.Title);
        Assert.Equal(TaskOrigin.Agent, task.Origin);
        Assert.Equal(requestId, task.ApprovalRequestId);
        Assert.Equal(Sara.Id, task.AssigneeUserId);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(new DateOnly(2026, 11, 1), task.DueDate);
        Assert.Equal(SessionId, task.SessionId);
        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionExecuted>.That.Matches(e => e.RequestId == requestId && e.EntityId == task.Id), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_ShouldReportFailure_WhenRequesterCannotContributeOrTitleMissing()
    {
        await _host.SeedMembershipsAsync();
        var byViewer = Guid.NewGuid();
        var noTitle = Guid.NewGuid();

        await HandleAsync(Approved(byViewer, Noura, """{"title":"x"}"""));
        await HandleAsync(Approved(noTitle, Khalid, """{"description":"بدون عنوان"}"""));
        await HandleAsync(Approved(Guid.NewGuid(), Khalid, """{"title":"x"}""", tool: "draft_document"));

        using var scope = _host.Scope(Sara);
        Assert.Equal(0, (await _host.Tasks(scope).GetListAsync(new FilterTaskDto(), Ct)).TotalCount);
        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionExecutionFailed>.That.Matches(e => e.RequestId == byViewer), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionExecutionFailed>.That.Matches(e => e.RequestId == noTitle && e.Detail == "title"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionExecuted>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
