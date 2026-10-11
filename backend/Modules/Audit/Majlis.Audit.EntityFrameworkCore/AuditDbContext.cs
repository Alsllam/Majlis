using Majlis.Audit.Domain.Entities;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Audit.EntityFrameworkCore;

public class AuditDbContext(DbContextOptions<AuditDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : MajlisDbContext(options, currentUser, clock)
{
    public const string SchemaName = "audit";

    protected override string Schema => SchemaName;

    public DbSet<AuditEntry> Entries => Set<AuditEntry>();
    public DbSet<AuditChain> Chains => Set<AuditChain>();
}
