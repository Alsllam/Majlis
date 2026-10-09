using Majlis.DbMigrator;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Security;
using Majlis.Identity.Domain.Entities;
using Majlis.Identity.EntityFrameworkCore;
using Majlis.Rooms.EntityFrameworkCore;
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
builder.Services.AddIdentityCore<MajlisUser>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<MajlisRole>()
    .AddEntityFrameworkStores<MajlisIdentityDbContext>();
builder.Services.AddOpenIddict().AddCore(o => o.UseEntityFrameworkCore().UseDbContext<MajlisIdentityDbContext>().ReplaceDefaultEntities<Guid>());
builder.Services.AddScoped<DataSeeder>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

await scope.ServiceProvider.GetRequiredService<MajlisIdentityDbContext>().Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<RoomsDbContext>().Database.MigrateAsync();
Console.WriteLine("Migrations applied");

await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
Console.WriteLine("Seed completed");
