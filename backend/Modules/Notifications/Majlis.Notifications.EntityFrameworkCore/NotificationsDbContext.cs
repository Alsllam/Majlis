using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore;
using Majlis.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Notifications.EntityFrameworkCore;

public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : MajlisDbContext(options, currentUser, clock)
{
    public const string SchemaName = "notifications";

    protected override string Schema => SchemaName;

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationSettings> Settings => Set<NotificationSettings>();
    public DbSet<ApprovalRef> ApprovalRefs => Set<ApprovalRef>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();
}
