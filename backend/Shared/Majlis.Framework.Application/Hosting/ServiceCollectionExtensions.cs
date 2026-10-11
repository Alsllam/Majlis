using System.Reflection;
using System.Threading.RateLimiting;
using FluentValidation;
using Majlis.Framework.Application.Localization;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Validation.AspNetCore;
using Serilog;

namespace Majlis.Framework.Application.Hosting;

/// <summary>Registration helpers used by every module host, in the order of the host Program.cs.</summary>
public static class ServiceCollectionExtensions
{
    public const string SlidingPolicy = "SlidingPolicy";
    public const string CorsPolicy = "MajlisCors";

    /// <summary>Explicit origin list from config; never "allow any origin" with credentials.</summary>
    public static IServiceCollection AddCORSExtensions(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
            .WithOrigins(settings.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
        return services;
    }

    public static IServiceCollection AddLocalizationService(this IServiceCollection services)
    {
        services.AddSingleton<ILocalizer, JsonLocalizer>();
        services.AddSingleton(TimeProvider.System);
        ValidatorOptions.Global.LanguageManager.Enabled = false;
        return services;
    }

    public static IServiceCollection AddLoggingService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(configuration).Enrich.FromLogContext());
        return services;
    }

    /// <summary>OpenIddict validation against the Auth host, plus permission policies.</summary>
    public static IServiceCollection AddOpenIddictExtension(this IServiceCollection services, IConfiguration configuration)
    {
        var auth = configuration.GetSection("Auth").Get<AuthSettings>() ?? new AuthSettings();
        services.AddOpenIddict().AddValidation(o =>
        {
            o.SetIssuer(auth.Issuer);
            o.AddAudiences(auth.Audience);
            o.UseSystemNetHttp();
            o.UseAspNetCore();
        });

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        services.AddMajlisAuthorization(configuration);
        return services;
    }

    /// <summary>Permission policies and the current user. Split out so tests can swap the authentication scheme.</summary>
    public static IServiceCollection AddMajlisAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.Configure<RolePermissionOptions>(o =>
        {
            foreach (var role in configuration.GetSection("RolePermissions").GetChildren())
            {
                o.Roles[role.Key] = role.Get<string[]>() ?? [];
            }
        });
        services.AddAuthorization(o => o.AddPolicy(InternalServicePolicy.Name, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(c => c.User.FindAll("scope").Concat(c.User.FindAll("oi_scp"))
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains(InternalServicePolicy.Scope))));
        return services;
    }

    /// <summary>Redis distributed cache, or in-memory when no Redis connection string is configured.</summary>
    public static IServiceCollection AddMajlisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "majlis:";
            });
        }

        return services;
    }

    /// <summary>Routes everything through the exception middleware instead of automatic 400s.</summary>
    public static IServiceCollection SuppressModelStateInvalidFilter(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
        return services;
    }

    public static IServiceCollection AddSlidingWindowRateLimiterStrategy(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("RateLimiter").Get<RateLimiterSettings>() ?? new RateLimiterSettings();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(SlidingPolicy, context => RateLimitPartition.GetSlidingWindowLimiter(
                context.User.FindFirst(MajlisClaimTypes.Subject)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    SegmentsPerWindow = settings.SegmentsPerWindow,
                    QueueLimit = 0,
                }));
        });
        return services;
    }

    /// <summary>Swagger only in Development; the generator is registered always, the UI is mapped only in Development.</summary>
    public static IServiceCollection AddMajlisSwagger(this IServiceCollection services, string title)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = title, Version = "v1" }));
        return services;
    }

    public static bool IsDevelopmentLike(this IHostEnvironment environment) => environment.IsDevelopment() || environment.IsEnvironment("Local");
}
