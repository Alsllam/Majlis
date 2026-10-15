using Majlis.Framework.EntityFrameworkCore.Configuration;
using Majlis.Workspaces.Domain.Constants;
using Majlis.Workspaces.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Workspaces.EntityFrameworkCore.Configurations;

public sealed class WorkspaceConfiguration : DefaultEntityTypeConfiguration<Workspace, Guid>
{
    public override void Configure(EntityTypeBuilder<Workspace> builder)
    {
        base.Configure(builder);
        builder.ToTable("Workspaces");
        builder.Property(w => w.Name).HasMaxLength(WorkspacesFieldDefinitions.MaxNameLength).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(WorkspacesFieldDefinitions.MaxDescriptionLength);
        builder.Property(w => w.Icon).HasMaxLength(WorkspacesFieldDefinitions.MaxIconLength);
        builder.Property(w => w.Color).HasMaxLength(WorkspacesFieldDefinitions.MaxColorLength);
        builder.Property(w => w.AgentInstructions).HasMaxLength(WorkspacesFieldDefinitions.MaxInstructionsLength);
        builder.HasIndex(w => new { w.TenantId, w.Name });
        builder.HasMany(w => w.Members).WithOne().HasForeignKey(m => m.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(w => w.Groups).WithOne().HasForeignKey(g => g.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Groups).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class WorkspaceGroupGrantConfiguration : DefaultEntityTypeConfiguration<WorkspaceGroupGrant, Guid>
{
    public override void Configure(EntityTypeBuilder<WorkspaceGroupGrant> builder)
    {
        base.Configure(builder);
        builder.ToTable("GroupGrants");
        builder.Property(g => g.GroupName).HasMaxLength(WorkspacesFieldDefinitions.MaxNameLength).IsRequired();
        builder.Property(g => g.Role).HasConversion<int>();
        builder.HasIndex(g => new { g.WorkspaceId, g.GroupId }).IsUnique();
        builder.HasIndex(g => g.GroupId);
    }
}

public sealed class GroupMembershipConfiguration : DefaultEntityTypeConfiguration<GroupMembership, Guid>
{
    public override void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        base.Configure(builder);
        builder.ToTable("GroupMemberships");
        builder.Property(g => g.GroupName).HasMaxLength(WorkspacesFieldDefinitions.MaxNameLength).IsRequired();
        builder.Property(g => g.DisplayName).HasMaxLength(WorkspacesFieldDefinitions.MaxDisplayNameLength).IsRequired();
        builder.HasIndex(g => new { g.GroupId, g.UserId }).IsUnique();
    }
}

public sealed class WorkspaceMemberConfiguration : DefaultEntityTypeConfiguration<WorkspaceMember, Guid>
{
    public override void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        base.Configure(builder);
        builder.ToTable("Members");
        builder.Property(m => m.DisplayName).HasMaxLength(WorkspacesFieldDefinitions.MaxDisplayNameLength).IsRequired();
        builder.Property(m => m.Role).HasConversion<int>();
        builder.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.ViaGroupId);
    }
}
