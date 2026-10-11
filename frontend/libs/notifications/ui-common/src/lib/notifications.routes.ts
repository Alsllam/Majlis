import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { NOTIFICATIONS_PERMISSIONS } from '@majlis/notifications-config';

export const NotificationsUICommonRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./list/notifications-list.component').then((m) => m.NotificationsListComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: NOTIFICATIONS_PERMISSIONS.viewNotification },
  },
  {
    path: 'settings',
    loadComponent: () => import('./settings/notification-settings.component').then((m) => m.NotificationSettingsComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: NOTIFICATIONS_PERMISSIONS.viewNotification },
  },
];
