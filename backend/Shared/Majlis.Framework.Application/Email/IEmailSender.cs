using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majlis.Framework.Application.Email;

/// <summary>
/// Outbound email behind an adapter (ADR-0006): Azure Communication Services on the cloud profile, SMTP on-prem later.
/// Messages carry no document or conversation content by default (SRS §4.8): a short notice and a link.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(string To, string? ToName, string Subject, string HtmlBody, string TextBody);

public sealed class EmailOptions
{
    /// <summary>Azure Communication Services connection string; when empty, emails are only logged (development).</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Verified sender address of the Email Communication Service domain.</summary>
    public string Sender { get; set; } = "DoNotReply@majlis.local";
}

/// <summary>Development fallback: writes the message to the log instead of sending it.</summary>
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Log.Email(logger, message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Email (not sent, no provider configured) to {To}: {Subject} — {Body}")]
        public static partial void Email(ILogger logger, string to, string subject, string body);
    }
}

public static class EmailServiceCollectionExtensions
{
    /// <summary>Azure Communication Services when <c>Email:ConnectionString</c> is set, otherwise the logging sender.</summary>
    public static IServiceCollection AddMajlisEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        var options = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender>(sp => new Adapters.Email.AzureCommunicationEmailSender(sp.GetRequiredService<IOptions<EmailOptions>>()));
        }

        return services;
    }
}
