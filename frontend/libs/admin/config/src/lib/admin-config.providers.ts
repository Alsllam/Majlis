import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@majlis/core';
import { ADMIN_PERMISSIONS } from './permissions';

export function provideAdminConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() =>
      inject(RoutesService).add([
        {
          path: '/admin/groups',
          name: 'Menu.Groups',
          icon: '⚇',
          order: 8,
          layout: eLayoutType.application,
          requiredPolicy: ADMIN_PERMISSIONS.manageGroups,
        },
      ]),
    ),
  ]);
}
