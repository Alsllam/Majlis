using FakeItEasy;
using Majlis.Framework.Application.Email;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Notifications.Application.Dispatch;
using Majlis.Notifications.Application.EventHandlers;
using Majlis.Notifications.Application.Notifications.DTOs;
using Majlis.Notifications.Domain.Constants;
using Majlis.Notifications.Domain.Entities;
using Majlis.Notifications.Tests.Application.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Notifications.Tests.Application.TestInfrastructure.NotificationsTestHost;

namespace Majlis.Notifications.Tests.Application.EventHandlers;

public class ApprovalNotificationTests : IDisposable
{
    private readonly NotificationsTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ApprovalRequested Requested(Guid requestId, (Guid Id, string Name) by, string risk = "Low")
        => new(TenantId, WorkspaceId, RoomId, SessionId, null, requestId, "create_task", "إنشاء مهمة: مراجعة العقد", null, risk, "{}", by.Id, by.Name, DateTime.UtcNow.AddDays(1));

    private async Task HandleRequestedAsync(ApprovalRequested m)
    {
        using var scope = _host.Scope(Sara);
        var sp = scope.ServiceProvider;
        await ApprovalRequestedHandler.Handle(m, sp.GetRequiredService<IRepository<ApprovalRef, Guid>>(), sp.GetRequiredService<IReadOnlyRepository<WorkspaceMembership, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<NotificationDispatcher>(), Ct);
    }

    private async Task<List<NotificationDto>> InboxOf((Guid Id, string Name) user)
    {
        using var scope = _host.Scope(user);
        return (await _host.Notifications(scope).GetListAsync(new FilterNotificationDto { SkipCount = 0, MaxResultCount = 50 }, Ct)).Items.ToList();
    }

