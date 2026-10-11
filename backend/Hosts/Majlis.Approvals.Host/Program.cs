using Majlis.Framework.Application.DynamicControllers;
using Majlis.Framework.Application.Hosting;
using Majlis.Framework.Application.Messaging;
using Majlis.Approvals.Application;
using Majlis.Approvals.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

services.AddApprovalsApplicationModule(config);
var mvc = services.AddControllers();
services.AddCORSExtensions(config);
services.AddLocalizationService();
services.AddMajlisSwagger("Majlis Approvals API");
services.AddApprovalsEntityFrameworkCoreModule(config);
builder.AddMajlisMessaging<ApprovalsDbContext>(ApprovalsApplicationModule.ModuleName, ApprovalsDbContext.SchemaName, ApprovalsApplicationModule.Topology, typeof(ApprovalsApplicationModule).Assembly);
services.AddMajlisCache(config);
services.AddLoggingService(config);
services.AddOpenIddictExtension(config);
mvc.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
mvc.AddDynamicControllers(typeof(ApprovalsApplicationModule).Assembly);
services.SuppressModelStateInvalidFilter();
services.AddSlidingWindowRateLimiterStrategy(config);
services.AddApprovalsBackgroundServices();
services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();
app.UseMajlisApiPipeline();
app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;
