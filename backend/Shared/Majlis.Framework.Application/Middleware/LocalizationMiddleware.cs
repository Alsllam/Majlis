using System.Globalization;
using Majlis.Framework.Application.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Majlis.Framework.Application.Middleware;

/// <summary>Sets the request culture from <c>Accept-Language</c> (ar or en; Arabic by default).</summary>
public sealed class LocalizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requested = context.Request.GetTypedHeaders().AcceptLanguage
            .OrderByDescending(l => l.Quality ?? 1)
            .Select(l => l.Value.Value?.Split('-')[0].ToLowerInvariant())
            .FirstOrDefault(l => l is not null && JsonLocalizer.SupportedCultures.Contains(l));

        var culture = CultureInfo.GetCultureInfo(requested ?? JsonLocalizer.DefaultCulture);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        context.Response.Headers.ContentLanguage = culture.TwoLetterISOLanguageName;
        await next(context);
    }
}

public static class LocalizationMiddlewareExtensions
{
    public static IApplicationBuilder UseLocalizationMiddleware(this IApplicationBuilder app) => app.UseMiddleware<LocalizationMiddleware>();
}
