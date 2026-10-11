import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@majlis/core';
import { WORKSPACES_PERMISSIONS } from './permissions';

export function provideWorkspacesConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() =>
      inject(RoutesService).add([
        {
          path: '/workspaces',
          name: 'Menu.Workspaces',
          icon: '▦',
          order: 0,
          layout: eLayoutType.application,
          requiredPolicy: WORKSPACES_PERMISSIONS.viewWorkspace,
        },
      ]),
    ),
  ]);
}
