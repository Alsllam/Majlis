using FakeItEasy;
using FluentValidation;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore.Extensions;
using Majlis.Tasks.Application;
using Majlis.Tasks.Application.Tasks;
using Majlis.Tasks.Domain.Entities;
using Majlis.Tasks.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Tasks.Tests.Application.TestInfrastructure;

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

/// <summary>Tasks app service over EF Core InMemory with the publisher faked.</summary>
public sealed class TasksTestHost : IDisposable
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

    public TasksTestHost()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddDbContext<TasksDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddMajlisRepositories<TasksDbContext>();
        services.AddSingleton(Publisher);
        services.AddValidatorsFromAssembly(typeof(TasksApplicationModule).Assembly);
        services.AddScoped<TasksAppService>();
        _provider = services.BuildServiceProvider();
    }

    public IEventPublisher Publisher { get; } = A.Fake<IEventPublisher>();

    public IServiceScope Scope((Guid Id, string Name) user)
    {
        var scope = _provider.CreateScope();
        var current = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
        current.Id = user.Id;
        current.TenantId = TenantId;
        current.DisplayName = user.Name;
        return scope;
    }

    public TasksAppService Tasks(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<TasksAppService>();

    /// <summary>Sara owns the workspace, Khalid contributes, Noura only watches.</summary>
    public async Task SeedMembershipsAsync()
    {
        using var scope = Scope(Sara);
        var db = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
        db.WorkspaceMemberships.AddRange(
            new WorkspaceMembership(TenantId, WorkspaceId, Sara.Id, Sara.Name, "Owner"),
            new WorkspaceMembership(TenantId, WorkspaceId, Khalid.Id, Khalid.Name, "Contributor"),
            new WorkspaceMembership(TenantId, WorkspaceId, Noura.Id, Noura.Name, "Viewer"));
        await db.SaveChangesAsync();
    }

    public void Dispose() => _provider.Dispose();
}
