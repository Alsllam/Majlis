import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { ROOMS_PERMISSIONS } from '@majlis/rooms-config';

export const RoomsUICommonRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./rooms/rooms-list.component').then((m) => m.RoomsListComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: ROOMS_PERMISSIONS.viewRoom },
  },
  {
    path: ':roomId',
    loadComponent: () => import('./room/room.component').then((m) => m.RoomComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: ROOMS_PERMISSIONS.viewRoom },
  },
];
