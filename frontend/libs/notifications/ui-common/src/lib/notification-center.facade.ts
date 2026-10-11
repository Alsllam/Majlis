import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LocalizationService, PermissionService } from '@majlis/core';
import { NOTIFICATIONS_PERMISSIONS } from '@majlis/notifications-config';
import { NotificationDto, NotificationsService } from '@majlis/notifications-proxy';
import { RealtimeConnectionService } from '@majlis/shared-realtime';
import { ToastService } from '@majlis/theme-shared';

const RECENT = 10;

/**
 * The notification center (FR-NTF-001): unread count and the latest notifications, kept live by the hub message
 * `notification` (one per new row, pushed to every connection of the user) and refreshed after every reconnect.
 * One instance for the whole app: the bell, the list page and toasts read from it.
 */
@Injectable({ providedIn: 'root' })
export class NotificationCenterFacade {
  private readonly api = inject(NotificationsService);
  private readonly realtime = inject(RealtimeConnectionService);
  private readonly permissions = inject(PermissionService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly recentItems = signal<NotificationDto[]>([]);
  private readonly unread = signal(0);
  private started = false;
  private detach: (() => void) | null = null;

  readonly recent = this.recentItems.asReadonly();
  readonly unreadCount = this.unread.asReadonly();
  readonly enabled = computed(() => this.permissions.isGranted(NOTIFICATIONS_PERMISSIONS.viewNotification));

  constructor() {
    this.destroyRef.onDestroy(() => this.detach?.());
  }

  /** Loads the state once and attaches to the hub; safe to call from several components. */
  async start(): Promise<void> {
    if (this.started || !this.enabled()) {
      return;
    }
    this.started = true;
    await this.refresh();
    this.detach = this.realtime.onReconnected(() => {
      void this.attach();
      void this.refresh();
    });
    await this.attach();
  }

  async refresh(): Promise<void> {
    try {
      const [count, page] = await Promise.all([
        firstValueFrom(this.api.getUnreadCount()),
        firstValueFrom(this.api.getList({ skipCount: 0, maxResultCount: RECENT })),
      ]);
      this.unread.set(count.count);
      this.recentItems.set(page.items);
    } catch {
      // Reported by the global handler; the bell keeps its last state.
    }
  }

  async markRead(notification: NotificationDto): Promise<void> {
    if (notification.readAt) {
      return;
    }
    const at = new Date().toISOString();
    this.recentItems.update((items) => items.map((n) => (n.id === notification.id ? { ...n, readAt: at } : n)));
    this.unread.update((n) => Math.max(0, n - 1));
    await firstValueFrom(this.api.markRead([notification.id]));
  }

  async markAllRead(): Promise<void> {
    const at = new Date().toISOString();
    this.recentItems.update((items) => items.map((n) => (n.readAt ? n : { ...n, readAt: at })));
    this.unread.set(0);
    await firstValueFrom(this.api.markAllRead());
  }

  /** Text of one notification in the current language: `Notifications.Types.<type>` with its params. */
  text(notification: NotificationDto): string {
    return this.localization.instant(`Notifications.Types.${notification.type}`, notification.params);
  }

  private async attach(): Promise<void> {
    try {
      const connection = await this.realtime.connect();
      connection.off('notification');
      connection.on('notification', (incoming: NotificationDto) => this.receive(incoming));
    } catch {
      // Offline: the connection service keeps retrying and `onReconnected` attaches again.
    }
  }

  private receive(incoming: NotificationDto): void {
    if (this.recentItems().some((n) => n.id === incoming.id)) {
      return;
    }
    this.recentItems.update((items) => [incoming, ...items].slice(0, RECENT));
    this.unread.update((n) => n + 1);
    this.toasts.show(this.text(incoming), 'info');
  }
}
