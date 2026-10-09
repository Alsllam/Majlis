using System.Net;
using System.Text.Json;
using FluentValidation;
using Majlis.Framework.Application.Localization;
using Majlis.Framework.Domain.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Majlis.Framework.Application.Middleware;

/// <summary>
/// The only place exceptions become HTTP responses. Every error has the shape
/// <c>{ "error": { "code", "date", "messages": [], "source" } }</c>.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, ILocalizer localizer, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, source, messages) = Map(ex);
            if (ex is not MajlisException && status >= 500)
            {
                Log.Unhandled(logger, ex, context.Request.Method, context.Request.Path);
            }
            else
            {
                Log.Rejected(logger, status, source, context.Request.Method, context.Request.Path);
            }

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new
                {
                    code = status.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    date = clock.GetUtcNow().UtcDateTime,
                    messages,
                    source,
                },
            }, Json));
        }
    }

    private (int Status, string Source, IReadOnlyList<string> Messages) Map(Exception ex) => ex switch
    {
        ValidationException v => (400, "Validation", v.Errors.Select(Translate).Distinct().ToList()),
        CustomValidationException c => (400, "Application", [localizer[c.MessageKey, c.Arguments]]),
        EntityNotFoundException n => (404, "Application", [localizer[n.MessageKey, n.Arguments]]),
        ForbiddenException f => (403, "Application", [localizer[f.MessageKey, f.Arguments]]),
        ConflictException c => (409, "Application", [localizer[c.MessageKey, c.Arguments]]),
        ServiceUnavailableException s => (503, "Application", [localizer[s.MessageKey, s.Arguments]]),
        _ when Majlis.Framework.EntityFrameworkCore.DbConflicts.IsConflict(ex) => (409, "Application", [localizer["General:Errors:Conflict"]]),
        BadHttpRequestException or JsonException => (400, "Parsing", [localizer["General:Errors:InvalidRequest"]]),
        _ => ((int)HttpStatusCode.InternalServerError, "Application", [localizer["General:Errors:Unexpected"]]),
    };

    /// <summary>Validator messages are localization keys; FluentValidation placeholders are filled from the failure.</summary>
    private string Translate(FluentValidation.Results.ValidationFailure failure)
    {
        var text = localizer[failure.ErrorMessage];
        text = text.Replace("{PropertyName}", failure.PropertyName, StringComparison.Ordinal);
        if (failure.FormattedMessagePlaceholderValues is { } values)
        {
            foreach (var (key, value) in values)
            {
                text = text.Replace("{" + key + "}", Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal);
            }
        }

        return text;
    }
}

internal static partial class Log
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}")]
    public static partial void Unhandled(ILogger logger, Exception exception, string method, PathString path);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Request rejected with {Status} ({Source}) on {Method} {Path}")]
    public static partial void Rejected(ILogger logger, int status, string source, string method, PathString path);
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder app) => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
