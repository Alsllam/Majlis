import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { LocalizationService } from '@majlis/core';
import { NotificationDto, NotificationsService } from '@majlis/notifications-proxy';
import { EmptyStateComponent } from '@majlis/shared-ui-common';
import { PageHeaderComponent } from '@majlis/theme-shared';
import { NotificationCenterFacade } from '../notification-center.facade';

/** Every notification of the caller, newest first; opening one marks it read and follows its link. */
@Component({
  selector: 'majlis-notifications-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DatePipe, RouterLink, PageHeaderComponent, EmptyStateComponent],
  template: `
    <majlis-page-header title="Notifications.Title" subtitle="Notifications.Subtitle">
      <button type="button" class="btn btn-outline-secondary btn-sm" (click)="markAllRead()" [disabled]="center.unreadCount() === 0">{{ 'Notifications.MarkAllRead' | translate }}</button>
      <a routerLink="settings" class="btn btn-outline-secondary btn-sm">{{ 'Notifications.Settings' | translate }}</a>
    </majlis-page-header>

    @if (items() === null) {
      <p class="text-secondary">{{ 'General.Loading' | translate }}</p>
    } @else if (items()?.length === 0) {
      <majlis-empty-state title="Notifications.EmptyTitle" text="Notifications.EmptyText" />
    } @else {
      <div class="card-surface list">
        @for (n of items(); track n.id) {
          <button type="button" class="row-item" [class.unread]="!n.readAt" (click)="open(n)" [attr.data-notification-id]="n.id">
            <span class="type status-tag status-tag-neutral">{{ 'Notifications.Kinds.' + n.type | translate }}</span>
            <span class="text">{{ center.text(n) }}</span>
            <time class="num">{{ n.createdAt | date: 'medium' : undefined : lang() }}</time>
          </button>
        }
      </div>
    }
  `,
  styles: `
    .list { display: grid; }
    .row-item { display: grid; grid-template-columns: auto 1fr auto; gap: var(--space-3); align-items: center; text-align: start; width: 100%; background: none; border: 0; border-bottom: 1px solid var(--border-subtle); padding: var(--space-3); color: var(--text-primary); cursor: pointer;
      &:last-child { border-bottom: 0; } &:hover { background: var(--surface-hover); } &.unread { background: var(--brand-subtle); font-weight: var(--fw-medium); } }
    time { font-size: var(--fs-xs); color: var(--text-muted); white-space: nowrap; }
    @media (max-width: 768px) { .row-item { grid-template-columns: 1fr; } }
  `,
})
export class NotificationsListComponent {
  protected readonly center = inject(NotificationCenterFacade);
  protected readonly lang = inject(LocalizationService).lang;
  private readonly api = inject(NotificationsService);
  private readonly router = inject(Router);
  protected readonly items = signal<NotificationDto[] | null>(null);

  constructor() {
    void this.center.start();
    this.load();
  }

  protected load(): void {
    this.api.getList({ skipCount: 0, maxResultCount: 100 }).subscribe((page) => this.items.set(page.items));
  }

  protected async markAllRead(): Promise<void> {
    await this.center.markAllRead();
    this.load();
  }

  protected async open(notification: NotificationDto): Promise<void> {
    if (!notification.readAt) {
      await firstValueFrom(this.api.markRead([notification.id]));
      await this.center.refresh();
    }
    if (notification.link) {
      await this.router.navigateByUrl(notification.link);
    } else {
      this.load();
    }
  }
}
