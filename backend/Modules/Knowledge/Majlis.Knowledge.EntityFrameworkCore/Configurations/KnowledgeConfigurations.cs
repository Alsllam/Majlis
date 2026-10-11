using Majlis.Framework.EntityFrameworkCore.Configuration;
using Majlis.Knowledge.Domain.Constants;
using Majlis.Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Knowledge.EntityFrameworkCore.Configurations;

public sealed class DocumentConfiguration : DefaultEntityTypeConfiguration<Document, Guid>
{
    public override void Configure(EntityTypeBuilder<Document> builder)
    {
        base.Configure(builder);
        builder.ToTable("Documents");
        builder.Property(d => d.Title).HasMaxLength(KnowledgeFieldDefinitions.MaxTitleLength).IsRequired();
        builder.Property(d => d.Type).HasConversion<int>();
        builder.Property(d => d.Origin).HasConversion<int>();
        builder.Property(d => d.Language).HasMaxLength(Framework.Domain.Localization.FieldDefinitions.LanguageCodeLength);
        builder.Property(d => d.Tags).HasMaxLength(KnowledgeFieldDefinitions.MaxTagsLength);
        builder.HasIndex(d => new { d.WorkspaceId, d.RoomId });
        builder.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(d => d.AclGroups);
        builder.Ignore(d => d.LatestVersion);
    }
}

public sealed class DocumentVersionConfiguration : DefaultEntityTypeConfiguration<DocumentVersion, Guid>
{
    public override void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        base.Configure(builder);
        builder.ToTable("DocumentVersions");
        builder.Property(v => v.FileName).HasMaxLength(KnowledgeFieldDefinitions.MaxFileNameLength).IsRequired();
        builder.Property(v => v.ContentType).HasMaxLength(KnowledgeFieldDefinitions.MaxContentTypeLength).IsRequired();
        builder.Property(v => v.BlobPath).HasMaxLength(KnowledgeFieldDefinitions.MaxBlobPathLength).IsRequired();
        builder.Property(v => v.Sha256).HasMaxLength(KnowledgeFieldDefinitions.Sha256Length);
        builder.Property(v => v.FailureReasonKey).HasMaxLength(KnowledgeFieldDefinitions.MaxReasonLength);
        builder.Property(v => v.FailureDetail).HasMaxLength(KnowledgeFieldDefinitions.MaxDetailLength);
        builder.Property(v => v.Status).HasConversion<int>();
        builder.HasIndex(v => new { v.DocumentId, v.Number }).IsUnique();
    }
}

public sealed class WorkspaceMembershipConfiguration : DefaultEntityTypeConfiguration<WorkspaceMembership, Guid>
{
    public override void Configure(EntityTypeBuilder<WorkspaceMembership> builder)
    {
        base.Configure(builder);
        builder.ToTable("WorkspaceMemberships");
        builder.Property(m => m.DisplayName).HasMaxLength(Framework.Domain.Localization.FieldDefinitions.MaxNameLength).IsRequired();
        builder.Property(m => m.Role).HasMaxLength(32).IsRequired();
        builder.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();
        builder.Ignore(m => m.CanContribute);
        builder.Ignore(m => m.CanManage);
    }
}
