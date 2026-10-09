using Majlis.Framework.EntityFrameworkCore.Configuration;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Majlis.Rooms.EntityFrameworkCore.Configurations;

public sealed class RoomConfiguration : DefaultEntityTypeConfiguration<Room, Guid>
{
    public override void Configure(EntityTypeBuilder<Room> builder)
    {
        base.Configure(builder);
        builder.ToTable("Rooms");
        builder.Property(r => r.Name).HasMaxLength(RoomsFieldDefinitions.MaxRoomNameLength).IsRequired();
        builder.Property(r => r.Purpose).HasMaxLength(RoomsFieldDefinitions.MaxPurposeLength);
        builder.Property(r => r.Visibility).HasConversion<int>();
        builder.HasIndex(r => r.WorkspaceId);
        builder.HasMany(r => r.Participants).WithOne().HasForeignKey(p => p.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RoomParticipantConfiguration : DefaultEntityTypeConfiguration<RoomParticipant, Guid>
{
    public override void Configure(EntityTypeBuilder<RoomParticipant> builder)
    {
        base.Configure(builder);
        builder.ToTable("RoomParticipants");
        builder.Property(p => p.DisplayName).HasMaxLength(RoomsFieldDefinitions.MaxDisplayNameLength).IsRequired();
        builder.Property(p => p.Role).HasConversion<int>();
        builder.HasIndex(p => new { p.RoomId, p.UserId }).IsUnique();
        builder.HasIndex(p => p.UserId);
    }
}

public sealed class AgentSessionConfiguration : DefaultEntityTypeConfiguration<AgentSession, Guid>
{
    public override void Configure(EntityTypeBuilder<AgentSession> builder)
    {
        base.Configure(builder);
        builder.ToTable("Sessions");
        builder.Property(s => s.Status).HasConversion<int>();
        builder.Property(s => s.DriverDisplayName).HasMaxLength(RoomsFieldDefinitions.MaxDisplayNameLength);
        builder.Property(s => s.PendingHandOffToDisplayName).HasMaxLength(RoomsFieldDefinitions.MaxDisplayNameLength);
        builder.Property(s => s.PendingHandOffNote).HasMaxLength(RoomsFieldDefinitions.MaxNoteLength);
        builder.HasIndex(s => new { s.RoomId, s.Status });
        builder.HasIndex(s => s.DriverAbsentSince).HasFilter("[DriverAbsentSince] IS NOT NULL");
        builder.HasMany(s => s.Requests).WithOne().HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Requests).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(s => s.Driver);
        builder.Ignore(s => s.IsHeld);
    }
}

public sealed class ControlRequestConfiguration : DefaultEntityTypeConfiguration<ControlRequest, Guid>
{
    public override void Configure(EntityTypeBuilder<ControlRequest> builder)
    {
        base.Configure(builder);
        builder.ToTable("ControlRequests");
        builder.Property(r => r.DisplayName).HasMaxLength(RoomsFieldDefinitions.MaxDisplayNameLength).IsRequired();
        builder.Property(r => r.Status).HasConversion<int>();
    }
}

public sealed class TurnConfiguration : DefaultEntityTypeConfiguration<Turn, Guid>
{
    public override void Configure(EntityTypeBuilder<Turn> builder)
    {
        base.Configure(builder);
        builder.ToTable("Turns");
        builder.Property(t => t.InstructedByDisplayName).HasMaxLength(RoomsFieldDefinitions.MaxDisplayNameLength).IsRequired();
        builder.Property(t => t.Instruction).HasMaxLength(RoomsFieldDefinitions.MaxInstructionLength).IsRequired();
        builder.Property(t => t.Language).HasMaxLength(Framework.Domain.Localization.FieldDefinitions.LanguageCodeLength);
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.FailureReasonKey).HasMaxLength(128);
        builder.HasIndex(t => new { t.SessionId, t.ClientRequestId }).IsUnique();
        builder.HasIndex(t => t.Status).HasFilter("[Status] = 0");
        builder.Ignore(t => t.IsRunning);
    }
}

public sealed class SessionEventConfiguration : DefaultEntityTypeConfiguration<SessionEvent, Guid>
{
    public override void Configure(EntityTypeBuilder<SessionEvent> builder)
    {
        base.Configure(builder);
        builder.ToTable("SessionEvents");
        builder.Property(e => e.Type).HasMaxLength(RoomsFieldDefinitions.MaxEventTypeLength).IsRequired();
        builder.Property(e => e.ActorKind).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ActorDisplayName).HasMaxLength(RoomsFieldDefinitions.MaxDisplayNameLength);
        builder.Property(e => e.DataJson).IsRequired();

        // The sequencer's guarantee: one row per (session, seq). A duplicate seq fails the transaction.
        builder.HasIndex(e => new { e.SessionId, e.Seq }).IsUnique();
    }
}
