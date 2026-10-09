using System.Linq.Expressions;
using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Security;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Framework.EntityFrameworkCore;

/// <summary>
/// Base for every module's DbContext: one schema per module, audit columns, soft-delete and tenant filters,
/// and the MassTransit outbox tables so integration events are published only after commit.
/// </summary>
public abstract class MajlisDbContext : DbContext
{
    public const string SoftDeleteFilter = "SoftDelete";
    public const string TenantFilter = "Tenant";

    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    protected MajlisDbContext(DbContextOptions options, ICurrentUser currentUser, TimeProvider clock)
        : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>The module's schema, e.g. <c>rooms</c>.</summary>
    protected abstract string Schema { get; }

    /// <summary>Tenant of the caller. Null for system work (message consumers, jobs), which is then not tenant-filtered.</summary>
    public Guid? CurrentTenantId => _currentUser.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (entityType.BaseType is not null)
            {
                continue;
            }

            if (typeof(ISoftDelete).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasQueryFilter(SoftDeleteFilter, BuildSoftDeleteFilter(clrType));
            }

            if (typeof(IMultiTenant).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasQueryFilter(TenantFilter, BuildTenantFilter(clrType));
                modelBuilder.Entity(clrType).HasIndex(nameof(IMultiTenant.TenantId));
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAudit()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _currentUser.Id;

        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity is ICreationAudited created:
                    created.CreationTime = now;
                    created.CreatorId ??= userId;
                    break;
                case EntityState.Modified when entry.Entity is IModificationAudited modified:
                    modified.LastModificationTime = now;
                    modified.LastModifierId = userId;
                    break;
                case EntityState.Deleted when entry.Entity is ISoftDelete soft:
                    entry.State = EntityState.Modified;
                    soft.IsDeleted = true;
                    soft.DeletionTime = now;
                    soft.DeleterId = userId;
                    break;
            }
        }
    }

    private static LambdaExpression BuildSoftDeleteFilter(Type type)
    {
        var e = Expression.Parameter(type, "e");
        var isDeleted = Expression.Property(e, nameof(ISoftDelete.IsDeleted));
        return Expression.Lambda(Expression.Not(isDeleted), e);
    }

    private LambdaExpression BuildTenantFilter(Type type)
    {
        // e => CurrentTenantId == null || e.TenantId == CurrentTenantId
        var e = Expression.Parameter(type, "e");
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        var tenant = Expression.Convert(Expression.Property(e, nameof(IMultiTenant.TenantId)), typeof(Guid?));
        var body = Expression.OrElse(
            Expression.Equal(current, Expression.Constant(null, typeof(Guid?))),
            Expression.Equal(tenant, current));
        return Expression.Lambda(body, e);
    }
}
