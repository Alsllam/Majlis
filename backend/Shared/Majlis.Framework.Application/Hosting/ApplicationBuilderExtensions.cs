using Majlis.Framework.Application.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Majlis.Framework.Application.Hosting;

public static class ApplicationBuilderExtensions
{
    /// <summary>The standard pipeline, in the order the skill requires.</summary>
    public static WebApplication UseMajlisApiPipeline(this WebApplication app)
    {
        app.UseLocalizationMiddleware();
        app.UseSerilogRequestLogging();
        app.UseExceptionHandlingMiddleware();
        if (!app.Environment.IsDevelopmentLike())
        {
            app.UseHttpsRedirection();
        }

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
        app.MapControllers().RequireRateLimiting(ServiceCollectionExtensions.SlidingPolicy);
        return app;
    }
}
