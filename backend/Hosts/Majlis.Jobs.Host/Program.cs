using Hangfire;
using Hangfire.SqlServer;
using Majlis.Framework.Application.Hosting;
using Majlis.Framework.Application.Messaging;
using Majlis.Framework.Domain.Events;
using Majlis.Jobs.Host.Jobs;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;
var connectionString = config.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
var jobsOptions = config.GetSection("Jobs").Get<JobsOptions>() ?? new JobsOptions();

services.Configure<JobsOptions>(config.GetSection("Jobs"));
services.AddSingleton(TimeProvider.System);
services.AddLoggingService(config);
services.AddScoped<JobPublisher>();
// The host has no business database: ticks go straight to the bus (Hangfire keeps the schedule and retries a failed publish).
builder.AddMajlisMessaging(
    "jobs",
    new MessagingTopology()
        .Publish<ApprovalExpirySweepRequested>()
        .Publish<DailyDigestRequested>()
        .Publish<AuditChainVerificationRequested>()
        .Publish<RecordingRetentionSweepRequested>(),
    typeof(Program).Assembly);
services.AddHangfire(h => h
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions { SchemaName = jobsOptions.Schema, PrepareSchemaIfNecessary = true, QueuePollInterval = TimeSpan.FromSeconds(15) }));
services.AddHangfireServer(o => o.WorkerCount = 2);
services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();
app.MapHealthChecks("/health");
if (app.Services.GetRequiredService<IOptions<JobsOptions>>().Value.Dashboard)
{
    app.MapHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new LocalRequestsOnlyFilter()], DashboardTitle = "Majlis jobs" });
}

var crons = app.Services.GetRequiredService<IOptions<JobsOptions>>().Value.Crons;
var recurring = app.Services.GetRequiredService<IRecurringJobManager>();
foreach (var job in JobSchedule.Jobs)
{
    var cron = crons.GetValueOrDefault(job.Id, job.Cron);
    recurring.AddOrUpdate<JobPublisher>(job.Id, p => p.PublishAsync(job.Id, CancellationToken.None), cron, new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}

app.Run();

/// <summary>Entry point marker for tests.</summary>
public partial class Program;
