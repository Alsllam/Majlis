using FluentValidation;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Majlis.Rooms.Application.Ai;
using Majlis.Rooms.Application.Sessions.DTOs;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;
using Majlis.Rooms.Domain.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majlis.Rooms.Application.Sessions;

[Route("sessions")]
public partial class SessionsAppService(
    IRepository<AgentSession, Guid> sessions,
    IReadOnlyRepository<Room, Guid> rooms,
    IRepository<Turn, Guid> turns,
    IReadOnlyRepository<SessionEvent, Guid> events,
    IUnitOfWork unitOfWork,
    SessionTimeline timeline,
    IEventPublisher publisher,
    IAiTurnClient aiClient,
    ITurnSignals signals,
    ICurrentUser currentUser,
    IValidator<InstructSessionDto> instructValidator,
    IValidator<OfferHandOffDto> handOffValidator,
    IOptions<RoomsOptions> options,
    TimeProvider clock,
    ILogger<SessionsAppService> logger) : ApplicationService, ISessionsAppService
{
    private const int HistoryTurns = 6;

    /// <inheritdoc />
    [HttpPost("start")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public async Task<SessionStateDto> StartAsync(StartSessionDto input, CancellationToken cancellationToken = default)
    {
        var room = await LoadRoomAsync(input.RoomId, cancellationToken);
        var me = Actor(room.EnsureCanDrive(currentUser.GetRequiredId()));

        if (await sessions.AnyAsync(s => s.RoomId == room.Id && s.Status == SessionStatus.Active, cancellationToken))
        {
            throw new ConflictException(RoomsErrors.ActiveSessionExists);
        }

        var (session, change) = AgentSession.Start(Guid.NewGuid(), room.TenantId, room.WorkspaceId, room.Id, me);
        await sessions.InsertAsync(session, autoSave: false, cancellationToken);
        await timeline.AppendAsync(session, SessionEventTypes.SessionStarted, new { startedBy = me.UserId }, EventActor.User(me), cancellationToken: cancellationToken);
        await timeline.AppendControlChangeAsync(session, change, EventActor.User(me), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.ToStateDto();
    }

    /// <inheritdoc />
    [HttpPost("getbyid")]
    [HasPermission(RoomsPermissions.ViewRoom)]
    public async Task<SessionDto> GetAsync(SessionIdDto input, CancellationToken cancellationToken = default)
    {
        var session = await sessions.Query().Include(s => s.Requests).FirstOrDefaultAsync(s => s.Id == input.SessionId, cancellationToken)
            ?? throw new EntityNotFoundException();
        (await LoadRoomAsync(session.RoomId, cancellationToken)).EnsureParticipant(currentUser.GetRequiredId());

        var take = options.Value.InitialEventCount;
        var latest = await events.Query()
            .Where(e => e.SessionId == session.Id)
            .OrderByDescending(e => e.Seq)
            .Take(take + 1)
            .ToListAsync(cancellationToken);

        var hasOlder = latest.Count > take;
        var page = latest.Take(take).OrderBy(e => e.Seq).Select(e => e.ToDto(session.WorkspaceId, session.RoomId)).ToList();
        return new SessionDto(session.ToStateDto(), page, hasOlder);
    }

    /// <inheritdoc />
    [HttpPost("events")]
    [HasPermission(RoomsPermissions.ViewRoom)]
    public async Task<IReadOnlyList<SessionEventDto>> GetEventsAsync(SessionEventsFilterDto input, CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetAsync(input.SessionId, cancellationToken);
        (await LoadRoomAsync(session.RoomId, cancellationToken)).EnsureParticipant(currentUser.GetRequiredId());

        var take = Math.Clamp(input.MaxResultCount, 1, 500);
        var query = events.Query().Where(e => e.SessionId == session.Id);
        List<SessionEvent> page;
        if (input.BeforeSeq is { } before)
        {
            page = await query.Where(e => e.Seq < before).OrderByDescending(e => e.Seq).Take(take).ToListAsync(cancellationToken);
            page.Reverse();
        }
        else
        {
            var after = input.AfterSeq ?? 0;
            page = await query.Where(e => e.Seq > after).OrderBy(e => e.Seq).Take(take).ToListAsync(cancellationToken);
        }

        return page.Select(e => e.ToDto(session.WorkspaceId, session.RoomId)).ToList();
    }

    /// <inheritdoc />
    [HttpPost("instruct")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public async Task<InstructResultDto> InstructAsync(InstructSessionDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(instructValidator, input, cancellationToken);

        var existing = await turns.Query().FirstOrDefaultAsync(t => t.SessionId == input.SessionId && t.ClientRequestId == input.ClientRequestId, cancellationToken);
        if (existing is not null)
        {
            var seq = await events.Query().Where(e => e.TurnId == existing.Id && e.Type == SessionEventTypes.TurnStarted).Select(e => e.Seq).FirstAsync(cancellationToken);
            return new InstructResultDto(existing.Id, seq);
        }

        var session = await LoadSessionTrackedAsync(input.SessionId, cancellationToken);
        var room = await LoadRoomAsync(session.RoomId, cancellationToken);
        var me = Actor(room.EnsureCanDrive(currentUser.GetRequiredId()));
        session.EnsureDriver(me.UserId, input.Epoch);

        var text = input.Text.Trim();
        var language = input.Language ?? TextLanguage.Detect(text);
        var turn = new Turn(Guid.NewGuid(), session.TenantId, session.Id, me, text, language, input.ClientRequestId);
        session.BeginTurn(turn.Id);
        await turns.InsertAsync(turn, autoSave: false, cancellationToken);
        var started = await timeline.AppendAsync(
            session, SessionEventTypes.TurnStarted,
            new { instruction = text, language, instructedBy = new { userId = me.UserId, displayName = me.DisplayName } },
            EventActor.User(me), turn.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var history = await LoadHistoryAsync(session.Id, turn.Id, cancellationToken);
            await aiClient.StartTurnAsync(
                session.Id,
                new StartAiTurnRequest(turn.Id, session.TenantId, session.WorkspaceId, session.RoomId, text, language, new AiActor(me.UserId, me.DisplayName), history),
                Request.Headers.Authorization.ToString(),
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or Refit.ApiException or TaskCanceledException)
        {
            Log.AiStartFailed(logger, ex, turn.Id);
            await FailTurnAsync(session, turn, "General:Errors:AiBusy", cancellationToken);
            throw new ServiceUnavailableException("General:Errors:AiBusy");
        }

        return new InstructResultDto(turn.Id, started.Seq);
    }

    /// <inheritdoc />
    [HttpPost("stop")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public async Task StopAsync(StopTurnDto input, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionTrackedAsync(input.SessionId, cancellationToken);
        session.EnsureDriver(currentUser.GetRequiredId(), input.Epoch);

        var turn = await turns.QueryTracked().FirstOrDefaultAsync(t => t.Id == input.TurnId && t.SessionId == session.Id, cancellationToken)
            ?? throw new EntityNotFoundException();
        turn.RequestStop();
        await signals.RequestCancelAsync(turn.Id);
        await publisher.PublishAsync(new TurnStopRequested(session.Id, turn.Id), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    [HttpPost("claim")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public Task<SessionStateDto> ClaimControlAsync(SessionIdDto input, CancellationToken cancellationToken = default)
        => ChangeControlAsync(input.SessionId, requireContributor: true, async (session, me) =>
            await timeline.AppendControlChangeAsync(session, session.Claim(me), EventActor.User(me), cancellationToken), cancellationToken);

    /// <inheritdoc />
    [HttpPost("request-control")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public Task<SessionStateDto> RequestControlAsync(SessionIdDto input, CancellationToken cancellationToken = default)
        => ChangeControlAsync(input.SessionId, requireContributor: true, async (session, me) =>
        {
            var request = session.RequestControl(Guid.NewGuid(), me, clock.GetUtcNow().UtcDateTime);
            await timeline.AppendAsync(
                session, SessionEventTypes.ControlRequested,
                new { requestId = request.Id, userId = me.UserId, displayName = me.DisplayName },
                EventActor.User(me), cancellationToken: cancellationToken);
        }, cancellationToken);

    /// <inheritdoc />
    [HttpPost("resolve-request")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public Task<SessionStateDto> ResolveControlRequestAsync(ResolveControlRequestDto input, CancellationToken cancellationToken = default)
        => ChangeControlAsync(input.SessionId, requireContributor: false, async (session, me) =>
        {
            var (request, change) = session.ResolveRequest(me.UserId, input.Epoch, input.RequestId, input.Accept);
            await timeline.AppendAsync(
                session, SessionEventTypes.ControlRequestResolved,
                new { requestId = request.Id, userId = request.UserId, accepted = input.Accept },
                EventActor.User(me), cancellationToken: cancellationToken);
            if (change is not null)
            {
                await timeline.AppendControlChangeAsync(session, change, EventActor.User(me), cancellationToken);
            }
        }, cancellationToken);

    /// <inheritdoc />
    [HttpPost("offer-handoff")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public async Task<SessionStateDto> OfferHandOffAsync(OfferHandOffDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(handOffValidator, input, cancellationToken);
        return await ChangeControlAsync(input.SessionId, requireContributor: false, async (session, me) =>
        {
            var room = await LoadRoomAsync(session.RoomId, cancellationToken);
            var to = Actor(room.EnsureCanDrive(input.ToUserId));
            var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
            session.OfferHandOff(me.UserId, input.Epoch, to, note);
            await timeline.AppendAsync(
                session, SessionEventTypes.HandOffOffered,
                new { to = new { userId = to.UserId, displayName = to.DisplayName }, note },
                EventActor.User(me), cancellationToken: cancellationToken);
        }, cancellationToken);
    }

    /// <inheritdoc />
    [HttpPost("resolve-handoff")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public Task<SessionStateDto> ResolveHandOffAsync(ResolveHandOffDto input, CancellationToken cancellationToken = default)
        => ChangeControlAsync(input.SessionId, requireContributor: true, async (session, me) =>
        {
            var change = session.ResolveHandOff(me, input.Accept);
            await timeline.AppendAsync(
                session, SessionEventTypes.HandOffResolved, new { userId = me.UserId, accepted = input.Accept },
                EventActor.User(me), cancellationToken: cancellationToken);
            if (change is not null)
            {
                await timeline.AppendControlChangeAsync(session, change, EventActor.User(me), cancellationToken);
            }
        }, cancellationToken);

    /// <inheritdoc />
    [HttpPost("takeover")]
    [HasPermission(RoomsPermissions.TakeOverSession)]
    public Task<SessionStateDto> TakeOverAsync(SessionIdDto input, CancellationToken cancellationToken = default)
        => ChangeControlAsync(input.SessionId, requireContributor: true, async (session, me) =>
            await timeline.AppendControlChangeAsync(session, session.TakeOver(me), EventActor.User(me), cancellationToken), cancellationToken);

    /// <inheritdoc />
    [HttpPost("release")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public Task<SessionStateDto> ReleaseControlAsync(SessionEpochDto input, CancellationToken cancellationToken = default)
        => ChangeControlAsync(input.SessionId, requireContributor: false, async (session, me) =>
            await timeline.AppendControlChangeAsync(session, session.Release(me.UserId, input.Epoch), EventActor.User(me), cancellationToken), cancellationToken);

    /// <inheritdoc />
    [HttpPost("end")]
    [HasPermission(RoomsPermissions.DriveSession)]
    public Task<SessionStateDto> EndAsync(SessionEpochDto input, CancellationToken cancellationToken = default)
        => ChangeControlAsync(input.SessionId, requireContributor: false, async (session, me) =>
        {
            session.EnsureDriver(me.UserId, input.Epoch);
            session.End(clock.GetUtcNow().UtcDateTime);
            await timeline.AppendAsync(session, SessionEventTypes.SessionEnded, new { endedBy = me.UserId }, EventActor.User(me), cancellationToken: cancellationToken);
        }, cancellationToken);

    /// <summary>Loads the session (tracked) and the caller as a participant, runs the transition, saves once.</summary>
    private async Task<SessionStateDto> ChangeControlAsync(Guid sessionId, bool requireContributor, Func<AgentSession, SessionActor, Task> change, CancellationToken cancellationToken)
    {
        var session = await LoadSessionTrackedAsync(sessionId, cancellationToken);
        var room = await LoadRoomAsync(session.RoomId, cancellationToken);
        var participant = requireContributor ? room.EnsureCanDrive(currentUser.GetRequiredId()) : room.EnsureParticipant(currentUser.GetRequiredId());

        await change(session, Actor(participant));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.ToStateDto();
    }

    private async Task FailTurnAsync(AgentSession session, Turn turn, string reasonKey, CancellationToken cancellationToken)
    {
        // The request token may be cancelled by now; the failure must still be recorded.
        if (turn.Fail(reasonKey, null, clock.GetUtcNow().UtcDateTime))
        {
            session.FinishTurn(turn.Id);
            await timeline.AppendAsync(session, SessionEventTypes.TurnFailed, new { reasonKey }, EventActor.System, turn.Id, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<IReadOnlyList<AiHistoryTurn>> LoadHistoryAsync(Guid sessionId, Guid currentTurnId, CancellationToken cancellationToken)
    {
        var previous = await turns.Query()
            .Where(t => t.SessionId == sessionId && t.Id != currentTurnId && t.Status == TurnStatus.Completed)
            .OrderByDescending(t => t.CreationTime)
            .Take(HistoryTurns)
            .Select(t => new AiHistoryTurn(t.Instruction, t.AnswerText ?? string.Empty, t.InstructedByDisplayName))
            .ToListAsync(cancellationToken);
        previous.Reverse();
        return previous;
    }

    private async Task<AgentSession> LoadSessionTrackedAsync(Guid id, CancellationToken cancellationToken)
        => await sessions.QueryTracked().Include(s => s.Requests).FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
           ?? throw new EntityNotFoundException();

    private async Task<Room> LoadRoomAsync(Guid id, CancellationToken cancellationToken)
        => await rooms.Query().Include(r => r.Participants).FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
           ?? throw new EntityNotFoundException();

    private static SessionActor Actor(RoomParticipant participant) => new(participant.UserId, participant.DisplayName);

    private static partial class Log
    {
        [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "ai-service did not accept turn {TurnId}")]
        public static partial void AiStartFailed(ILogger logger, Exception exception, Guid turnId);
    }
}
