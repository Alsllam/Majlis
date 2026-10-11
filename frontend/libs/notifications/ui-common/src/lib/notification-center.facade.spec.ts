import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { LocalizationService, PermissionService } from '@majlis/core';
import { NotificationsService } from '@majlis/notifications-proxy';
import { RealtimeConnectionService } from '@majlis/shared-realtime';
import { ToastService } from '@majlis/theme-shared';
import { NotificationCenterFacade } from './notification-center.facade';

describe('NotificationCenterFacade', () => {
  it('loads the unread count, receives hub pushes once, and marks read optimistically', async () => {
    const handlers: Record<string, (n: unknown) => void> = {};
    const connection = { on: (name: string, h: (n: unknown) => void) => (handlers[name] = h), off: jest.fn() };
    const api = {
      getUnreadCount: jest.fn(() => of({ count: 1 })),
      getList: jest.fn(() => of({ items: [{ id: 'a', type: 'task.assigned', params: { name: 'سارة', title: 'x' }, link: '/tasks', createdAt: '2026-10-11T08:00:00Z', readAt: null }], totalCount: 1 })),
      markRead: jest.fn(() => of(undefined)),
      markAllRead: jest.fn(() => of(undefined)),
    };
    const toasts = { show: jest.fn() };
    TestBed.configureTestingModule({
      providers: [
        { provide: NotificationsService, useValue: api },
        { provide: RealtimeConnectionService, useValue: { connect: () => Promise.resolve(connection), onReconnected: () => () => undefined } },
        { provide: PermissionService, useValue: { isGranted: () => true } },
        { provide: ToastService, useValue: toasts },
        { provide: LocalizationService, useValue: { instant: (k: string, p: Record<string, string>) => `${k}:${p['name']}` } },
      ],
    });
    const facade = TestBed.inject(NotificationCenterFacade);
    await facade.start();
    await facade.start(); // idempotent

    expect(facade.unreadCount()).toBe(1);
    expect(api.getList).toHaveBeenCalledTimes(1);

    const incoming = { id: 'b', type: 'approval.requested', params: { name: 'خالد', summary: 's' }, link: '/rooms/r', createdAt: '2026-10-11T09:00:00Z', readAt: null };
    handlers['notification'](incoming);
    handlers['notification'](incoming); // duplicate delivery
    expect(facade.unreadCount()).toBe(2);
    expect(facade.recent().map((n) => n.id)).toEqual(['b', 'a']);
    expect(toasts.show).toHaveBeenCalledWith('Notifications.Types.approval.requested:خالد', 'info');

    await facade.markRead(incoming);
    expect(facade.unreadCount()).toBe(1);
    expect(api.markRead).toHaveBeenCalledWith(['b']);
    await facade.markAllRead();
    expect(facade.unreadCount()).toBe(0);
    expect(facade.recent().every((n) => n.readAt)).toBe(true);
  });
});
