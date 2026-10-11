using FakeItEasy;
using FluentValidation;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore.Extensions;
using Majlis.Workspaces.Application;
using Majlis.Workspaces.Application.Security;
using Majlis.Workspaces.Application.Workspaces;
using Majlis.Workspaces.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Workspaces.Tests.Application.TestInfrastructure;

/// <summary>A caller whose identity tests can switch.</summary>
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

/// <summary>Workspaces app service over EF Core InMemory with a real in-memory distributed cache and a faked publisher.</summary>
public sealed class WorkspacesTestHost : IDisposable
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly (Guid Id, string Name) Sara = (Guid.NewGuid(), "سارة");
    public static readonly (Guid Id, string Name) Khalid = (Guid.NewGuid(), "خالد");
    public static readonly (Guid Id, string Name) Noura = (Guid.NewGuid(), "نورة");

    private readonly ServiceProvider _provider;

    public WorkspacesTestHost()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddDbContext<WorkspacesDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddMajlisRepositories<WorkspacesDbContext>();
        services.AddDistributedMemoryCache();
        services.AddSingleton(Publisher);
        services.AddValidatorsFromAssembly(typeof(WorkspacesApplicationModule).Assembly);
        services.AddScoped<WorkspacePermissionCache>();
        services.AddScoped<WorkspacesAppService>();
        _provider = services.BuildServiceProvider();
    }

    public IEventPublisher Publisher { get; } = A.Fake<IEventPublisher>();

    public IDistributedCache Cache => _provider.GetRequiredService<IDistributedCache>();

    public IServiceScope Scope((Guid Id, string Name) user)
    {
        var scope = _provider.CreateScope();
        var current = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
        current.Id = user.Id;
        current.TenantId = TenantId;
        current.DisplayName = user.Name;
        return scope;
    }

    public WorkspacesAppService Service(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<WorkspacesAppService>();

    public void Dispose() => _provider.Dispose();
}
