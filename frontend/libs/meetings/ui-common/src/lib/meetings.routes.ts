import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { MEETINGS_PERMISSIONS } from '@majlis/meetings-config';

export const MeetingsUICommonRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./list/meetings-list.component').then((m) => m.MeetingsListComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: MEETINGS_PERMISSIONS.viewMeeting },
  },
  {
    path: ':id',
    loadComponent: () => import('./meeting/meeting-page.component').then((m) => m.MeetingPageComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: MEETINGS_PERMISSIONS.viewMeeting },
  },
];
