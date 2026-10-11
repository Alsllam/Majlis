import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { WORKSPACES_PERMISSIONS } from '@majlis/workspaces-config';

export const WorkspacesUICommonRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./workspaces/workspaces-list.component').then((m) => m.WorkspacesListComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: WORKSPACES_PERMISSIONS.viewWorkspace },
  },
  {
    path: ':workspaceId',
    loadComponent: () => import('./workspace/workspace.component').then((m) => m.WorkspaceComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: WORKSPACES_PERMISSIONS.viewWorkspace },
  },
];
