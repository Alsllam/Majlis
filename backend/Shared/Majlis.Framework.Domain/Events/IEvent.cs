namespace Majlis.Framework.Domain.Events;

/// <summary>Marker for integration events published between modules.</summary>
public interface IEvent;

/// <summary>Events that the Audit module records.</summary>
public interface IAuditable
{
    Guid TenantId { get; }
    Guid? ActorId { get; }
    string AuditAction { get; }
}

/// <summary>Publishes integration events. Module hosts implement it over the transactional outbox (ADR-0009).</summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) where TEvent : class, IEvent;
}
