using Majlis.Framework.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Framework.EntityFrameworkCore.Configuration;

/// <summary>Base configuration: key, concurrency token and the IsActive default. Query filters are added by <see cref="MajlisDbContext"/>.</summary>
public abstract class DefaultEntityTypeConfiguration<TEntity, TKey> : IEntityTypeConfiguration<TEntity>
    where TEntity : Entity<TKey>
    where TKey : notnull
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(e => e.Id);

        if (typeof(TKey) == typeof(Guid))
        {
            builder.Property(e => e.Id).ValueGeneratedNever();
        }

        if (typeof(IHasConcurrencyStamp).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(IHasConcurrencyStamp.RowVersion)).IsRowVersion();
        }

        if (typeof(IActivableEntity).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(IActivableEntity.IsActive)).HasDefaultValue(true);
        }
    }
}
