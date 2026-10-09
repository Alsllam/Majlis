using Majlis.Framework.Application.Hosting;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Security;
using Majlis.Realtime.Host.Auth;
using Majlis.Realtime.Host.Clients;
using Majlis.Realtime.Host.Hubs;
using Majlis.Realtime.Host.Presence;
using Majlis.Realtime.Host.Streaming;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Refit;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;
var redisConnection = config.GetConnectionString("Redis") ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");

services.AddSingleton(TimeProvider.System);
services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
services.AddSingleton<RealtimeTicketStore>();
services.AddSingleton<PresenceStore>();
services.AddSingleton<LocalConnections>();
services.AddMemoryCache();
services.AddCORSExtensions(config);
services.AddLoggingService(config);

services.AddAuthentication(TicketAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TicketAuthenticationHandler>(TicketAuthenticationHandler.SchemeName, null);
services.AddAuthorization();
services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();

services.AddSignalR(o =>
    {
        o.MaximumReceiveMessageSize = 32 * 1024;
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(40);
    })
    .AddStackExchangeRedis(redisConnection, o => o.Configuration.ChannelPrefix = RedisChannel.Literal("majlis:signalr"));

services.Configure<InternalAuthOptions>(config.GetSection("InternalAuth"));
services.AddHttpClient<ServiceTokenProvider>();
services.AddTransient<ServiceTokenHandler>();
services.AddRefitGeneratedClient<IRoomsInternalClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(config["Services:RoomsUrl"] ?? "http://localhost:7020"))
    .AddHttpMessageHandler<ServiceTokenHandler>();

services.AddMajlisMessaging(config, "realtime", typeof(Program).Assembly);
services.AddHostedService<StreamRelay>();
services.AddHostedService<PresenceSweeper>();
services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();
app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseCors(ServiceCollectionExtensions.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHub<SessionHub>(SessionHub.Path);
app.Run();

/// <summary>SignalR user id = the token subject, so <c>Clients.User(id)</c> works across instances.</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst(MajlisClaimTypes.Subject)?.Value;
}

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;
