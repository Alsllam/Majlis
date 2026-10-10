import { Route } from '@angular/router';
import { authGuard } from '@majlis/core';
import { LayoutComponent } from '@majlis/theme-shared';

export const appRoutes: Route[] = [
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'workspaces' },
      { path: 'workspaces', loadChildren: () => import('@majlis/workspaces-ui-common').then((m) => m.WorkspacesUICommonRoutes) },
      { path: 'rooms', loadChildren: () => import('@majlis/rooms-ui-common').then((m) => m.RoomsUICommonRoutes) },
      { path: '403', loadComponent: () => import('./pages/forbidden.component').then((m) => m.ForbiddenComponent) },
      { path: '404', loadComponent: () => import('./pages/not-found.component').then((m) => m.NotFoundComponent) },
    ],
  },
  { path: '**', redirectTo: '/404' },
];
