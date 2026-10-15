import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { ADMIN_PERMISSIONS } from '@majlis/admin-config';

export const AdminUICommonRoutes: Route[] = [
  {
    path: 'groups',
    loadComponent: () => import('./groups/groups.component').then((m) => m.GroupsComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: ADMIN_PERMISSIONS.manageGroups },
  },
];
