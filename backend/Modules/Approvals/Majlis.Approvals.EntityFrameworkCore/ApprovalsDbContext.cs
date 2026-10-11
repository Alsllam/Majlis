using Majlis.Approvals.Domain.Entities;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Approvals.EntityFrameworkCore;

public class ApprovalsDbContext(DbContextOptions<ApprovalsDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : MajlisDbContext(options, currentUser, clock)
{
    public const string SchemaName = "approvals";

    protected override string Schema => SchemaName;

    public DbSet<ApprovalRequest> Requests => Set<ApprovalRequest>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();
}
