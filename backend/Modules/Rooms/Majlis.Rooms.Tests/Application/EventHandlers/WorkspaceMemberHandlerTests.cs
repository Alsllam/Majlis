using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Application.EventHandlers;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Events;
using Majlis.Rooms.Tests.Application.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Rooms.Tests.Application.TestInfrastructure.RoomsTestHost;

namespace Majlis.Rooms.Tests.Application.EventHandlers;

public class WorkspaceMemberHandlerTests : IDisposable
{
    private readonly RoomsTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<List<WorkspaceMembership>> MembershipsAsync()
    {
        using var scope = _host.Scope(Sara);
        return await scope.ServiceProvider.GetRequiredService<IReadOnlyRepository<WorkspaceMembership, Guid>>().Query().Where(m => m.WorkspaceId == WorkspaceId).ToListAsync(Ct);
    }

    [Fact]
    public async Task Handle_ShouldUpsertOnceAndTrackRoleChanges_WhenMemberEventsArriveTwice()
    {
        var added = new MemberAdded(TenantId, WorkspaceId, Khalid.Id, Khalid.Name, "Contributor");
        using (var scope = _host.Scope(Sara))
        {
            var sp = scope.ServiceProvider;
            await MemberAddedHandler.Handle(added, sp.GetRequiredService<IRepository<WorkspaceMembership, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
            await MemberAddedHandler.Handle(added, sp.GetRequiredService<IRepository<WorkspaceMembership, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
            await MemberRoleChangedHandler.Handle(new MemberRoleChanged(TenantId, WorkspaceId, Khalid.Id, Khalid.Name, "Admin"), sp.GetRequiredService<IRepository<WorkspaceMembership, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
        }

        var row = Assert.Single(await MembershipsAsync());
        Assert.Equal("Admin", row.Role);

        using (var scope = _host.Scope(Sara))
        {
            var sp = scope.ServiceProvider;
            await MemberRemovedHandler.Handle(new MemberRemoved(TenantId, WorkspaceId, Khalid.Id), sp.GetRequiredService<IRepository<WorkspaceMembership, Guid>>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
        }

        Assert.Empty(await MembershipsAsync());
    }
}
