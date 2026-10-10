using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Knowledge.EntityFrameworkCore;

public static class KnowledgeEntityFrameworkCoreModule
{
    public static IServiceCollection AddKnowledgeEntityFrameworkCoreModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddMajlisDbContext<KnowledgeDbContext>(configuration, KnowledgeDbContext.SchemaName);
}
