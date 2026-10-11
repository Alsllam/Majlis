import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { WORKSPACES_PERMISSIONS } from '@majlis/workspaces-config';

/** Mounted at `/home` by the app: the landing page after sign-in. */
export const WorkspaceHomeRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./home/workspace-home.component').then((m) => m.WorkspaceHomeComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: WORKSPACES_PERMISSIONS.viewWorkspace },
  },
];
