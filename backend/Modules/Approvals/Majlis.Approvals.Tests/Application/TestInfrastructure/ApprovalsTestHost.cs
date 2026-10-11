using FakeItEasy;
using FluentValidation;
using Majlis.Approvals.Application;
using Majlis.Approvals.Application.Requests;
using Majlis.Approvals.Domain.Entities;
using Majlis.Approvals.EntityFrameworkCore;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Majlis.Approvals.Tests.Application.TestInfrastructure;

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

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

/// <summary>Approval requests app service over EF Core InMemory with the publisher faked and a controllable clock.</summary>
public sealed class ApprovalsTestHost : IDisposable
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid WorkspaceId = Guid.NewGuid();
    public static readonly Guid RoomId = Guid.NewGuid();
    public static readonly Guid SessionId = Guid.NewGuid();
    public static readonly (Guid Id, string Name) Sara = (Guid.NewGuid(), "سارة");
    public static readonly (Guid Id, string Name) Khalid = (Guid.NewGuid(), "خالد");
    public static readonly (Guid Id, string Name) Noura = (Guid.NewGuid(), "نورة");
    public static readonly (Guid Id, string Name) Outsider = (Guid.NewGuid(), "زائر");

    private readonly ServiceProvider _provider;

    public ApprovalsTestHost()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddDbContext<ApprovalsDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddMajlisRepositories<ApprovalsDbContext>();
        services.AddSingleton(Publisher);
        services.AddSingleton(Options.Create(new ApprovalsOptions { ExpiryHours = 24 }));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddValidatorsFromAssembly(typeof(ApprovalsApplicationModule).Assembly);
        services.AddScoped<ApprovalRequestsAppService>();
        services.AddApprovalsBackgroundServices();
        _provider = services.BuildServiceProvider();
    }

    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));

    public IEventPublisher Publisher { get; } = A.Fake<IEventPublisher>();

    public IServiceProvider Services => _provider;

    public IServiceScope Scope((Guid Id, string Name) user)
    {
        var scope = _provider.CreateScope();
        var current = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
        current.Id = user.Id;
        current.TenantId = TenantId;
        current.DisplayName = user.Name;
        return scope;
    }

    public ApprovalRequestsAppService Requests(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApprovalRequestsAppService>();

    /// <summary>Sara owns the workspace, Khalid contributes, Noura only watches.</summary>
    public async Task SeedMembershipsAsync()
    {
        using var scope = Scope(Sara);
        var db = scope.ServiceProvider.GetRequiredService<ApprovalsDbContext>();
        db.WorkspaceMemberships.AddRange(
            new WorkspaceMembership(TenantId, WorkspaceId, Sara.Id, Sara.Name, "Owner"),
            new WorkspaceMembership(TenantId, WorkspaceId, Khalid.Id, Khalid.Name, "Contributor"),
            new WorkspaceMembership(TenantId, WorkspaceId, Noura.Id, Noura.Name, "Viewer"));
        await db.SaveChangesAsync();
    }

    public void Dispose() => _provider.Dispose();
}
