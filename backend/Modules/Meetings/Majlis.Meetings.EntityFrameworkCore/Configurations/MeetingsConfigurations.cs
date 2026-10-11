using Majlis.Framework.EntityFrameworkCore.Configuration;
using Majlis.Meetings.Domain.Constants;
using Majlis.Meetings.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Meetings.EntityFrameworkCore.Configurations;

public sealed class MeetingConfiguration : DefaultEntityTypeConfiguration<Meeting, Guid>
{
    public override void Configure(EntityTypeBuilder<Meeting> builder)
    {
        base.Configure(builder);
        builder.ToTable("Meetings");
        builder.Property(m => m.Title).HasMaxLength(MeetingsFieldDefinitions.MaxTitleLength).IsRequired();
        builder.Property(m => m.Language).HasMaxLength(Framework.Domain.Localization.FieldDefinitions.LanguageCodeLength).IsRequired();
        builder.Property(m => m.AttendeesJson).HasMaxLength(MeetingsFieldDefinitions.MaxAttendeesLength).IsRequired();
        builder.Property(m => m.Source).HasConversion<int>();
        builder.Property(m => m.Status).HasConversion<int>();
        builder.Property(m => m.RecordingBlobPath).HasMaxLength(512);
        builder.Property(m => m.RecordingContentType).HasMaxLength(100);
        builder.Property(m => m.TranscriptText).HasMaxLength(MeetingsFieldDefinitions.MaxTranscriptLength);
        builder.Property(m => m.FailureReasonKey).HasMaxLength(100);
        builder.HasIndex(m => new { m.WorkspaceId, m.HeldAt });
        builder.HasIndex(m => m.RoomId);
        builder.HasOne(m => m.Minutes).WithOne().HasForeignKey<Minutes>(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(m => m.Attendees);
        builder.Ignore(m => m.HasTranscript);
    }
}

public sealed class MinutesConfiguration : DefaultEntityTypeConfiguration<Minutes, Guid>
{
    public override void Configure(EntityTypeBuilder<Minutes> builder)
    {
        base.Configure(builder);
        builder.ToTable("Minutes");
        builder.Property(m => m.Summary).HasMaxLength(MeetingsFieldDefinitions.MaxSummaryLength).IsRequired();
        builder.Property(m => m.DecisionsJson).HasMaxLength(MeetingsFieldDefinitions.MaxListJsonLength).IsRequired();
        builder.Property(m => m.OpenQuestionsJson).HasMaxLength(MeetingsFieldDefinitions.MaxListJsonLength).IsRequired();
        builder.Property(m => m.Language).HasMaxLength(Framework.Domain.Localization.FieldDefinitions.LanguageCodeLength).IsRequired();
        builder.Property(m => m.Status).HasConversion<int>();
        builder.Property(m => m.ApprovedByDisplayName).HasMaxLength(Framework.Domain.Localization.FieldDefinitions.MaxNameLength);
        builder.HasIndex(m => m.MeetingId).IsUnique();
        builder.HasMany(m => m.ActionItems).WithOne().HasForeignKey(i => i.MinutesId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(m => m.ActionItems).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(m => m.Decisions);
        builder.Ignore(m => m.OpenQuestions);
    }
}

public sealed class ActionItemConfiguration : DefaultEntityTypeConfiguration<ActionItem, Guid>
{
    public override void Configure(EntityTypeBuilder<ActionItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("ActionItems");
        builder.Property(i => i.Text).HasMaxLength(MeetingsFieldDefinitions.MaxItemTextLength).IsRequired();
        builder.Property(i => i.OwnerName).HasMaxLength(MeetingsFieldDefinitions.MaxOwnerLength);
        builder.HasIndex(i => i.ApprovalRequestId).IsUnique().HasFilter("[ApprovalRequestId] IS NOT NULL");
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
