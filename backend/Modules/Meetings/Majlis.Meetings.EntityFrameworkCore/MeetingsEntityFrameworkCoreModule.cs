using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Meetings.EntityFrameworkCore;

public static class MeetingsEntityFrameworkCoreModule
{
    public static IServiceCollection AddMeetingsEntityFrameworkCoreModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddMajlisDbContext<MeetingsDbContext>(configuration, MeetingsDbContext.SchemaName);
}