    [Fact]
    public async Task ApprovalRequested_ShouldNotifyApproversOnce_AndEmailThemInTheirLanguage()
    {
        await _host.SeedMembershipsAsync();
        var requestId = Guid.NewGuid();

        await HandleRequestedAsync(Requested(requestId, Sara));
        await HandleRequestedAsync(Requested(requestId, Sara)); // redelivered

        // Low risk: contributors and above other than the requester → Khalid only (Noura watches, Sara asked).
        var khalid = Assert.Single(await InboxOf(Khalid));
        Assert.Equal(NotificationTypes.ApprovalRequested, khalid.Type);
        Assert.Equal("سارة", khalid.Params.GetProperty("name").GetString());
        Assert.Equal($"/rooms/{RoomId}", khalid.Link);
        Assert.Empty(await InboxOf(Sara));
        Assert.Empty(await InboxOf(Noura));
        A.CallTo(() => _host.Publisher.PublishAsync(A<NotificationCreated>.That.Matches(n => n.UserId == Khalid.Id), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Email.SendAsync(A<EmailMessage>.That.Matches(e => e.To == "خالد@demo.local" && e.Subject.StartsWith("Majlis:") && e.TextBody.Contains("http://web.local/rooms/")), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ApprovalRequested_ShouldNotifyOnlyManagers_WhenRiskIsMedium()
    {
        await _host.SeedMembershipsAsync();

        await HandleRequestedAsync(Requested(Guid.NewGuid(), Khalid, "Medium"));

        Assert.Single(await InboxOf(Sara));
        Assert.Empty(await InboxOf(Khalid));
    }

    [Fact]
    public async Task ApprovalDecidedAndExecuted_ShouldGoBackToTheRequester_ExceptSelfDecisionsAndExpiry()
    {
        await _host.SeedMembershipsAsync();
        var requestId = Guid.NewGuid();
        await HandleRequestedAsync(Requested(requestId, Khalid));

        using (var scope = _host.Scope(Sara))
        {
            var sp = scope.ServiceProvider;
            var refs = sp.GetRequiredService<IReadOnlyRepository<ApprovalRef, Guid>>();
            var dispatcher = sp.GetRequiredService<NotificationDispatcher>();
            await ApprovalDecidedHandler.Handle(new ApprovalDecided(TenantId, SessionId, requestId, "create_task", "Approved", Sara.Id, Sara.Name, null, null), refs, dispatcher, Ct);
            await ApprovalDecidedHandler.Handle(new ApprovalDecided(TenantId, SessionId, requestId, "create_task", "Approved", Khalid.Id, Khalid.Name, null, null), refs, dispatcher, Ct); // self
            await ApprovalDecidedHandler.Handle(new ApprovalDecided(TenantId, SessionId, Guid.NewGuid(), "create_task", "Expired", null, null, null, null), refs, dispatcher, Ct);
            await ApprovalExecutedHandler.Handle(new ApprovalExecuted(TenantId, SessionId, requestId, "create_task", true, "مراجعة العقد", Guid.NewGuid(), null), refs, dispatcher, Ct);
        }

        var inbox = await InboxOf(Khalid);
        Assert.Equal([NotificationTypes.ApprovalExecuted, NotificationTypes.ApprovalDecided], inbox.Select(n => n.Type));
        Assert.Equal("Approved", inbox[1].Params.GetProperty("decision").GetString());
        A.CallTo(() => _host.Email.SendAsync(A<EmailMessage>.That.Matches(e => e.Subject == "Majlis: your request was decided"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task TaskAssignedAndHandOff_ShouldNotifyTheirTargets_UnlessSelfAssigned()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Sara);
        var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
        var taskId = Guid.NewGuid();

        await TaskAssignedHandler.Handle(new TaskAssigned(TenantId, WorkspaceId, RoomId, taskId, "مراجعة العقد", Khalid.Id, Sara.Id, Sara.Name), dispatcher, Ct);
        await TaskAssignedHandler.Handle(new TaskAssigned(TenantId, WorkspaceId, RoomId, Guid.NewGuid(), "لنفسي", Sara.Id, Sara.Id, Sara.Name), dispatcher, Ct);
        await SessionEventAppendedHandler.Handle(
            new SessionEventAppended(TenantId, WorkspaceId, RoomId, SessionId, 7, "handoff.offered", null, DateTime.UtcNow, "user", Sara.Id, Sara.Name, $$"""{"to":{"userId":"{{Khalid.Id}}","displayName":"خالد"},"note":null}"""),
            dispatcher, Ct);
        await SessionEventAppendedHandler.Handle(
            new SessionEventAppended(TenantId, WorkspaceId, RoomId, SessionId, 8, "turn.started", null, DateTime.UtcNow, "user", Sara.Id, Sara.Name, "{}"), dispatcher, Ct);

        var inbox = await InboxOf(Khalid);
        Assert.Equal([NotificationTypes.HandOffOffered, NotificationTypes.TaskAssigned], inbox.Select(n => n.Type));
        Assert.Equal($"/tasks?workspaceId={WorkspaceId}", inbox[1].Link);
        Assert.Empty(await InboxOf(Sara));
    }

    [Fact]
    public async Task Dispatcher_ShouldHonourMutedTypesAndEmailPreference()
    {
        await _host.SeedMembershipsAsync();
        using (var scope = _host.Scope(Khalid))
        {
            await _host.Notifications(scope).UpdateSettingsAsync(new UpdateNotificationSettingsDto { EmailEnabled = false, MutedTypes = [NotificationTypes.TaskAssigned, "bogus"] }, Ct);
            var settings = await _host.Notifications(scope).GetSettingsAsync(Ct);
            Assert.False(settings.EmailEnabled);
            Assert.Equal([NotificationTypes.TaskAssigned], settings.MutedTypes);
        }

        using (var scope = _host.Scope(Sara))
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
            await TaskAssignedHandler.Handle(new TaskAssigned(TenantId, WorkspaceId, RoomId, Guid.NewGuid(), "مكتومة", Khalid.Id, Sara.Id, Sara.Name), dispatcher, Ct);
        }

        await HandleRequestedAsync(Requested(Guid.NewGuid(), Sara));

        var inbox = await InboxOf(Khalid);
        Assert.Equal([NotificationTypes.ApprovalRequested], inbox.Select(n => n.Type));
        A.CallTo(() => _host.Email.SendAsync(A<EmailMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Dispatcher_ShouldKeepTheNotification_WhenEmailFails()
    {
        await _host.SeedMembershipsAsync();
        A.CallTo(() => _host.Email.SendAsync(A<EmailMessage>._, A<CancellationToken>._)).Throws(new InvalidOperationException("smtp down"));

        await HandleRequestedAsync(Requested(Guid.NewGuid(), Sara));

        Assert.Single(await InboxOf(Khalid));
    }
}
