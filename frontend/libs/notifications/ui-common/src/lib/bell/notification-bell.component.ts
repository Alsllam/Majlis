import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { EnArPipe, LocalizationService } from '@majlis/core';
import { NotificationDto } from '@majlis/notifications-proxy';
import { NotificationCenterFacade } from '../notification-center.facade';

/** The top-bar bell: live unread badge and the last notifications; a click opens the link and marks it read. */
@Component({
  selector: 'majlis-notification-bell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbDropdownModule, TranslatePipe, DatePipe, EnArPipe, RouterLink],
  template: `
    @if (center.enabled()) {
      <div ngbDropdown class="d-inline-block" placement="bottom-end">
        <button type="button" class="chip bell" ngbDropdownToggle [attr.aria-label]="'Notifications.Title' | translate" data-testid="notification-bell">
          <span aria-hidden="true">🔔</span>
          @if (center.unreadCount() > 0) {
            <span class="badge" data-testid="unread-count">{{ center.unreadCount() | enar }}</span>
          }
        </button>
        <div ngbDropdownMenu class="menu">
          <div class="head">
            <span class="title">{{ 'Notifications.Title' | translate }}</span>
            @if (center.unreadCount() > 0) {
              <button type="button" class="btn btn-link btn-sm p-0" (click)="center.markAllRead()">{{ 'Notifications.MarkAllRead' | translate }}</button>
            }
          </div>
          @for (n of center.recent(); track n.id) {
            <button type="button" ngbDropdownItem class="row-item" [class.unread]="!n.readAt" (click)="open(n)">
              <span class="text">{{ center.text(n) }}</span>
              <time class="num">{{ n.createdAt | date: 'short' : undefined : lang() }}</time>
            </button>
          } @empty {
            <p class="empty">{{ 'Notifications.Empty' | translate }}</p>
          }
          <a ngbDropdownItem routerLink="/notifications" class="all">{{ 'Notifications.SeeAll' | translate }}</a>
        </div>
      </div>
    }
  `,
  styles: `
    .chip { border: 1px solid var(--border-subtle); background: var(--surface); color: var(--text-primary); border-radius: var(--radius-full); padding: var(--space-1) var(--space-3); cursor: pointer; font-size: var(--fs-sm); display: inline-flex; align-items: center; gap: var(--space-2); position: relative;
      &:hover { background: var(--surface-hover); } &:focus-visible { outline: 2px solid var(--focus-ring); outline-offset: 2px; } }
    .bell::after { display: none; }
    .badge { background: var(--danger-solid); color: #fff; border-radius: var(--radius-full); font-size: var(--fs-xs); min-width: 18px; height: 18px; display: inline-grid; place-items: center; padding-inline: 4px; }
    .menu { width: min(380px, calc(100vw - 32px)); padding: 0; }
    .head { display: flex; justify-content: space-between; align-items: center; padding: var(--space-2) var(--space-3); border-bottom: 1px solid var(--border-subtle); }
    .title { font-weight: var(--fw-medium); }
    .row-item { display: grid; gap: 2px; white-space: normal; padding: var(--space-2) var(--space-3); border-bottom: 1px solid var(--border-subtle); }
    .row-item.unread { background: var(--brand-subtle); }
    .text { font-size: var(--fs-sm); color: var(--text-primary); }
    time { font-size: var(--fs-xs); color: var(--text-muted); }
    .empty { margin: 0; padding: var(--space-4); text-align: center; color: var(--text-muted); font-size: var(--fs-sm); }
    .all { text-align: center; font-size: var(--fs-sm); padding: var(--space-2); }
  `,
})
export class NotificationBellComponent {
  protected readonly center = inject(NotificationCenterFacade);
  protected readonly lang = inject(LocalizationService).lang;
  private readonly router = inject(Router);

  constructor() {
    void this.center.start();
  }

  protected async open(notification: NotificationDto): Promise<void> {
    await this.center.markRead(notification);
    if (notification.link) {
      await this.router.navigateByUrl(notification.link);
    }
  }
}
