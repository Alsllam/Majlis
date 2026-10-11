using Majlis.Framework.Application.DynamicControllers;
using Majlis.Framework.Application.Hosting;
using Majlis.Framework.Application.Messaging;
using Majlis.Audit.Application;
using Majlis.Audit.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

services.AddAuditApplicationModule(config);
var mvc = services.AddControllers();
services.AddCORSExtensions(config);
services.AddLocalizationService();
services.AddMajlisSwagger("Majlis.Audit API");
services.AddAuditEntityFrameworkCoreModule(config);
builder.AddMajlisMessaging<AuditDbContext>(AuditApplicationModule.ModuleName, AuditDbContext.SchemaName, AuditApplicationModule.Topology, typeof(AuditApplicationModule).Assembly);
services.AddMajlisCache(config);
services.AddLoggingService(config);
services.AddOpenIddictExtension(config);
mvc.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
mvc.AddDynamicControllers(typeof(AuditApplicationModule).Assembly);
services.SuppressModelStateInvalidFilter();
services.AddSlidingWindowRateLimiterStrategy(config);
services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();
app.UseMajlisApiPipeline();
app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;
