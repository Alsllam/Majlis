using Azure;
using Azure.Communication.Email;
using Majlis.Framework.Application.Email;
using AcsEmailMessage = Azure.Communication.Email.EmailMessage;
using EmailMessage = Majlis.Framework.Application.Email.EmailMessage;
using Microsoft.Extensions.Options;

namespace Majlis.Framework.Application.Adapters.Email;

/// <summary>Cloud adapter: Azure Communication Services Email. The only place the ACS SDK is used.</summary>
public sealed class AzureCommunicationEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly string _sender;

    public AzureCommunicationEmailSender(IOptions<EmailOptions> options)
    {
        var o = options.Value;
        _client = new EmailClient(o.ConnectionString ?? throw new InvalidOperationException("Email:ConnectionString is required."));
        _sender = o.Sender;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var content = new EmailContent(message.Subject) { PlainText = message.TextBody, Html = message.HtmlBody };
        var recipients = new EmailRecipients([new EmailAddress(message.To, message.ToName)]);
        // Queued for delivery; the operation is not awaited to completion (ACS retries delivery on its side).
        await _client.SendAsync(WaitUntil.Started, new AcsEmailMessage(_sender, recipients, content), cancellationToken);
    }
}
