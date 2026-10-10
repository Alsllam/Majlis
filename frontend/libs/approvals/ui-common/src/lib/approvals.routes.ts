import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { APPROVALS_PERMISSIONS } from '@majlis/approvals-config';

export const ApprovalsUICommonRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./inbox/approvals-inbox.component').then((m) => m.ApprovalsInboxComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: APPROVALS_PERMISSIONS.viewApproval },
  },
];
