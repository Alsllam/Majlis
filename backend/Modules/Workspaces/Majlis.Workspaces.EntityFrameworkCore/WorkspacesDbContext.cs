using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore;
using Majlis.Workspaces.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Workspaces.EntityFrameworkCore;

public class WorkspacesDbContext(DbContextOptions<WorkspacesDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : MajlisDbContext(options, currentUser, clock)
{
    public const string SchemaName = "workspaces";

    protected override string Schema => SchemaName;

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> Members => Set<WorkspaceMember>();
}
