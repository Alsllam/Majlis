using Majlis.Framework.EntityFrameworkCore.Configuration;
using Majlis.Tasks.Domain.Constants;
using Majlis.Tasks.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Tasks.EntityFrameworkCore.Configurations;

public sealed class TaskItemConfiguration : DefaultEntityTypeConfiguration<TaskItem, Guid>
{
    public override void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("Tasks");
        builder.Property(t => t.Title).HasMaxLength(TasksFieldDefinitions.MaxTitleLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(TasksFieldDefinitions.MaxDescriptionLength);
        builder.Property(t => t.AssigneeDisplayName).HasMaxLength(TasksFieldDefinitions.MaxDisplayNameLength);
        builder.Property(t => t.Priority).HasConversion<int>();
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.Origin).HasConversion<int>();
        builder.HasIndex(t => new { t.WorkspaceId, t.Status });
        builder.HasIndex(t => t.AssigneeUserId);
        builder.HasIndex(t => t.ApprovalRequestId).IsUnique().HasFilter("[ApprovalRequestId] IS NOT NULL");
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
