using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore;
using Majlis.Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Knowledge.EntityFrameworkCore;

public class KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : MajlisDbContext(options, currentUser, clock)
{
    public const string SchemaName = "knowledge";

    protected override string Schema => SchemaName;

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();
}
