using FakeItEasy;
using Majlis.Framework.Application.Email;
using Majlis.Framework.Domain.Events;
using Majlis.Notifications.Application.Dispatch;
using Majlis.Notifications.Domain.Constants;
using Majlis.Notifications.Domain.Entities;
using Majlis.Notifications.EntityFrameworkCore;
using Majlis.Notifications.Tests.Application.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Notifications.Tests.Application.TestInfrastructure.NotificationsTestHost;

namespace Majlis.Notifications.Tests.Application.EventHandlers;

public class DigestTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 12, 6, 0, 0, DateTimeKind.Utc);
    private readonly NotificationsTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedAsync(params (Guid UserId, DateTime CreatedAt, bool Read)[] rows)
    {
        using var scope = _host.Scope(Sara);
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        foreach (var (userId, createdAt, read) in rows)
        {
            var n = new Notification(Guid.NewGuid(), TenantId, userId, NotificationTypes.ApprovalRequested, "{}", "/approvals", Guid.NewGuid().ToString("N"), createdAt);
            if (read)
            {
                n.MarkRead(createdAt);
            }

            db.Notifications.Add(n);
        }

        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task DailyDigest_ShouldEmailOnceAboutUnreadRows_InTheRecipientLanguage_AndNotRepeatWithinTheInterval()
    {
        await SeedAsync(
            (Khalid.Id, Now.AddHours(-2), false),
            (Khalid.Id, Now.AddHours(-5), false),
            (Khalid.Id, Now.AddHours(-3), true), // read: not counted
            (Noura.Id, Now.AddHours(-30), false), // older than the window
            (Sara.Id, Now.AddHours(-1), false));
        using (var scope = _host.Scope(Sara))
        {
            // Sara turned email off.
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var sara = new NotificationSettings(TenantId, Sara.Id);
            sara.Update(false, []);
            db.Settings.Add(sara);
            await db.SaveChangesAsync(Ct);
        }

        int sent;
        using (var scope = _host.Scope(Sara))
        {
            sent = await scope.ServiceProvider.GetRequiredService<DigestSender>().SendAsync(Now, Ct);
        }

        Assert.Equal(1, sent);
        A.CallTo(() => _host.Email.SendAsync(A<EmailMessage>.That.Matches(e => e.To == "خالد@demo.local" && e.Subject == "Majlis: your daily digest" && e.TextBody.Contains("2 unread") && e.TextBody.Contains("http://web.local/notifications")), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Email.SendAsync(A<EmailMessage>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        // A repeated tick an hour later sends nothing; the next morning only the rows since the last digest count.
        using (var scope = _host.Scope(Sara))
        {
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DigestSender>().SendAsync(Now.AddHours(1), Ct));
            var pref = await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Settings.SingleAsync(s => s.UserId == Khalid.Id, Ct);
            Assert.Equal(Now, pref.LastDigestAt);
        }

        await SeedAsync((Khalid.Id, Now.AddHours(20), false));
        using (var scope = _host.Scope(Sara))
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DigestSender>().SendAsync(Now.AddHours(24), Ct));
        }

        A.CallTo(() => _host.Email.SendAsync(A<EmailMessage>.That.Matches(e => e.To == "خالد@demo.local" && e.TextBody.Contains("1 unread")), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DailyDigest_ShouldSendNothing_WhenEverythingIsRead()
    {
        await SeedAsync((Khalid.Id, Now.AddHours(-2), true));
        using var scope = _host.Scope(Sara);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DigestSender>().SendAsync(Now, Ct));
        A.CallTo(() => _host.Email.SendAsync(A<EmailMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
