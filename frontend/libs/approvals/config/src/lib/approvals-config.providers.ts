import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@majlis/core';
import { APPROVALS_PERMISSIONS } from './permissions';

export function provideApprovalsConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() =>
      inject(RoutesService).add([
        {
          path: '/approvals',
          name: 'Menu.Approvals',
          icon: '✓',
          order: 3,
          layout: eLayoutType.application,
          requiredPolicy: APPROVALS_PERMISSIONS.viewApproval,
        },
      ]),
    ),
  ]);
}
