using System.Text.Json;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Majlis.Notifications.Application.Notifications.DTOs;
using Majlis.Notifications.Domain.Constants;
using Majlis.Notifications.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Notifications.Application.Notifications;

[Route("notifications")]
public class NotificationsAppService(
    IRepository<Notification, Guid> notifications,
    IRepository<NotificationSettings, Guid> settings,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock) : ApplicationService, INotificationsAppService
{
    /// <inheritdoc />
    [HttpPost("list")]
    [HasPermission(NotificationsPermissions.ViewNotification)]
    public async Task<PagedResultDto<NotificationDto>> GetListAsync(FilterNotificationDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var query = notifications.Query().Where(n => n.UserId == userId && (!input.UnreadOnly || n.ReadAt == null));
        var total = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(n => n.CreatedAt).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)).ToListAsync(cancellationToken);
        return new PagedResultDto<NotificationDto>(page.Select(ToDto).ToList(), total);
    }

    /// <inheritdoc />
    [HttpPost("unread-count")]
    [HasPermission(NotificationsPermissions.ViewNotification)]
    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        return new UnreadCountDto(await notifications.Query().CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken));
    }

    /// <inheritdoc />
    [HttpPost("mark-read")]
    [HasPermission(NotificationsPermissions.ViewNotification)]
    public async Task MarkReadAsync(MarkReadDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var ids = input.Ids.Distinct().Take(100).ToList();
        var rows = await notifications.QueryTracked().Where(n => n.UserId == userId && ids.Contains(n.Id) && n.ReadAt == null).ToListAsync(cancellationToken);
        await MarkAsync(rows, cancellationToken);
    }

    /// <inheritdoc />
    [HttpPost("mark-all-read")]
    [HasPermission(NotificationsPermissions.ViewNotification)]
    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var rows = await notifications.QueryTracked().Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync(cancellationToken);
        await MarkAsync(rows, cancellationToken);
    }

    /// <inheritdoc />
    [HttpPost("settings")]
    [HasPermission(NotificationsPermissions.ViewNotification)]
    public async Task<NotificationSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var row = await settings.Query().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        return row is null ? new NotificationSettingsDto(true, []) : new NotificationSettingsDto(row.EmailEnabled, row.MutedTypeList.ToList());
    }

    /// <inheritdoc />
    [HttpPut("settings")]
    [HasPermission(NotificationsPermissions.ViewNotification)]
    public async Task<NotificationSettingsDto> UpdateSettingsAsync(UpdateNotificationSettingsDto input, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredId();
        var row = await settings.QueryTracked().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (row is null)
        {
            row = new NotificationSettings(currentUser.GetRequiredTenantId(), userId);
            row.Update(input.EmailEnabled, input.MutedTypes);
            await settings.InsertAsync(row, autoSave: false, cancellationToken);
        }
        else
        {
            row.Update(input.EmailEnabled, input.MutedTypes);
            await settings.UpdateAsync(row, autoSave: false, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NotificationSettingsDto(row.EmailEnabled, row.MutedTypeList.ToList());
    }

    private async Task MarkAsync(List<Notification> rows, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var row in rows.Where(r => r.MarkRead(now)))
        {
            await notifications.UpdateAsync(row, autoSave: false, cancellationToken);
        }

        if (rows.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static NotificationDto ToDto(Notification n)
    {
        using var doc = JsonDocument.Parse(n.ParamsJson);
        return new NotificationDto(n.Id, n.Type, doc.RootElement.Clone(), n.Link, n.CreatedAt, n.ReadAt);
    }
}
