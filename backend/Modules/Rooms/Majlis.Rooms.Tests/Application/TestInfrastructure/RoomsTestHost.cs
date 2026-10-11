using FakeItEasy;
using FluentValidation;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore.Extensions;
using Majlis.Rooms.Application;
using Majlis.Rooms.Application.Ai;
using Majlis.Rooms.Application.Rooms;
using Majlis.Rooms.Application.Sessions;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;
using Majlis.Rooms.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Majlis.Rooms.Tests.Application.TestInfrastructure;

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

/// <summary>
/// Builds the Rooms application services over EF Core InMemory, with ai-service, the event publisher and Redis faked.
/// One instance per test; each <see cref="Scope"/> mimics one HTTP request.
/// </summary>
public sealed class RoomsTestHost : IDisposable
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid WorkspaceId = Guid.NewGuid();
    public static readonly (Guid Id, string Name) Sara = (Guid.NewGuid(), "سارة");
    public static readonly (Guid Id, string Name) Khalid = (Guid.NewGuid(), "خالد");
    public static readonly (Guid Id, string Name) Noura = (Guid.NewGuid(), "نورة");
    public static readonly (Guid Id, string Name) Outsider = (Guid.NewGuid(), "زائر");

    private readonly ServiceProvider _provider;

    public RoomsTestHost()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddDbContext<RoomsDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddMajlisRepositories<RoomsDbContext>();
        services.AddScoped<SessionTimeline>();
        services.AddSingleton(Publisher);
        services.AddSingleton(AiClient);
        services.AddSingleton(Signals);
        services.AddValidatorsFromAssembly(typeof(RoomsApplicationModule).Assembly);
        services.AddSingleton(Options.Create(new RoomsOptions()));
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddScoped<SessionsAppService>();
        services.AddScoped<RoomsAppService>();
        _provider = services.BuildServiceProvider();
    }

    public IEventPublisher Publisher { get; } = A.Fake<IEventPublisher>();

    public IAiTurnClient AiClient { get; } = A.Fake<IAiTurnClient>();

    public ITurnSignals Signals { get; } = A.Fake<ITurnSignals>();

    public IServiceScope Scope((Guid Id, string Name) user)
    {
        var scope = _provider.CreateScope();
        var current = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
        current.Id = user.Id;
        current.TenantId = TenantId;
        current.DisplayName = user.Name;
        return scope;
    }

    public SessionsAppService Sessions(IServiceScope scope)
    {
        var service = scope.ServiceProvider.GetRequiredService<SessionsAppService>();
        service.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        service.ControllerContext.HttpContext.Request.Headers.Authorization = "Bearer test-token";
        return service;
    }

    public RoomsAppService Rooms(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<RoomsAppService>();

    /// <summary>Workspace membership read model: Sara and Khalid contribute, Noura only watches; the outsider is no member.</summary>
    public async Task SeedMembershipsAsync()
    {
        using var scope = Scope(Sara);
        var db = scope.ServiceProvider.GetRequiredService<RoomsDbContext>();
        if (await db.WorkspaceMemberships.AnyAsync(m => m.WorkspaceId == WorkspaceId))
        {
            return;
        }

        db.WorkspaceMemberships.AddRange(
            new WorkspaceMembership(TenantId, WorkspaceId, Sara.Id, Sara.Name, "Owner"),
            new WorkspaceMembership(TenantId, WorkspaceId, Khalid.Id, Khalid.Name, "Contributor"),
            new WorkspaceMembership(TenantId, WorkspaceId, Noura.Id, Noura.Name, "Viewer"));
        await db.SaveChangesAsync();
    }

    /// <summary>A room where Sara and Khalid are contributors.</summary>
    public async Task<Guid> SeedRoomAsync()
    {
        await SeedMembershipsAsync();
        using var scope = Scope(Sara);
        var db = scope.ServiceProvider.GetRequiredService<RoomsDbContext>();
        var room = new Room(Guid.NewGuid(), TenantId, WorkspaceId, "غرفة العقود", null, RoomVisibility.Private);
        room.AddParticipant(Sara.Id, Sara.Name, ParticipantRole.Contributor);
        room.AddParticipant(Khalid.Id, Khalid.Name, ParticipantRole.Contributor);
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room.Id;
    }

    public void Dispose() => _provider.Dispose();
}
