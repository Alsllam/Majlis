namespace Majlis.Framework.Domain.Entities;

/// <summary>Soft-deleted rows are hidden by a global query filter.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletionTime { get; set; }
    Guid? DeleterId { get; set; }
}

/// <summary>Rows owned by one tenant. The DbContext filters by the current tenant; the value always comes from the token.</summary>
public interface IMultiTenant
{
    Guid TenantId { get; }
}

/// <summary>Anything that can be enabled or disabled.</summary>
public interface IActivableEntity
{
    bool IsActive { get; set; }
}

/// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
public interface IHasConcurrencyStamp
{
    byte[] RowVersion { get; set; }
}

/// <summary>Creation audit columns, filled by the DbContext.</summary>
public interface ICreationAudited
{
    DateTime CreationTime { get; set; }
    Guid? CreatorId { get; set; }
}

/// <summary>Modification audit columns, filled by the DbContext.</summary>
public interface IModificationAudited
{
    DateTime? LastModificationTime { get; set; }
    Guid? LastModifierId { get; set; }
}
