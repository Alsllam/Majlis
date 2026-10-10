using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Majlis.Framework.Application.Messaging;

/// <summary>
/// Publishes through Wolverine's transactional outbox enlisted in the module DbContext: the event is stored with the
/// business rows by <see cref="OutboxUnitOfWork.SaveChangesAsync"/> and sent only after that commit.
/// </summary>
public sealed class OutboxEventPublisher(IDbContextOutbox outbox, MajlisDbContext dbContext) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) where TEvent : class, IEvent
    {
        outbox.Enroll(dbContext);
        return outbox.PublishAsync(message).AsTask();
    }
}

/// <summary>Publishes straight to the bus (hosts without a database); delivery is best effort.</summary>
public sealed class BusEventPublisher(IMessageBus bus) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) where TEvent : class, IEvent
        => bus.PublishAsync(message).AsTask();
}

/// <summary>
/// Unit of work for module hosts: one save commits the business rows and the pending integration events, then the
/// events are sent. With an explicit transaction, events are persisted and sent after the commit (never before it).
/// </summary>
public sealed class OutboxUnitOfWork(IDbContextOutbox outbox, MajlisDbContext dbContext) : IUnitOfWork, IDisposable, IAsyncDisposable
{
    private IDbContextTransaction? _transaction;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        outbox.Enroll(dbContext);
        return outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _transaction ??= await dbContext.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        outbox.Enroll(dbContext);
        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
    }
}
