using FakeItEasy;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Rooms.Application.Ai;
using Majlis.Rooms.Application.Sessions.DTOs;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;
using Majlis.Rooms.EntityFrameworkCore;
using Majlis.Rooms.Tests.Application.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Rooms.Tests.Application.TestInfrastructure.RoomsTestHost;

namespace Majlis.Rooms.Tests.Application.AppServices;

public class SessionsAppServiceTests : IDisposable
{
    private readonly RoomsTestHost _host = new();

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<SessionStateDto> StartAsSaraAsync(Guid roomId)
    {
        using var scope = _host.Scope(Sara);
        return await _host.Sessions(scope).StartAsync(new StartSessionDto { RoomId = roomId }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_ShouldAppendStartedAndControlEvents_WhenRoomHasNoActiveSession()
    {
        var roomId = await _host.SeedRoomAsync();

        var state = await StartAsSaraAsync(roomId);

        Assert.Equal(Sara.Id, state.Driver!.UserId);
        Assert.Equal(1, state.ControlEpoch);
        Assert.Equal(2, state.LastSeq);
        A.CallTo(() => _host.Publisher.PublishAsync(A<SessionEventAppended>.That.Matches(e => e.Seq == 1 && e.Type == SessionEventTypes.SessionStarted), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<SessionEventAppended>.That.Matches(e => e.Seq == 2 && e.Type == SessionEventTypes.ControlChanged), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartAsync_ShouldThrowConflict_WhenRoomAlreadyHasActiveSession()
    {
        var roomId = await _host.SeedRoomAsync();
        await StartAsSaraAsync(roomId);

        using var scope = _host.Scope(Khalid);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _host.Sessions(scope).StartAsync(new StartSessionDto { RoomId = roomId }, TestContext.Current.CancellationToken));

        Assert.Equal(RoomsErrors.ActiveSessionExists, ex.MessageKey);
    }

    [Fact]
    public async Task GetAsync_ShouldThrowForbidden_WhenCallerIsNotParticipant()
    {
        var roomId = await _host.SeedRoomAsync();
        var state = await StartAsSaraAsync(roomId);

        using var scope = _host.Scope(Outsider);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _host.Sessions(scope).GetAsync(new SessionIdDto { SessionId = state.Id }, TestContext.Current.CancellationToken));

        Assert.Equal(RoomsErrors.NotParticipant, ex.MessageKey);
    }

    [Fact]
    public async Task GetAsync_ShouldThrowNotFound_WhenSessionDoesNotExist()
    {
        await _host.SeedRoomAsync();

        using var scope = _host.Scope(Sara);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _host.Sessions(scope).GetAsync(new SessionIdDto { SessionId = Guid.NewGuid() }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InstructAsync_ShouldStartTurnAndCallAiWithDriverToken_WhenDriverInstructs()
    {
        var roomId = await _host.SeedRoomAsync();
        var state = await StartAsSaraAsync(roomId);

        using var scope = _host.Scope(Sara);
        var result = await _host.Sessions(scope).InstructAsync(
            new InstructSessionDto { SessionId = state.Id, Text = "لخّص العقد", Epoch = state.ControlEpoch, ClientRequestId = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Seq);
        A.CallTo(() => _host.AiClient.StartTurnAsync(state.Id, A<StartAiTurnRequest>.That.Matches(r => r.TurnId == result.TurnId && r.Language == "ar"), "Bearer test-token", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        var db = scope.ServiceProvider.GetRequiredService<RoomsDbContext>();
        Assert.Equal(result.TurnId, (await db.Sessions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).ActiveTurnId);
    }

    [Fact]
    public async Task InstructAsync_ShouldReturnSameTurn_WhenClientRequestIdRepeats()
    {
        var roomId = await _host.SeedRoomAsync();
        var state = await StartAsSaraAsync(roomId);
        var input = new InstructSessionDto { SessionId = state.Id, Text = "hello", Epoch = state.ControlEpoch, ClientRequestId = Guid.NewGuid() };

        InstructResultDto first, second;
        using (var scope = _host.Scope(Sara))
        {
            first = await _host.Sessions(scope).InstructAsync(input, TestContext.Current.CancellationToken);
        }

        using (var scope = _host.Scope(Sara))
        {
            second = await _host.Sessions(scope).InstructAsync(input, TestContext.Current.CancellationToken);
        }

        Assert.Equal(first, second);
        A.CallTo(() => _host.AiClient.StartTurnAsync(A<Guid>._, A<StartAiTurnRequest>._, A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstructAsync_ShouldThrowConflict_WhenEpochIsStale()
    {
        var roomId = await _host.SeedRoomAsync();
        var state = await StartAsSaraAsync(roomId);

        using var scope = _host.Scope(Sara);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _host.Sessions(scope).InstructAsync(
            new InstructSessionDto { SessionId = state.Id, Text = "hello", Epoch = state.ControlEpoch + 7, ClientRequestId = Guid.NewGuid() },
            TestContext.Current.CancellationToken));

        Assert.Equal(RoomsErrors.ControlChanged, ex.MessageKey);
    }

    [Fact]
    public async Task InstructAsync_ShouldFailTurnAndThrowServiceUnavailable_WhenAiServiceIsDown()
    {
        var roomId = await _host.SeedRoomAsync();
        var state = await StartAsSaraAsync(roomId);
        A.CallTo(() => _host.AiClient.StartTurnAsync(A<Guid>._, A<StartAiTurnRequest>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        using var scope = _host.Scope(Sara);
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => _host.Sessions(scope).InstructAsync(
            new InstructSessionDto { SessionId = state.Id, Text = "hello", Epoch = state.ControlEpoch, ClientRequestId = Guid.NewGuid() },
            TestContext.Current.CancellationToken));

        var db = scope.ServiceProvider.GetRequiredService<RoomsDbContext>();
        var turn = await db.Turns.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TurnStatus.Failed, turn.Status);
        Assert.Null((await db.Sessions.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).ActiveTurnId);
        Assert.Contains(await db.SessionEvents.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken), e => e.Type == SessionEventTypes.TurnFailed);
    }

    [Fact]
    public async Task RequestAndResolve_ShouldMoveControlToRequester_WhenDriverAccepts()
    {
        var roomId = await _host.SeedRoomAsync();
        var state = await StartAsSaraAsync(roomId);

        SessionStateDto requested;
        using (var scope = _host.Scope(Khalid))
        {
            requested = await _host.Sessions(scope).RequestControlAsync(new SessionIdDto { SessionId = state.Id }, TestContext.Current.CancellationToken);
        }

        using (var scope = _host.Scope(Sara))
        {
            var after = await _host.Sessions(scope).ResolveControlRequestAsync(
                new ResolveControlRequestDto { SessionId = state.Id, RequestId = requested.PendingRequests[0].Id, Epoch = requested.ControlEpoch, Accept = true },
                TestContext.Current.CancellationToken);

            Assert.Equal(Khalid.Id, after.Driver!.UserId);
            Assert.Equal(state.ControlEpoch + 1, after.ControlEpoch);
            Assert.Empty(after.PendingRequests);
        }
    }

    [Fact]
    public async Task GetEventsAsync_ShouldReturnOnlyEventsAfterSeqInOrder_WhenAfterSeqIsGiven()
    {
        var roomId = await _host.SeedRoomAsync();
        var state = await StartAsSaraAsync(roomId);
        using (var scope = _host.Scope(Khalid))
        {
            await _host.Sessions(scope).RequestControlAsync(new SessionIdDto { SessionId = state.Id }, TestContext.Current.CancellationToken);
        }

        using (var scope = _host.Scope(Khalid))
        {
            var events = await _host.Sessions(scope).GetEventsAsync(new SessionEventsFilterDto { SessionId = state.Id, AfterSeq = 1 }, TestContext.Current.CancellationToken);

            Assert.Equal([2, 3], events.Select(e => e.Seq));
            Assert.Equal(SessionEventTypes.ControlRequested, events[^1].Type);
        }
    }

    [Fact]
    public async Task StopAsync_ShouldSignalCancel_WhenDriverStopsRunningTurn()
    {
        var roomId = await _host.SeedRoomAsync();
        var state = await StartAsSaraAsync(roomId);
        InstructResultDto turn;
        using (var scope = _host.Scope(Sara))
        {
            turn = await _host.Sessions(scope).InstructAsync(
                new InstructSessionDto { SessionId = state.Id, Text = "hello", Epoch = state.ControlEpoch, ClientRequestId = Guid.NewGuid() },
                TestContext.Current.CancellationToken);
        }

        using (var scope = _host.Scope(Sara))
        {
            await _host.Sessions(scope).StopAsync(new StopTurnDto { SessionId = state.Id, TurnId = turn.TurnId, Epoch = state.ControlEpoch }, TestContext.Current.CancellationToken);
        }

        A.CallTo(() => _host.Signals.RequestCancelAsync(turn.TurnId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<Majlis.Rooms.Domain.Events.TurnStopRequested>.That.Matches(e => e.TurnId == turn.TurnId), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
