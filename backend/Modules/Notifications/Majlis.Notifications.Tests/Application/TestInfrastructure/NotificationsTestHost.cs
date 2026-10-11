using FakeItEasy;
using Majlis.Framework.Application.Email;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore.Extensions;
using Majlis.Notifications.Application;
using Majlis.Notifications.Application.Dispatch;
using Majlis.Notifications.Application.Notifications;
using Majlis.Notifications.Domain.Entities;
using Majlis.Notifications.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Majlis.Notifications.Tests.Application.TestInfrastructure;

public sealed class TestCurrentUser : ICurrentUser
{
    public Guid? Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? DisplayName { get; set; }
    public bool IsAuthenticated => Id is not null;
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public Guid GetRequiredId() => Id ?? throw new InvalidOperationException();
    public Guid GetRequiredTenantId() => TenantId ?? throw new InvalidOperationException();
}

/// <summary>Dispatcher, handlers and app service over EF Core InMemory; publisher, email and the identity lookup are faked.</summary>
public sealed class NotificationsTestHost : IDisposable
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid WorkspaceId = Guid.NewGuid();
    public static readonly Guid RoomId = Guid.NewGuid();
    public static readonly Guid SessionId = Guid.NewGuid();
    public static readonly (Guid Id, string Name) Sara = (Guid.NewGuid(), "سارة");
    public static readonly (Guid Id, string Name) Khalid = (Guid.NewGuid(), "خالد");
    public static readonly (Guid Id, string Name) Noura = (Guid.NewGuid(), "نورة");

    private readonly ServiceProvider _provider;

    public NotificationsTestHost()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddDbContext<NotificationsDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddMajlisRepositories<NotificationsDbContext>();
        services.AddSingleton(Publisher);
        services.AddSingleton(Email);
        services.AddSingleton(Identity);
        services.AddSingleton(Options.Create(new NotificationsOptions { WebBaseUrl = "http://web.local" }));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<NotificationsAppService>();
        _provider = services.BuildServiceProvider();

        A.CallTo(() => Identity.LookupByIdsAsync(A<InternalUsersQueryDto>._, A<CancellationToken>._))
            .ReturnsLazily((InternalUsersQueryDto q, CancellationToken _) =>
                new[] { Sara, Khalid, Noura }.Where(u => q.Ids.Contains(u.Id))
                    .Select(u => new InternalUserDto(u.Id, u.Name, $"{u.Name}@demo.local", u == Khalid ? "en" : "ar", true)).ToList());
    }

    public IEventPublisher Publisher { get; } = A.Fake<IEventPublisher>();

    public IEmailSender Email { get; } = A.Fake<IEmailSender>();

    public IIdentityInternalClient Identity { get; } = A.Fake<IIdentityInternalClient>();

    public IServiceScope Scope((Guid Id, string Name) user)
    {
        var scope = _provider.CreateScope();
        var current = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
        current.Id = user.Id;
        current.TenantId = TenantId;
        current.DisplayName = user.Name;
        return scope;
    }

    public NotificationsAppService Notifications(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<NotificationsAppService>();

    /// <summary>Sara owns the workspace, Khalid contributes, Noura only watches.</summary>
    public async Task SeedMembershipsAsync()
    {
        using var scope = Scope(Sara);
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        db.WorkspaceMemberships.AddRange(
            new WorkspaceMembership(TenantId, WorkspaceId, Sara.Id, Sara.Name, "Owner"),
            new WorkspaceMembership(TenantId, WorkspaceId, Khalid.Id, Khalid.Name, "Contributor"),
            new WorkspaceMembership(TenantId, WorkspaceId, Noura.Id, Noura.Name, "Viewer"));
        await db.SaveChangesAsync();
    }

    public void Dispose() => _provider.Dispose();
}
