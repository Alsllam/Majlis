using Majlis.Audit.Domain.Constants;
using Majlis.Audit.Domain.Entities;
using Majlis.Framework.EntityFrameworkCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Audit.EntityFrameworkCore.Configurations;

public sealed class AuditEntryConfiguration : DefaultEntityTypeConfiguration<AuditEntry, Guid>
{
    public override void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("Entries");
        builder.Property(e => e.Category).HasMaxLength(AuditFieldDefinitions.MaxCategoryLength).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(AuditFieldDefinitions.MaxActionLength).IsRequired();
        builder.Property(e => e.ActorKind).HasMaxLength(AuditFieldDefinitions.MaxActorKindLength).IsRequired();
        builder.Property(e => e.ActorDisplayName).HasMaxLength(Framework.Domain.Localization.FieldDefinitions.MaxNameLength);
        builder.Property(e => e.EntityType).HasMaxLength(AuditFieldDefinitions.MaxEntityTypeLength);
        builder.Property(e => e.DetailsJson).HasMaxLength(AuditFieldDefinitions.MaxDetailsLength).IsRequired();
        builder.Property(e => e.SourceKey).HasMaxLength(AuditFieldDefinitions.MaxSourceKeyLength).IsRequired();
        builder.Property(e => e.PreviousHash).HasMaxLength(AuditFieldDefinitions.HashLength).IsRequired();
        builder.Property(e => e.Hash).HasMaxLength(AuditFieldDefinitions.HashLength).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Seq }).IsUnique();
        builder.HasIndex(e => e.SourceKey).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt });
        builder.HasIndex(e => new { e.WorkspaceId, e.OccurredAt });
    }
}

public sealed class AuditChainConfiguration : DefaultEntityTypeConfiguration<AuditChain, Guid>
{
    public override void Configure(EntityTypeBuilder<AuditChain> builder)
    {
        base.Configure(builder);
        builder.ToTable("Chains");
        builder.Property(c => c.LastHash).HasMaxLength(AuditFieldDefinitions.HashLength).IsRequired();
        builder.HasIndex(c => c.TenantId).IsUnique();
    }
}
