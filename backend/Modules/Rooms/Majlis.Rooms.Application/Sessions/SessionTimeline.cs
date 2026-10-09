using System.Text.Json;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;

namespace Majlis.Rooms.Application.Sessions;

/// <summary>Who caused a timeline event.</summary>
public readonly record struct EventActor(string Kind, Guid? Id, string? DisplayName)
{
    public static EventActor User(SessionActor actor) => new(ActorKinds.User, actor.UserId, actor.DisplayName);

    public static readonly EventActor Agent = new(ActorKinds.Agent, null, null);

    public static readonly EventActor System = new(ActorKinds.System, null, null);
}

/// <summary>
/// The sequencer (ADR-0003): appends an event with the session's next <c>seq</c> and publishes
/// <see cref="SessionEventAppended"/> through the outbox. Both are committed by the caller's <c>SaveChanges</c>,
/// together with the state change, so an event can never be shown without being stored.
/// The session must be tracked; its row version makes concurrent appends fail instead of reusing a seq.
/// </summary>
public sealed class SessionTimeline(IRepository<SessionEvent, Guid> events, IEventPublisher publisher, TimeProvider clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SessionEvent> AppendAsync(
        AgentSession session, string type, object data, EventActor actor, Guid? turnId = null, CancellationToken cancellationToken = default)
    {
        var dataJson = JsonSerializer.Serialize(data, Json);
        var entry = new SessionEvent(
            Guid.NewGuid(), session.TenantId, session.Id, session.NextSeq(), type, turnId,
            actor.Kind, actor.Id, actor.DisplayName, dataJson, clock.GetUtcNow().UtcDateTime);

        await events.InsertAsync(entry, autoSave: false, cancellationToken);
        await publisher.PublishAsync(
            new SessionEventAppended(
                entry.TenantId, session.WorkspaceId, session.RoomId, session.Id, entry.Seq, entry.Type, entry.TurnId, entry.At,
                entry.ActorKind, entry.ActorId, entry.ActorDisplayName, entry.DataJson),
            cancellationToken);
        return entry;
    }

    public Task<SessionEvent> AppendControlChangeAsync(AgentSession session, ControlChange change, EventActor actor, CancellationToken cancellationToken = default)
        => AppendAsync(
            session,
            SessionEventTypes.ControlChanged,
            new
            {
                kind = KindName(change.Kind),
                from = change.From is { } f ? new { userId = f.UserId, displayName = f.DisplayName } : null,
                to = change.To is { } t ? new { userId = t.UserId, displayName = t.DisplayName } : null,
                epoch = change.Epoch,
                note = change.Note,
            },
            actor,
            cancellationToken: cancellationToken);

    /// <summary>Wire names of control changes (docs/architecture/realtime-collaboration.md §4.2).</summary>
    public static string KindName(ControlChangeKind kind) => kind switch
    {
        ControlChangeKind.Start => "start",
        ControlChangeKind.Claim => "claim",
        ControlChangeKind.RequestAccepted => "request-accepted",
        ControlChangeKind.HandOff => "handoff",
        ControlChangeKind.TakeOver => "takeover",
        ControlChangeKind.Release => "release",
        ControlChangeKind.Timeout => "timeout",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
