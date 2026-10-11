using System.Reflection;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Events;
using Majlis.Notifications.Application.Dispatch;
using Majlis.Notifications.Application.EventHandlers;
using Majlis.Notifications.Application.Notifications;
using Majlis.Notifications.Application.Notifications.DTOs;
using Majlis.Notifications.Tests.Application.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Notifications.Tests.Application.TestInfrastructure.NotificationsTestHost;

namespace Majlis.Notifications.Tests.Application.AppServices;

public class NotificationsAppServiceTests : IDisposable
{
    private readonly NotificationsTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Endpoints_ShouldRequireAPermission_WhenExposedToUsers()
    {
        var endpoints = typeof(NotificationsAppService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, m => Assert.NotNull(m.GetCustomAttribute<HasPermissionAttribute>()));
    }

    [Fact]
    public async Task MarkRead_ShouldOnlyTouchTheCallersRows_AndUnreadCountFollows()
    {
        await _host.SeedMembershipsAsync();
        using (var scope = _host.Scope(Sara))
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
            for (var i = 0; i < 3; i++)
            {
                await TaskAssignedHandler.Handle(new TaskAssigned(TenantId, WorkspaceId, null, Guid.NewGuid(), $"مهمة {i}", Khalid.Id, Sara.Id, Sara.Name), dispatcher, Ct);
            }

            await TaskAssignedHandler.Handle(new TaskAssigned(TenantId, WorkspaceId, null, Guid.NewGuid(), "لنورة", Noura.Id, Sara.Id, Sara.Name), dispatcher, Ct);
        }

        using var khalid = _host.Scope(Khalid);
        var service = _host.Notifications(khalid);
        Assert.Equal(3, (await service.GetUnreadCountAsync(Ct)).Count);
        var unread = await service.GetListAsync(new FilterNotificationDto { UnreadOnly = true, SkipCount = 0, MaxResultCount = 10 }, Ct);
        Assert.Equal(3, unread.TotalCount);

        using (var noura = _host.Scope(Noura))
        {
            await _host.Notifications(noura).MarkReadAsync(new MarkReadDto { Ids = [unread.Items[0].Id] }, Ct); // not hers: ignored
        }

        Assert.Equal(3, (await service.GetUnreadCountAsync(Ct)).Count);
        await service.MarkReadAsync(new MarkReadDto { Ids = [unread.Items[0].Id] }, Ct);
        Assert.Equal(2, (await service.GetUnreadCountAsync(Ct)).Count);
        await service.MarkAllReadAsync(Ct);
        Assert.Equal(0, (await service.GetUnreadCountAsync(Ct)).Count);
        Assert.Equal(3, (await service.GetListAsync(new FilterNotificationDto { SkipCount = 0, MaxResultCount = 10 }, Ct)).TotalCount);
        Assert.Equal(1, (await _host.Notifications(_host.Scope(Noura)).GetUnreadCountAsync(Ct)).Count);
    }
}
