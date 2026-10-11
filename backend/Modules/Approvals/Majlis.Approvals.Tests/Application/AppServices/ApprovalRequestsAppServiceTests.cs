using System.Text.Json;
using FakeItEasy;
using Majlis.Approvals.Application.Background;
using Majlis.Approvals.Application.EventHandlers;
using Majlis.Approvals.Application.Requests.DTOs;
using Majlis.Approvals.Domain.Entities;
using Majlis.Approvals.Domain.Enums;
using Majlis.Approvals.Tests.Application.TestInfrastructure;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Approvals.Tests.Application.TestInfrastructure.ApprovalsTestHost;

namespace Majlis.Approvals.Tests.Application.AppServices;

public class ApprovalRequestsAppServiceTests : IDisposable
{
    private readonly ApprovalsTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private static CreateApprovalRequestDto CreateTask(string tool = "create_task") => new()
    {
        WorkspaceId = WorkspaceId, RoomId = RoomId, SessionId = SessionId, Tool = tool,
        Args = JsonSerializer.SerializeToElement(new { title = "مراجعة العقد", priority = "High" }), Summary = "إنشاء مهمة: مراجعة العقد", Reason = "طلب في الاجتماع",
    };

    private static string? TitleOf(string argsJson)
    {
        using var doc = JsonDocument.Parse(argsJson);
        return doc.RootElement.GetProperty("title").GetString();
    }

    private async Task<Guid> RequestedByAsync((Guid Id, string Name) user, string tool = "create_task")
    {
        using var scope = _host.Scope(user);
        return await _host.Requests(scope).CreateAsync(CreateTask(tool), Ct);
    }

