using Majlis.Framework.Application.DynamicControllers;
using Majlis.Framework.Application.Hosting;
using Majlis.Rooms.Application;
using Majlis.Rooms.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

services.AddRoomsApplicationModule(config);
var mvc = services.AddControllers();
services.AddCORSExtensions(config);
services.AddLocalizationService();
services.AddMajlisSwagger("Majlis Rooms API");
services.AddRoomsEntityFrameworkCoreModule(config);
services.AddSharedEntityFrameworkCoreModule<RoomsDbContext>(config, RoomsApplicationModule.ModuleName, typeof(RoomsApplicationModule).Assembly);
services.AddMajlisCache(config);
services.AddLoggingService(config);
services.AddOpenIddictExtension(config);
mvc.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
mvc.AddDynamicControllers(typeof(RoomsApplicationModule).Assembly);
services.SuppressModelStateInvalidFilter();
services.AddSlidingWindowRateLimiterStrategy(config);
services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
services.AddRoomsBackgroundServices();

var app = builder.Build();
app.UseMajlisApiPipeline();
app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;
