using Majlis.Framework.EntityFrameworkCore.Configuration;
using Majlis.Notifications.Domain.Constants;
using Majlis.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Notifications.EntityFrameworkCore.Configurations;

public sealed class NotificationConfiguration : DefaultEntityTypeConfiguration<Notification, Guid>
{
    public override void Configure(EntityTypeBuilder<Notification> builder)
    {
        base.Configure(builder);
        builder.ToTable("Notifications");
        builder.Property(n => n.Type).HasMaxLength(NotificationsFieldDefinitions.MaxTypeLength).IsRequired();
        builder.Property(n => n.ParamsJson).HasMaxLength(NotificationsFieldDefinitions.MaxParamsLength).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(NotificationsFieldDefinitions.MaxLinkLength);
        builder.Property(n => n.DedupeKey).HasMaxLength(NotificationsFieldDefinitions.MaxDedupeKeyLength).IsRequired();
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => new { n.UserId, n.ReadAt });
        builder.HasIndex(n => n.DedupeKey).IsUnique();
        builder.Ignore(n => n.IsRead);
    }
}

public sealed class NotificationSettingsConfiguration : DefaultEntityTypeConfiguration<NotificationSettings, Guid>
{
    public override void Configure(EntityTypeBuilder<NotificationSettings> builder)
    {
        base.Configure(builder);
        builder.ToTable("Settings");
        builder.Property(s => s.MutedTypes).HasMaxLength(NotificationsFieldDefinitions.MaxMutedTypesLength).IsRequired();
        builder.HasIndex(s => s.UserId).IsUnique();
        builder.Ignore(s => s.MutedTypeList);
    }
}

public sealed class ApprovalRefConfiguration : DefaultEntityTypeConfiguration<ApprovalRef, Guid>
{
    public override void Configure(EntityTypeBuilder<ApprovalRef> builder)
    {
        base.Configure(builder);
        builder.ToTable("ApprovalRefs");
        builder.Property(a => a.Tool).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(Framework.Domain.Localization.FieldDefinitions.MaxDescriptionLength).IsRequired();
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
