import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@majlis/core';
import { MEETINGS_PERMISSIONS } from './permissions';

export function provideMeetingsConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() =>
      inject(RoutesService).add([
        {
          path: '/meetings',
          name: 'Menu.Meetings',
          icon: '🗓',
          order: 5,
          layout: eLayoutType.application,
          requiredPolicy: MEETINGS_PERMISSIONS.viewMeeting,
        },
      ]),
    ),
  ]);
}
