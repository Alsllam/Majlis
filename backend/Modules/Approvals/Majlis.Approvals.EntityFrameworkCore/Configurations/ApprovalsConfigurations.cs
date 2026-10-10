using Majlis.Approvals.Domain.Constants;
using Majlis.Approvals.Domain.Entities;
using Majlis.Framework.EntityFrameworkCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Approvals.EntityFrameworkCore.Configurations;

public sealed class ApprovalRequestConfiguration : DefaultEntityTypeConfiguration<ApprovalRequest, Guid>
{
    public override void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        base.Configure(builder);
        builder.ToTable("Requests");
        builder.Property(r => r.Tool).HasMaxLength(ApprovalsFieldDefinitions.MaxToolLength).IsRequired();
        builder.Property(r => r.ArgsJson).HasMaxLength(ApprovalsFieldDefinitions.MaxArgsLength).IsRequired();
        builder.Property(r => r.EditedArgsJson).HasMaxLength(ApprovalsFieldDefinitions.MaxArgsLength);
        builder.Property(r => r.Summary).HasMaxLength(ApprovalsFieldDefinitions.MaxSummaryLength).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(ApprovalsFieldDefinitions.MaxReasonLength);
        builder.Property(r => r.RequestedByDisplayName).HasMaxLength(ApprovalsFieldDefinitions.MaxDisplayNameLength).IsRequired();
        builder.Property(r => r.DecidedByDisplayName).HasMaxLength(ApprovalsFieldDefinitions.MaxDisplayNameLength);
        builder.Property(r => r.DecisionNote).HasMaxLength(ApprovalsFieldDefinitions.MaxReasonLength);
        builder.Property(r => r.ResultSummary).HasMaxLength(ApprovalsFieldDefinitions.MaxResultLength);
        builder.Property(r => r.FailureReasonKey).HasMaxLength(128);
        builder.Property(r => r.Risk).HasConversion<int>();
        builder.Property(r => r.Status).HasConversion<int>();
        builder.HasIndex(r => new { r.WorkspaceId, r.Status });
        builder.HasIndex(r => r.SessionId);
        builder.HasIndex(r => new { r.Status, r.ExpiresAt });
        builder.Ignore(r => r.EffectiveArgsJson);
        builder.Ignore(r => r.IsPending);
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
