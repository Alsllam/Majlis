using Majlis.Framework.Domain.Events;
using MassTransit;

namespace Majlis.Framework.Application.Messaging;

/// <summary>
/// Publishes through MassTransit. Inside a request with the EF Core outbox, messages are stored in the
/// module's outbox table and sent only after <c>SaveChanges</c> commits.
/// </summary>
public sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) where TEvent : class, IEvent
        => publishEndpoint.Publish(message, cancellationToken);
}
