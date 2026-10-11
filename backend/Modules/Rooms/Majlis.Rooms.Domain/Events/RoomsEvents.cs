using Majlis.Framework.Domain.Events;

namespace Majlis.Rooms.Domain.Events;

/// <summary>Rooms asked ai-service to stop a running turn (fallback for the Redis cancel key).</summary>
public sealed record TurnStopRequested(Guid SessionId, Guid TurnId) : IEvent;
