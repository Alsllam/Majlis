import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { TASKS_PERMISSIONS } from '@majlis/tasks-config';

export const TasksUICommonRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./tasks/tasks-list.component').then((m) => m.TasksListComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: TASKS_PERMISSIONS.viewTask },
  },
];
