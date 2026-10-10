using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Tasks.EntityFrameworkCore;

public static class TasksEntityFrameworkCoreModule
{
    public static IServiceCollection AddTasksEntityFrameworkCoreModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddMajlisDbContext<TasksDbContext>(configuration, TasksDbContext.SchemaName);
}
