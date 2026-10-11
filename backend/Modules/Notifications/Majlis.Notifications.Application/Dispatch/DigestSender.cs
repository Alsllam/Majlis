using Majlis.Framework.Application.Email;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine.Attributes;

namespace Majlis.Notifications.Application.Dispatch;

/// <summary>
/// FR-NTF-002: one email per user with unread notifications newer than their last digest (or the last 24 hours),
/// to users who want email. Users who never opened their settings get a row with the defaults so the digest time is kept.
/// </summary>
public sealed partial class DigestSender(
    IReadOnlyRepository<Notification, Guid> notifications,
    IRepository<NotificationSettings, Guid> settings,
    IUnitOfWork unitOfWork,
    IEmailSender email,
    IRecipientLookup recipients,
    IOptions<NotificationsOptions> options,
    ILogger<DigestSender> logger)
{
    /// <summary>Returns the number of digests sent.</summary>
    public async Task<int> SendAsync(DateTime now, CancellationToken cancellationToken)
    {
        var window = now.AddHours(-24);
        var notBefore = now.AddHours(-options.Value.DigestMinIntervalHours);
        var unread = await notifications.Query().IgnoreQueryFilters()
            .Where(n => n.ReadAt == null && n.CreatedAt > window)
            .GroupBy(n => new { n.TenantId, n.UserId })
            .Select(g => new { g.Key.TenantId, g.Key.UserId, Count = g.Count(), Newest = g.Max(n => n.CreatedAt) })
            .ToListAsync(cancellationToken);
        if (unread.Count == 0)
        {
            return 0;
        }

        var userIds = unread.Select(u => u.UserId).ToList();
        var prefs = await settings.QueryTracked().IgnoreQueryFilters().Where(s => userIds.Contains(s.UserId)).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var tenant in unread.GroupBy(u => u.TenantId))
        {
            var due = tenant.Where(u =>
            {
                var pref = prefs.FirstOrDefault(p => p.UserId == u.UserId);
                return pref is null || (pref.EmailEnabled && (pref.LastDigestAt is null || pref.LastDigestAt <= notBefore) && u.Newest > (pref.LastDigestAt ?? window));
            }).ToList();
            if (due.Count == 0)
            {
                continue;
            }

            List<RecipientDto> users;
            try
            {
                users = await recipients.LookupAsync(tenant.Key, due.Select(d => d.UserId).ToList(), cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                Log.LookupFailed(logger, tenant.Key, ex);
                continue;
            }

            var link = options.Value.WebBaseUrl.TrimEnd('/') + "/notifications";
            foreach (var item in due)
            {
                var user = users.FirstOrDefault(u => u.Id == item.UserId);
                if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
                {
                    continue;
                }

                var pref = prefs.FirstOrDefault(p => p.UserId == item.UserId);
                var count = pref?.LastDigestAt is { } last
                    ? await notifications.Query().IgnoreQueryFilters().CountAsync(n => n.UserId == item.UserId && n.ReadAt == null && n.CreatedAt > last, cancellationToken)
                    : item.Count;
                if (count == 0)
                {
                    continue;
                }

                var (subject, text) = EmailTemplates.Digest(count, user.PreferredLanguage);
                try
                {
                    await email.SendAsync(new EmailMessage(user.Email, user.DisplayName, subject, EmailTemplates.Html(text, link, user.PreferredLanguage), $"{text}\n{link}"), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.EmailFailed(logger, user.Id, ex);
                    continue;
                }

                if (pref is null)
                {
                    pref = new NotificationSettings(tenant.Key, item.UserId);
                    await settings.InsertAsync(pref, autoSave: false, cancellationToken);
                    prefs.Add(pref);
                }

                pref.MarkDigestSent(now);
                sent++;
            }
        }

        if (sent > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            Log.Sent(logger, sent);
        }

        return sent;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 7201, Level = LogLevel.Information, Message = "Sent {Count} daily digests")]
        public static partial void Sent(ILogger logger, int count);

        [LoggerMessage(EventId = 7202, Level = LogLevel.Warning, Message = "Recipient lookup failed for tenant {TenantId}; digests skipped")]
        public static partial void LookupFailed(ILogger logger, Guid tenantId, Exception exception);

        [LoggerMessage(EventId = 7203, Level = LogLevel.Warning, Message = "Digest email to user {UserId} failed")]
        public static partial void EmailFailed(ILogger logger, Guid userId, Exception exception);
    }
}

[WolverineHandler]
public static class DailyDigestRequestedHandler
{
    public static Task Handle(DailyDigestRequested m, DigestSender sender, CancellationToken cancellationToken) => sender.SendAsync(m.At, cancellationToken);
}
