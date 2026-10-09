using Majlis.Framework.Domain.Exceptions;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;

namespace Majlis.Rooms.Tests.Domain;

public class AgentSessionTests
{
    private static readonly SessionActor Sara = new(Guid.NewGuid(), "سارة");
    private static readonly SessionActor Khalid = new(Guid.NewGuid(), "خالد");
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static AgentSession Started() => AgentSession.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Sara).Session;

    [Fact]
    public void Start_ShouldMakeStarterDriverWithEpochOne_WhenSessionStarts()
    {
        var (session, change) = AgentSession.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Sara);

        Assert.Equal(Sara.UserId, session.DriverUserId);
        Assert.Equal(1, session.ControlEpoch);
        Assert.Equal(ControlChangeKind.Start, change.Kind);
        Assert.Null(change.From);
    }

    [Fact]
    public void EnsureDriver_ShouldThrowConflict_WhenEpochIsStale()
    {
        var session = Started();

        var ex = Assert.Throws<ConflictException>(() => session.EnsureDriver(Sara.UserId, session.ControlEpoch - 1));

        Assert.Equal(RoomsErrors.ControlChanged, ex.MessageKey);
    }

    [Fact]
    public void EnsureDriver_ShouldThrowForbidden_WhenCallerIsNotDriver()
    {
        var session = Started();

        var ex = Assert.Throws<ForbiddenException>(() => session.EnsureDriver(Khalid.UserId, session.ControlEpoch));

        Assert.Equal(RoomsErrors.NotDriver, ex.MessageKey);
    }

    [Fact]
    public void Claim_ShouldThrowConflict_WhenControlIsHeld()
    {
        var session = Started();

        var ex = Assert.Throws<ConflictException>(() => session.Claim(Khalid));

        Assert.Equal(RoomsErrors.ControlNotFree, ex.MessageKey);
    }

    [Fact]
    public void Claim_ShouldGiveControlAndBumpEpoch_WhenControlIsFree()
    {
        var session = Started();
        session.Release(Sara.UserId, session.ControlEpoch);
        var epoch = session.ControlEpoch;

        var change = session.Claim(Khalid);

        Assert.Equal(Khalid.UserId, session.DriverUserId);
        Assert.Equal(epoch + 1, session.ControlEpoch);
        Assert.Equal(ControlChangeKind.Claim, change.Kind);
    }

    [Fact]
    public void RequestControl_ShouldBeIdempotent_WhenSameUserAsksTwice()
    {
        var session = Started();

        var first = session.RequestControl(Guid.NewGuid(), Khalid, Now);
        var second = session.RequestControl(Guid.NewGuid(), Khalid, Now);

        Assert.Same(first, second);
        Assert.Single(session.Requests);
    }

    [Fact]
    public void ResolveRequest_ShouldHandControlToRequester_WhenDriverAccepts()
    {
        var session = Started();
        var request = session.RequestControl(Guid.NewGuid(), Khalid, Now);

        var (resolved, change) = session.ResolveRequest(Sara.UserId, session.ControlEpoch, request.Id, accept: true);

        Assert.Equal(ControlRequestStatus.Accepted, resolved.Status);
        Assert.Equal(Khalid.UserId, session.DriverUserId);
        Assert.Equal(ControlChangeKind.RequestAccepted, change!.Kind);
        Assert.Equal(Sara.UserId, change.From!.Value.UserId);
    }

    [Fact]
    public void ResolveRequest_ShouldKeepDriver_WhenDriverDeclines()
    {
        var session = Started();
        var request = session.RequestControl(Guid.NewGuid(), Khalid, Now);
        var epoch = session.ControlEpoch;

        var (resolved, change) = session.ResolveRequest(Sara.UserId, epoch, request.Id, accept: false);

        Assert.Equal(ControlRequestStatus.Declined, resolved.Status);
        Assert.Null(change);
        Assert.Equal(Sara.UserId, session.DriverUserId);
        Assert.Equal(epoch, session.ControlEpoch);
    }

    [Fact]
    public void ResolveHandOff_ShouldGiveControlToReceiver_WhenReceiverAccepts()
    {
        var session = Started();
        session.OfferHandOff(Sara.UserId, session.ControlEpoch, Khalid, "أكمل من البند ٤");

        var change = session.ResolveHandOff(Khalid, accept: true);

        Assert.Equal(Khalid.UserId, session.DriverUserId);
        Assert.Equal(ControlChangeKind.HandOff, change!.Kind);
        Assert.Equal("أكمل من البند ٤", change.Note);
        Assert.Null(session.PendingHandOffToUserId);
    }

    [Fact]
    public void ResolveHandOff_ShouldThrowConflict_WhenNoOfferIsPendingForCaller()
    {
        var session = Started();

        var ex = Assert.Throws<ConflictException>(() => session.ResolveHandOff(Khalid, accept: true));

        Assert.Equal(RoomsErrors.NoPendingHandOff, ex.MessageKey);
    }

    [Fact]
    public void OfferHandOff_ShouldThrowValidation_WhenOfferedToSelf()
    {
        var session = Started();

        Assert.Throws<CustomValidationException>(() => session.OfferHandOff(Sara.UserId, session.ControlEpoch, Sara, null));
    }

    [Fact]
    public void TakeOver_ShouldReplaceDriverAndClearPendingOffer_WhenAdminTakesOver()
    {
        var session = Started();
        session.OfferHandOff(Sara.UserId, session.ControlEpoch, Khalid, null);
        var admin = new SessionActor(Guid.NewGuid(), "نورة");

        var change = session.TakeOver(admin);

        Assert.Equal(admin.UserId, session.DriverUserId);
        Assert.Equal(ControlChangeKind.TakeOver, change.Kind);
        Assert.Null(session.PendingHandOffToUserId);
    }

    [Fact]
    public void FreeIfDriverAbsent_ShouldFreeControl_WhenAbsentLongerThanTimeout()
    {
        var session = Started();
        session.MarkDriverAbsent(Sara.UserId, Now);

        var change = session.FreeIfDriverAbsent(Now.AddMinutes(3), TimeSpan.FromMinutes(2));

        Assert.NotNull(change);
        Assert.Equal(ControlChangeKind.Timeout, change.Kind);
        Assert.Null(session.DriverUserId);
    }

    [Fact]
    public void FreeIfDriverAbsent_ShouldDoNothing_WhenDriverCameBack()
    {
        var session = Started();
        session.MarkDriverAbsent(Sara.UserId, Now);
        session.MarkPresent(Sara.UserId);

        var change = session.FreeIfDriverAbsent(Now.AddMinutes(3), TimeSpan.FromMinutes(2));

        Assert.Null(change);
        Assert.Equal(Sara.UserId, session.DriverUserId);
    }

    [Fact]
    public void BeginTurn_ShouldThrowConflict_WhenATurnIsRunning()
    {
        var session = Started();
        session.BeginTurn(Guid.NewGuid());

        var ex = Assert.Throws<ConflictException>(() => session.BeginTurn(Guid.NewGuid()));

        Assert.Equal(RoomsErrors.TurnInProgress, ex.MessageKey);
    }

    [Fact]
    public void NextSeq_ShouldBeGapFree_WhenCalledRepeatedly()
    {
        var session = Started();

        var seqs = Enumerable.Range(0, 5).Select(_ => session.NextSeq()).ToList();

        Assert.Equal([1, 2, 3, 4, 5], seqs);
    }

    [Fact]
    public void End_ShouldClearDriverAndRejectFurtherCommands_WhenSessionEnds()
    {
        var session = Started();

        session.End(Now);

        Assert.Equal(SessionStatus.Ended, session.Status);
        Assert.Null(session.DriverUserId);
        Assert.Throws<ConflictException>(() => session.Claim(Khalid));
    }
}
