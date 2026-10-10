import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@majlis/core';
import { ROOMS_PERMISSIONS } from './permissions';

export function provideRoomsConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() =>
      inject(RoutesService).add([
        {
          path: '/rooms',
          name: 'Menu.Rooms',
          icon: '◱',
          order: 1,
          layout: eLayoutType.application,
          requiredPolicy: ROOMS_PERMISSIONS.viewRoom,
        },
      ]),
    ),
  ]);
}
