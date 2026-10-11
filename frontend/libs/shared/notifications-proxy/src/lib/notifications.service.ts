import { Injectable, inject } from '@angular/core';
import { PagedResultDto, RestConfig, RestService } from '@majlis/core';
import { FilterNotificationDto, NotificationDto, NotificationSettingsDto, UnreadCountDto } from './notification.model';

/** Mirrors `NotificationsAppService` (`[Route("notifications")]`, reached as `/api/notifications/notifications/...`). */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly rest = inject(RestService);
  readonly apiName = 'notifications';

  private call<TBody, TResult>(method: 'POST' | 'PUT', url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method, url: `/notifications${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterNotificationDto, config?: Partial<RestConfig>) =>
    this.call<FilterNotificationDto, PagedResultDto<NotificationDto>>('POST', '/list', input, config);
  getUnreadCount = (config?: Partial<RestConfig>) => this.call<Record<string, never>, UnreadCountDto>('POST', '/unread-count', {}, config);
  markRead = (ids: string[], config?: Partial<RestConfig>) => this.call<{ ids: string[] }, void>('POST', '/mark-read', { ids }, config);
  markAllRead = (config?: Partial<RestConfig>) => this.call<Record<string, never>, void>('POST', '/mark-all-read', {}, config);
  getSettings = (config?: Partial<RestConfig>) => this.call<Record<string, never>, NotificationSettingsDto>('POST', '/settings', {}, config);
  updateSettings = (input: NotificationSettingsDto, config?: Partial<RestConfig>) =>
    this.call<NotificationSettingsDto, NotificationSettingsDto>('PUT', '/settings', input, config);
}
