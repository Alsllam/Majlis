using System.Text.Json;
using Majlis.Framework.Application.Email;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majlis.Notifications.Application.Dispatch;

public sealed record Recipient(Guid UserId);

/// <summary>
/// Writes one notification per recipient (skipping muted types and duplicates), publishes <c>NotificationCreated</c>
/// through the outbox for the live push, and emails the recipients who want email. An email failure never fails the
/// handler: the in-app notification is the record, email is best effort.
/// </summary>
public sealed partial class NotificationDispatcher(
    IRepository<Notification, Guid> notifications,
    IReadOnlyRepository<NotificationSettings, Guid> settings,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    IEmailSender email,
    IIdentityInternalClient identity,
    IOptions<NotificationsOptions> options,
    TimeProvider clock,
    ILogger<NotificationDispatcher> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Returns the number of notifications written.</summary>
    public async Task<int> NotifyAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        string type,
        IReadOnlyDictionary<string, string> parameters,
        string? link,
        string sourceKey,
        CancellationToken cancellationToken)
    {
        var targets = userIds.Distinct().ToList();
        if (targets.Count == 0)
        {
            return 0;
        }

        var keys = targets.Select(u => DedupeKey(type, sourceKey, u)).ToList();
        var existing = await notifications.Query().Where(n => keys.Contains(n.DedupeKey)).Select(n => n.DedupeKey).ToListAsync(cancellationToken);
        var prefs = await settings.Query().Where(s => targets.Contains(s.UserId)).ToListAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var paramsJson = JsonSerializer.Serialize(parameters, Json);
        var written = new List<Notification>();
        foreach (var userId in targets)
        {
            var pref = prefs.FirstOrDefault(p => p.UserId == userId);
            if (pref?.IsMuted(type) == true || existing.Contains(DedupeKey(type, sourceKey, userId)))
            {
                continue;
            }

            var notification = new Notification(Guid.NewGuid(), tenantId, userId, type, paramsJson, link, DedupeKey(type, sourceKey, userId), now);
            await notifications.InsertAsync(notification, autoSave: false, cancellationToken);
            await publisher.PublishAsync(new NotificationCreated(tenantId, userId, notification.Id, type, paramsJson, link, now), cancellationToken);
            written.Add(notification);
        }

        if (written.Count == 0)
        {
            return 0;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await EmailAsync(tenantId, written.Where(n => prefs.FirstOrDefault(p => p.UserId == n.UserId)?.EmailEnabled != false).ToList(), type, parameters, link, cancellationToken);
        return written.Count;
    }

    private async Task EmailAsync(Guid tenantId, List<Notification> targets, string type, IReadOnlyDictionary<string, string> parameters, string? link, CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            return;
        }

        List<InternalUserDto> users;
        try
        {
            users = await identity.LookupByIdsAsync(new InternalUsersQueryDto(tenantId, targets.Select(t => t.UserId).ToList()), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or Refit.ApiException or TaskCanceledException)
        {
            Log.LookupFailed(logger, ex);
            return;
        }

        var url = options.Value.WebBaseUrl.TrimEnd('/') + (link ?? "/notifications");
        foreach (var notification in targets)
        {
            var user = users.FirstOrDefault(u => u.Id == notification.UserId);
            if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
            {
                continue;
            }

            var (subject, text) = EmailTemplates.For(type, user.PreferredLanguage, parameters);
            try
            {
                await email.SendAsync(new EmailMessage(user.Email, user.DisplayName, subject, EmailTemplates.Html(text, url, user.PreferredLanguage), $"{text}\n{url}"), cancellationToken);
                notification.MarkEmailSent(clock.GetUtcNow().UtcDateTime);
                await notifications.UpdateAsync(notification, autoSave: false, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.EmailFailed(logger, user.Id, ex);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string DedupeKey(string type, string sourceKey, Guid userId) => $"{type}:{sourceKey}:{userId:N}";

    private static partial class Log
    {
        [LoggerMessage(EventId = 7101, Level = LogLevel.Warning, Message = "Recipient lookup failed; emails skipped")]
        public static partial void LookupFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 7102, Level = LogLevel.Warning, Message = "Email to user {UserId} failed")]
        public static partial void EmailFailed(ILogger logger, Guid userId, Exception exception);
    }
}
