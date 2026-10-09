using System.Linq.Expressions;
using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Framework.EntityFrameworkCore.Repositories;

/// <summary>Generic repository over the host's module DbContext (registered as <see cref="MajlisDbContext"/>).</summary>
public class EfRepository<TEntity, TKey>(MajlisDbContext dbContext) : IRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
    where TKey : notnull
{
    protected MajlisDbContext DbContext { get; } = dbContext;

    protected DbSet<TEntity> Set => DbContext.Set<TEntity>();

    public IQueryable<TEntity> Query() => Set.AsNoTracking();

    public IQueryable<TEntity> QueryTracked() => Set;

    public async Task<TEntity?> FindAsync(TKey id, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includes)
    {
        var query = includes.Aggregate(Query(), (q, include) => q.Include(include));
        return await query.FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);
    }

    public async Task<TEntity> GetAsync(TKey id, CancellationToken cancellationToken = default, params Expression<Func<TEntity, object>>[] includes)
        => await FindAsync(id, cancellationToken, includes) ?? throw new EntityNotFoundException();

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Query().AnyAsync(predicate, cancellationToken);

    public Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Query().Where(predicate).ToListAsync(cancellationToken);

    public async Task<PagedResult<TEntity>> GetPagedListAsync(
        Expression<Func<TEntity, bool>> predicate,
        int skipCount,
        int maxResultCount,
        string? sorting,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes)
    {
        var query = includes.Aggregate(Query(), (q, include) => q.Include(include)).Where(predicate);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .ApplySorting(string.IsNullOrWhiteSpace(sorting) ? "CreationTime Desc" : sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
        return new PagedResult<TEntity>(items, total);
    }

    public async Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return entity;
    }

    public async Task UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        if (DbContext.Entry(entity).State == EntityState.Detached)
        {
            Set.Update(entity);
        }

        if (autoSave)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        Set.Remove(entity);
        if (autoSave)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
