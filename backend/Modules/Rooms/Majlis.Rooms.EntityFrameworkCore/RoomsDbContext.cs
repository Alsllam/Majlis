using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore;
using Majlis.Rooms.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Rooms.EntityFrameworkCore;

public class RoomsDbContext(DbContextOptions<RoomsDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : MajlisDbContext(options, currentUser, clock)
{
    public const string SchemaName = "rooms";

    protected override string Schema => SchemaName;

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomParticipant> RoomParticipants => Set<RoomParticipant>();
    public DbSet<AgentSession> Sessions => Set<AgentSession>();
    public DbSet<ControlRequest> ControlRequests => Set<ControlRequest>();
    public DbSet<Turn> Turns => Set<Turn>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();
}
