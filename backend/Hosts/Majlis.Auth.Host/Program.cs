using System.Security.Cryptography.X509Certificates;
using Majlis.Auth.Host;
using Majlis.Framework.Application.DynamicControllers;
using Majlis.Framework.Application.Hosting;
using Majlis.Framework.Application.Middleware;
using Majlis.Identity.Application;
using Majlis.Identity.Domain.Constants;
using Majlis.Identity.Domain.Entities;
using Majlis.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;
var server = config.GetSection("Auth").Get<AuthServerOptions>() ?? new AuthServerOptions();

services.AddIdentityApplicationModule(config);
var mvc = services.AddControllersWithViews();
services.AddRazorPages();
services.AddCORSExtensions(config);
services.AddLocalizationService();
services.AddMajlisSwagger("Majlis Auth API");
services.AddDbContext<MajlisIdentityDbContext>(o =>
{
    o.UseSqlServer(config.GetConnectionString("Default"), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", MajlisIdentityDbContext.SchemaName));
    o.UseOpenIddict<Guid>();
});
services.AddIdentity<MajlisUser, MajlisRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 10;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.ClaimsIdentity.UserIdClaimType = OpenIddictConstants.Claims.Subject;
        o.ClaimsIdentity.UserNameClaimType = OpenIddictConstants.Claims.Name;
        o.ClaimsIdentity.RoleClaimType = OpenIddictConstants.Claims.Role;
    })
    .AddEntityFrameworkStores<MajlisIdentityDbContext>()
    .AddDefaultTokenProviders();
services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "majlis.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/Account/Login";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});
services.AddLoggingService(config);

services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<MajlisIdentityDbContext>().ReplaceDefaultEntities<Guid>())
    .AddServer(o =>
    {
        o.SetIssuer(new Uri(server.Issuer));
        o.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .SetEndSessionEndpointUris("connect/logout")
            .SetUserInfoEndpointUris("connect/userinfo");

        o.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        o.AllowRefreshTokenFlow();
        o.AllowClientCredentialsFlow();
        o.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Roles, OpenIddictConstants.Scopes.OfflineAccess,
            MajlisScopes.Api, MajlisScopes.AiApi, MajlisScopes.Internal);

        o.SetAccessTokenLifetime(TimeSpan.FromMinutes(server.AccessTokenMinutes));
        o.SetRefreshTokenLifetime(TimeSpan.FromDays(server.RefreshTokenDays));

        // APIs (and ai-service) validate signed JWTs through discovery, so access tokens are not encrypted.
        o.DisableAccessTokenEncryption();

        var certs = server.Certificates;
        if (!string.IsNullOrEmpty(certs.SigningPath) && !string.IsNullOrEmpty(certs.EncryptionPath))
        {
            o.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(certs.SigningPath, certs.SigningPassword));
            o.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(certs.EncryptionPath, certs.EncryptionPassword));
        }
        else if (builder.Environment.IsDevelopmentLike())
        {
            o.AddDevelopmentSigningCertificate().AddDevelopmentEncryptionCertificate();
        }
        else
        {
            throw new InvalidOperationException("Auth:Certificates are required outside Development.");
        }

        var aspNet = o.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough();
        if (builder.Environment.IsDevelopmentLike())
        {
            aspNet.DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(o =>
    {
        o.UseLocalServer();
        o.UseAspNetCore();
    });

services.AddMajlisAuthorization(config);
mvc.AddDynamicControllers(typeof(IdentityApplicationModule).Assembly);
services.SuppressModelStateInvalidFilter();
services.AddSlidingWindowRateLimiterStrategy(config);
services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();
app.UseForwardedHeaders();
app.UseLocalizationMiddleware();
app.UseSerilogRequestLogging();
app.UseExceptionHandlingMiddleware();
app.UseRouting();
app.UseCors(ServiceCollectionExtensions.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopmentLike())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapRazorPages();
app.MapControllers().RequireRateLimiting(ServiceCollectionExtensions.SlidingPolicy);
app.Run();
