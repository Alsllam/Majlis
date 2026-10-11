import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { AUDIT_PERMISSIONS } from '@majlis/audit-config';

export const AuditUICommonRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./log/audit-log.component').then((m) => m.AuditLogComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: AUDIT_PERMISSIONS.viewAudit },
  },
];
