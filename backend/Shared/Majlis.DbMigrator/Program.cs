using Majlis.DbMigrator;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Security;
using Majlis.Identity.Domain.Entities;
using Majlis.Identity.EntityFrameworkCore;
using Majlis.Rooms.EntityFrameworkCore;
using Majlis.Workspaces.EntityFrameworkCore;
using Majlis.Knowledge.EntityFrameworkCore;
using Majlis.Approvals.EntityFrameworkCore;
using Majlis.Tasks.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Applies every module's migrations, then runs idempotent seeders. Hosts never migrate at startup.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
var config = builder.Configuration;
var connectionString = config.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICurrentUser, SystemCurrentUser>();
builder.Services.Configure<SeedOptions>(config.GetSection("Seed"));
builder.Services.AddDbContext<MajlisIdentityDbContext>(o =>
{
    o.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", MajlisIdentityDbContext.SchemaName));
    o.UseOpenIddict<Guid>();
});
builder.Services.AddDbContext<RoomsDbContext>(o =>
    o.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", RoomsDbContext.SchemaName)));
builder.Services.AddDbContext<WorkspacesDbContext>(o =>
    o.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", WorkspacesDbContext.SchemaName)));
builder.Services.AddDbContext<KnowledgeDbContext>(o =>
    o.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", KnowledgeDbContext.SchemaName)));
builder.Services.AddDbContext<ApprovalsDbContext>(o =>
    o.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ApprovalsDbContext.SchemaName)));
builder.Services.AddDbContext<TasksDbContext>(o =>
    o.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", TasksDbContext.SchemaName)));
builder.Services.AddIdentityCore<MajlisUser>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<MajlisRole>()
    .AddEntityFrameworkStores<MajlisIdentityDbContext>();
builder.Services.AddOpenIddict().AddCore(o => o.UseEntityFrameworkCore().UseDbContext<MajlisIdentityDbContext>().ReplaceDefaultEntities<Guid>());
builder.Services.AddScoped<DataSeeder>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

await scope.ServiceProvider.GetRequiredService<MajlisIdentityDbContext>().Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>().Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<RoomsDbContext>().Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>().Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<ApprovalsDbContext>().Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<TasksDbContext>().Database.MigrateAsync();
Console.WriteLine("Migrations applied");

await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
Console.WriteLine("Seed completed");
