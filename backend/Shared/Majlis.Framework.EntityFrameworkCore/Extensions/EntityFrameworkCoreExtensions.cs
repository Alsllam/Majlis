using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Framework.EntityFrameworkCore.Extensions;

public static class EntityFrameworkCoreExtensions
{
    /// <summary>
    /// Registers a module DbContext on SQL Server with its own schema and migrations history table,
    /// and binds the generic repositories and unit of work to it. One module DbContext per host.
    /// </summary>
    public static IServiceCollection AddMajlisDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema, string connectionStringName = "Default")
        where TContext : MajlisDbContext
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' is missing.");

        // No retrying execution strategy: Wolverine's outbox commits the rows and the events in one transaction it
        // opens itself, which EF's retry strategy forbids. Transient failures are retried at the message level instead.
        services.AddDbContext<TContext>(
            options => options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema)),
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddMajlisRepositories<TContext>();
        return services;
    }

    /// <summary>Binds repositories and the unit of work to an already registered DbContext (also used by tests).</summary>
    public static IServiceCollection AddMajlisRepositories<TContext>(this IServiceCollection services)
        where TContext : MajlisDbContext
    {
        services.AddScoped<MajlisDbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.AddScoped(typeof(IReadOnlyRepository<,>), typeof(EfRepository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