    [Fact]
    public async Task CreateAsync_ShouldStorePendingRequestAndPublish_WhenToolIsKnown()
    {
        await _host.SeedMembershipsAsync();

        var id = await RequestedByAsync(Khalid);

        using var scope = _host.Scope(Khalid);
        var dto = await _host.Requests(scope).GetAsync(new ApprovalRequestIdDto(id), Ct);
        Assert.Equal(ApprovalStatus.Pending, dto.Status);
        Assert.Equal(RiskLevel.Low, dto.Risk);
        Assert.Equal(Khalid.Id, dto.RequestedBy.UserId);
        Assert.Equal(_host.Clock.GetUtcNow().UtcDateTime.AddHours(24), dto.ExpiresAt);
        Assert.True(dto.CanDecide);
        A.CallTo(() => _host.Publisher.PublishAsync(A<ApprovalRequested>.That.Matches(e => e.RequestId == id && e.Tool == "create_task"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateAsync_ShouldReject_WhenToolUnknownOrUserNotMember()
    {
        await _host.SeedMembershipsAsync();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => RequestedByAsync(Khalid, "delete_everything"));
        await Assert.ThrowsAsync<ForbiddenException>(() => RequestedByAsync(Outsider));
    }

    [Fact]
    public async Task ApproveAsync_ShouldPublishActionApprovedWithEditedArgs_WhenContributorApprovesLowRisk()
    {
        await _host.SeedMembershipsAsync();
        var id = await RequestedByAsync(Khalid);
        var edited = JsonSerializer.SerializeToElement(new { title = "مراجعة عقد المورد" });

        using var scope = _host.Scope(Sara);
        var dto = await _host.Requests(scope).ApproveAsync(new ApproveRequestDto { Id = id, EditedArgs = edited, Note = "عدلت العنوان" }, Ct);

        Assert.Equal(ApprovalStatus.Approved, dto.Status);
        Assert.Equal(Sara.Id, dto.DecidedBy!.UserId);
        Assert.NotNull(dto.EditedArgs);
        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionApproved>.That.Matches(e => e.RequestId == id && TitleOf(e.ArgsJson) == "مراجعة عقد المورد"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<ApprovalDecided>.That.Matches(e => e.RequestId == id && e.Decision == "Approved"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ApproveAsync_ShouldForbid_WhenViewerOrSelfOnMediumRisk()
    {
        await _host.SeedMembershipsAsync();
        var low = await RequestedByAsync(Khalid);
        var medium = await RequestedByAsync(Sara, "update_task");

        using (var scope = _host.Scope(Noura))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Requests(scope).ApproveAsync(new ApproveRequestDto { Id = low }, Ct));
        }

        using (var scope = _host.Scope(Sara))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Requests(scope).ApproveAsync(new ApproveRequestDto { Id = medium }, Ct));
        }

        using (var scope = _host.Scope(Khalid))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Requests(scope).ApproveAsync(new ApproveRequestDto { Id = medium }, Ct));
        }

        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionApproved>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RejectAsync_ShouldPublishActionRejected_AndBlockALaterApproval()
    {
        await _host.SeedMembershipsAsync();
        var id = await RequestedByAsync(Khalid);

        using (var scope = _host.Scope(Sara))
        {
            var dto = await _host.Requests(scope).RejectAsync(new RejectRequestDto { Id = id, Reason = "ليست أولوية" }, Ct);
            Assert.Equal(ApprovalStatus.Rejected, dto.Status);
            Assert.Equal("ليست أولوية", dto.DecisionNote);
        }

        using (var scope = _host.Scope(Sara))
        {
            await Assert.ThrowsAsync<ConflictException>(() => _host.Requests(scope).ApproveAsync(new ApproveRequestDto { Id = id }, Ct));
        }

        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionRejected>.That.Matches(e => e.RequestId == id), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetListAsync_ShouldReturnOnlyDecidableRequests_WhenForMe()
    {
        await _host.SeedMembershipsAsync();
        var low = await RequestedByAsync(Khalid);
        var medium = await RequestedByAsync(Sara, "update_task");

        using (var scope = _host.Scope(Sara))
        {
            var inbox = await _host.Requests(scope).GetListAsync(new FilterApprovalRequestDto { ForMe = true }, Ct);
            Assert.Equal([low], inbox.Items.Select(i => i.Id));
        }

        using (var scope = _host.Scope(Noura))
        {
            var all = await _host.Requests(scope).GetListAsync(new FilterApprovalRequestDto(), Ct);
            Assert.Equal(2, all.TotalCount);
            Assert.All(all.Items, i => Assert.False(i.CanDecide));
            var inbox = await _host.Requests(scope).GetListAsync(new FilterApprovalRequestDto { ForMe = true }, Ct);
            Assert.Empty(inbox.Items);
        }

        using (var scope = _host.Scope(Outsider))
        {
            var none = await _host.Requests(scope).GetListAsync(new FilterApprovalRequestDto(), Ct);
            Assert.Equal(0, none.TotalCount);
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Requests(scope).GetAsync(new ApprovalRequestIdDto(medium), Ct));
        }
    }

    [Fact]
    public async Task ExpirySweeper_ShouldExpirePendingRequests_WhenPastDue()
    {
        await _host.SeedMembershipsAsync();
        var id = await RequestedByAsync(Khalid);
        var sweeper = _host.Services.GetRequiredService<ExpirySweeper>();

        Assert.Equal(0, await sweeper.SweepOnceAsync(Ct));
        _host.Clock.Advance(TimeSpan.FromHours(25));
        Assert.Equal(1, await sweeper.SweepOnceAsync(Ct));
        Assert.Equal(0, await sweeper.SweepOnceAsync(Ct));

        using var scope = _host.Scope(Sara);
        var dto = await _host.Requests(scope).GetAsync(new ApprovalRequestIdDto(id), Ct);
        Assert.Equal(ApprovalStatus.Expired, dto.Status);
        Assert.False(dto.CanDecide);
        await Assert.ThrowsAsync<ConflictException>(() => _host.Requests(scope).ApproveAsync(new ApproveRequestDto { Id = id }, Ct));
        A.CallTo(() => _host.Publisher.PublishAsync(A<ApprovalDecided>.That.Matches(e => e.RequestId == id && e.Decision == "Expired"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ActionExecutedHandler_ShouldMarkExecutedOnce_WhenDeliveredTwice()
    {
        await _host.SeedMembershipsAsync();
        var id = await RequestedByAsync(Khalid);
        using (var scope = _host.Scope(Sara))
        {
            await _host.Requests(scope).ApproveAsync(new ApproveRequestDto { Id = id }, Ct);
        }

        var entityId = Guid.NewGuid();
        for (var i = 0; i < 2; i++)
        {
            using var scope = _host.Scope(Sara);
            var sp = scope.ServiceProvider;
            await ActionExecutedHandler.Handle(
                new ActionExecuted(TenantId, id, "create_task", entityId, "مراجعة العقد"),
                sp.GetRequiredService<IRepository<ApprovalRequest, Guid>>(), sp.GetRequiredService<IEventPublisher>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
        }

        using var check = _host.Scope(Sara);
        var dto = await _host.Requests(check).GetAsync(new ApprovalRequestIdDto(id), Ct);
        Assert.Equal(ApprovalStatus.Executed, dto.Status);
        Assert.Equal(entityId, dto.ResultEntityId);
        A.CallTo(() => _host.Publisher.PublishAsync(A<ApprovalExecuted>.That.Matches(e => e.RequestId == id && e.Succeeded), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ActionExecutionFailedHandler_ShouldMarkFailed_WhenOwningModuleReportsFailure()
    {
        await _host.SeedMembershipsAsync();
        var id = await RequestedByAsync(Khalid);
        using (var scope = _host.Scope(Sara))
        {
            await _host.Requests(scope).ApproveAsync(new ApproveRequestDto { Id = id }, Ct);
        }

        using (var scope = _host.Scope(Sara))
        {
            var sp = scope.ServiceProvider;
            await ActionExecutionFailedHandler.Handle(
                new ActionExecutionFailed(TenantId, id, "create_task", "General:Fields:Required", "title"),
                sp.GetRequiredService<IRepository<ApprovalRequest, Guid>>(), sp.GetRequiredService<IEventPublisher>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
        }

        using var check = _host.Scope(Sara);
        var dto = await _host.Requests(check).GetAsync(new ApprovalRequestIdDto(id), Ct);
        Assert.Equal(ApprovalStatus.Failed, dto.Status);
        Assert.Equal("General:Fields:Required", dto.FailureReasonKey);
        A.CallTo(() => _host.Publisher.PublishAsync(A<ApprovalExecuted>.That.Matches(e => e.RequestId == id && !e.Succeeded), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
