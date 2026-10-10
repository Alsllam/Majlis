using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore;
using Majlis.Tasks.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Tasks.EntityFrameworkCore;

public class TasksDbContext(DbContextOptions<TasksDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : MajlisDbContext(options, currentUser, clock)
{
    public const string SchemaName = "tasks";

    protected override string Schema => SchemaName;

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();
}
