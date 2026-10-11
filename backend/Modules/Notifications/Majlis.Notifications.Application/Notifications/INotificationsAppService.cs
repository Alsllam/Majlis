using Majlis.Framework.Application.Dtos;
using Majlis.Notifications.Application.Notifications.DTOs;

namespace Majlis.Notifications.Application.Notifications;

/// <summary>The caller's notification center and preferences (SRS §4.8). Only the caller's own rows are ever visible.</summary>
public interface INotificationsAppService
{
    Task<PagedResultDto<NotificationDto>> GetListAsync(FilterNotificationDto input, CancellationToken cancellationToken = default);

    Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task MarkReadAsync(MarkReadDto input, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(CancellationToken cancellationToken = default);

    Task<NotificationSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<NotificationSettingsDto> UpdateSettingsAsync(UpdateNotificationSettingsDto input, CancellationToken cancellationToken = default);
}
