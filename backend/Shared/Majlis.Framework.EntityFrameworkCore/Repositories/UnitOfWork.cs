using Majlis.Framework.Domain.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Majlis.Framework.EntityFrameworkCore.Repositories;

/// <summary>Disposable both ways: request scopes dispose asynchronously, background scopes synchronously.</summary>
public sealed class UnitOfWork(MajlisDbContext dbContext) : IUnitOfWork, IDisposable, IAsyncDisposable
{
    private IDbContextTransaction? _transaction;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

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
