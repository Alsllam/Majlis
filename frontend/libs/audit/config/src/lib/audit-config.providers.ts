import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@majlis/core';
import { AUDIT_PERMISSIONS } from './permissions';

export function provideAuditConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() =>
      inject(RoutesService).add([
        {
          path: '/audit',
          name: 'Menu.Audit',
          icon: '≡',
          order: 9,
          layout: eLayoutType.application,
          requiredPolicy: AUDIT_PERMISSIONS.viewAudit,
        },
      ]),
    ),
  ]);
}
