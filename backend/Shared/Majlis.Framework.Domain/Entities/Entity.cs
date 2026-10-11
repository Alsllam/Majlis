namespace Majlis.Framework.Domain.Entities;

/// <summary>Base for every entity. Equality is by identity.</summary>
public abstract class Entity<TKey> where TKey : notnull
{
    public TKey Id { get; protected set; } = default!;

    protected Entity()
    {
    }

    protected Entity(TKey id) => Id = id;
}

/// <summary>Adds creation audit columns, filled by the unit of work.</summary>
public abstract class CreationAuditedEntity<TKey> : Entity<TKey>, ICreationAudited where TKey : notnull
{
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }

    protected CreationAuditedEntity()
    {
    }

    protected CreationAuditedEntity(TKey id) : base(id)
    {
    }
}

/// <summary>Adds modification audit columns.</summary>
public abstract class AuditedEntity<TKey> : CreationAuditedEntity<TKey>, IModificationAudited where TKey : notnull
{
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }

    protected AuditedEntity()
    {
    }

    protected AuditedEntity(TKey id) : base(id)
    {
    }
}

/// <summary>Adds soft delete. The default base for business data.</summary>
public abstract class FullAuditedEntity<TKey> : AuditedEntity<TKey>, ISoftDelete where TKey : notnull
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public Guid? DeleterId { get; set; }

    protected FullAuditedEntity()
    {
    }

    protected FullAuditedEntity(TKey id) : base(id)
    {
    }
}
