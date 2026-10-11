using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore;
using Majlis.Meetings.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Meetings.EntityFrameworkCore;

public class MeetingsDbContext(DbContextOptions<MeetingsDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : MajlisDbContext(options, currentUser, clock)
{
    public const string SchemaName = "meetings";

    protected override string Schema => SchemaName;

    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Minutes> Minutes => Set<Minutes>();
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();
}
