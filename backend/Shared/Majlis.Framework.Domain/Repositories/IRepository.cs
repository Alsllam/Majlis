using System.Linq.Expressions;
using Majlis.Framework.Domain.Entities;

namespace Majlis.Framework.Domain.Repositories;

/// <summary>Read side: no tracking. Inject this for queries.</summary>
public interface IReadOnlyRepository<TEntity, in TKey>
    where TEntity : Entity<TKey>
    where TKey : notnull
{
    Task<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includes);

    Task<TEntity> GetAsync(TKey id, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includes);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<PagedResult<TEntity>> GetPagedListAsync(
        Expression<Func<TEntity, bool>> predicate,
        int skipCount,
        int maxResultCount,
        string? sorting,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes);

    /// <summary>Queryable for custom projections. Never leaks out of the application layer.</summary>
    IQueryable<TEntity> Query();
}

/// <summary>Write side: tracked entities.</summary>
public interface IRepository<TEntity, in TKey> : IReadOnlyRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
    where TKey : notnull
{
    Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>Tracked queryable, for loading aggregates with their children.</summary>
    IQueryable<TEntity> QueryTracked();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
