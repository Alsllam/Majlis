import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@majlis/core';
import { TASKS_PERMISSIONS } from './permissions';

export function provideTasksConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() =>
      inject(RoutesService).add([
        {
          path: '/tasks',
          name: 'Menu.Tasks',
          icon: '☐',
          order: 4,
          layout: eLayoutType.application,
          requiredPolicy: TASKS_PERMISSIONS.viewTask,
        },
      ]),
    ),
  ]);
}
