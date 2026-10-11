using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Application.Sessions;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Application.EventHandlers;
using Majlis.Rooms.Application.Sessions.DTOs;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Enums;
using Majlis.Rooms.EntityFrameworkCore;
using Majlis.Rooms.Tests.Application.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Rooms.Tests.Application.TestInfrastructure.RoomsTestHost;

namespace Majlis.Rooms.Tests.Application.EventHandlers;

public class TurnCompletedHandlerTests : IDisposable
{
    private readonly RoomsTestHost _host = new();

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Consume_ShouldCompleteTurnOnce_WhenSameMessageArrivesTwice()
    {
        var roomId = await _host.SeedRoomAsync();
        SessionStateDto state;
        InstructResultDto turn;
        using (var scope = _host.Scope(Sara))
        {
            state = await _host.Sessions(scope).StartAsync(new StartSessionDto { RoomId = roomId }, TestContext.Current.CancellationToken);
        }

        using (var scope = _host.Scope(Sara))
        {
            turn = await _host.Sessions(scope).InstructAsync(
                new InstructSessionDto { SessionId = state.Id, Text = "hello", Epoch = state.ControlEpoch, ClientRequestId = Guid.NewGuid() },
                TestContext.Current.CancellationToken);
        }

        var message = new TurnCompleted(state.Id, turn.TurnId, "الجواب [S1]", [], 100, 20, 0);
        for (var i = 0; i < 2; i++)
        {
            using var scope = _host.Scope(Sara);
            var sp = scope.ServiceProvider;
            await TurnCompletedHandler.Handle(
                message,
                sp.GetRequiredService<IRepository<AgentSession, Guid>>(),
                sp.GetRequiredService<IRepository<Turn, Guid>>(),
                sp.GetRequiredService<SessionTimeline>(),
                sp.GetRequiredService<IUnitOfWork>(),
                sp.GetRequiredService<TimeProvider>(),
                TestContext.Current.CancellationToken);
        }

        using var check = _host.Scope(Sara);
        var db = check.ServiceProvider.GetRequiredService<RoomsDbContext>();
        Assert.Equal(TurnStatus.Completed, (await db.Turns.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Single(await db.SessionEvents.AsNoTracking().Where(e => e.Type == SessionEventTypes.TurnCompleted).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Null((await db.Sessions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).ActiveTurnId);
    }
}
