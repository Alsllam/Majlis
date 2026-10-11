using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Application.EventHandlers;
using Majlis.Rooms.Application.Sessions;
using Majlis.Rooms.Application.Sessions.DTOs;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.EntityFrameworkCore;
using Majlis.Rooms.Tests.Application.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Rooms.Tests.Application.TestInfrastructure.RoomsTestHost;

namespace Majlis.Rooms.Tests.Application.EventHandlers;

public class ApprovalHandlerTests : IDisposable
{
    private readonly RoomsTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_ShouldAppendEachApprovalEventOnce_WhenRedelivered()
    {
        var roomId = await _host.SeedRoomAsync();
        Guid sessionId;
        using (var scope = _host.Scope(Sara))
        {
            sessionId = (await _host.Sessions(scope).StartAsync(new StartSessionDto { RoomId = roomId }, Ct)).Id;
        }

        var requestId = Guid.NewGuid();
        var requested = new ApprovalRequested(TenantId, WorkspaceId, roomId, sessionId, null, requestId, "create_task", "إنشاء مهمة", null, "Low", """{"title":"x"}""", Sara.Id, Sara.Name, DateTime.UtcNow.AddDays(1));
        var decided = new ApprovalDecided(TenantId, sessionId, requestId, "create_task", "Approved", Khalid.Id, Khalid.Name, null, null);
        var executed = new ApprovalExecuted(TenantId, sessionId, requestId, "create_task", true, "x", Guid.NewGuid(), null);
        var otherSession = new ApprovalDecided(TenantId, Guid.NewGuid(), Guid.NewGuid(), "create_task", "Expired", null, null, null, null);

        for (var i = 0; i < 2; i++)
        {
            using var scope = _host.Scope(Sara);
            var sp = scope.ServiceProvider;
            var sessions = sp.GetRequiredService<IRepository<AgentSession, Guid>>();
            var events = sp.GetRequiredService<IReadOnlyRepository<SessionEvent, Guid>>();
            var timeline = sp.GetRequiredService<SessionTimeline>();
            var uow = sp.GetRequiredService<IUnitOfWork>();
            await ApprovalRequestedHandler.Handle(requested, sessions, events, timeline, uow, Ct);
            await ApprovalDecidedHandler.Handle(decided, sessions, events, timeline, uow, Ct);
            await ApprovalExecutedHandler.Handle(executed, sessions, events, timeline, uow, Ct);
            await ApprovalDecidedHandler.Handle(otherSession, sessions, events, timeline, uow, Ct);
        }

        using var check = _host.Scope(Sara);
        var db = check.ServiceProvider.GetRequiredService<RoomsDbContext>();
        var rows = await db.SessionEvents.AsNoTracking().Where(e => e.SessionId == sessionId && e.Type.StartsWith("approval.")).ToListAsync(Ct);
        Assert.Equal(
            [SessionEventTypes.ApprovalRequested, SessionEventTypes.ApprovalDecided, SessionEventTypes.ApprovalExecuted],
            rows.OrderBy(e => e.Seq).Select(e => e.Type));
        Assert.All(rows, e => Assert.Contains(requestId.ToString(), e.DataJson));
        Assert.Empty(await db.SessionEvents.AsNoTracking().Where(e => e.Type == SessionEventTypes.ApprovalExpired).ToListAsync(Ct));
    }
}
