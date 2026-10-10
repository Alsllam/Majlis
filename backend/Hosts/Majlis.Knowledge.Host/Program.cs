using Majlis.Framework.Application.Adapters.Storage;
using Majlis.Framework.Application.DynamicControllers;
using Majlis.Framework.Application.Hosting;
using Majlis.Framework.Application.Messaging;
using Majlis.Knowledge.Application;
using Majlis.Knowledge.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

services.AddKnowledgeApplicationModule(config);
var mvc = services.AddControllers();
services.AddCORSExtensions(config);
services.AddLocalizationService();
services.AddMajlisSwagger("Majlis Knowledge API");
services.AddKnowledgeEntityFrameworkCoreModule(config);
services.AddMajlisBlobStorage(config);
builder.AddMajlisMessaging<KnowledgeDbContext>(KnowledgeApplicationModule.ModuleName, KnowledgeDbContext.SchemaName, KnowledgeApplicationModule.Topology, typeof(KnowledgeApplicationModule).Assembly);
services.AddMajlisCache(config);
services.AddLoggingService(config);
services.AddOpenIddictExtension(config);
mvc.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
mvc.AddDynamicControllers(typeof(KnowledgeApplicationModule).Assembly);
services.SuppressModelStateInvalidFilter();
services.AddSlidingWindowRateLimiterStrategy(config);
services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();
// Browser uploads go straight to storage; the origins come from BlobStorage:CorsAllowedOrigins (dev + Azurite).
await app.Services.GetRequiredService<AzureBlobStorage>().ConfigureCorsAsync();
app.UseMajlisApiPipeline();
app.Run();

/// <summary>Entry point marker for integration tests.</summary>
public partial class Program;
