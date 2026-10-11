import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RoutesService, eLayoutType } from '@majlis/core';
import { KNOWLEDGE_PERMISSIONS } from './permissions';

export function provideKnowledgeConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() =>
      inject(RoutesService).add([
        {
          path: '/documents',
          name: 'Menu.Documents',
          icon: '▤',
          order: 2,
          layout: eLayoutType.application,
          requiredPolicy: KNOWLEDGE_PERMISSIONS.viewDocument,
        },
      ]),
    ),
  ]);
}
