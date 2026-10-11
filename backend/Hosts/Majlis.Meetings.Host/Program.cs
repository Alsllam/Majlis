using Majlis.Framework.Application.Adapters.Storage;
using Majlis.Framework.Application.DynamicControllers;
using Majlis.Framework.Application.Hosting;
using Majlis.Framework.Application.Messaging;
using Majlis.Meetings.Application;
using Majlis.Meetings.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

services.AddMeetingsApplicationModule(config);
var mvc = services.AddControllers();
services.AddCORSExtensions(config);
services.AddLocalizationService();
services.AddMajlisSwagger("Majlis Meetings API");
services.AddMeetingsEntityFrameworkCoreModule(config);
services.AddMajlisBlobStorage(config);
builder.AddMajlisMessaging<MeetingsDbContext>(MeetingsApplicationModule.ModuleName, MeetingsDbContext.SchemaName, MeetingsApplicationModule.Topology, typeof(MeetingsApplicationModule).Assembly);
services.AddMajlisCache(config);
services.AddLoggingService(config);
services.AddOpenIddictExtension(config);
mvc.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
mvc.AddDynamicControllers(typeof(MeetingsApplicationModule).Assembly);
services.SuppressModelStateInvalidFilter();
services.AddSlidingWindowRateLimiterStrategy(config);
services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();
// Browser uploads of recordings go straight to storage; CORS on the account comes from BlobStorage:CorsAllowedOrigins.
await app.Services.GetRequiredService<AzureBlobStorage>().ConfigureCorsAsync();
app.UseMajlisApiPipeline();
app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;
