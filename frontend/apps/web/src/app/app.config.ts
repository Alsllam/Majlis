import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideApprovalsConfig } from '@majlis/approvals-config';
import { provideAuditConfig } from '@majlis/audit-config';
import { TOPBAR_WIDGETS, provideMajlisCore } from '@majlis/core';
import { provideNotificationsConfig } from '@majlis/notifications-config';
import { provideKnowledgeConfig } from '@majlis/knowledge-config';
import { provideRoomsConfig } from '@majlis/rooms-config';
import { provideMeetingsConfig } from '@majlis/meetings-config';
import { provideTasksConfig } from '@majlis/tasks-config';
import { provideThemeShared } from '@majlis/theme-shared';
import { provideWorkspacesConfig } from '@majlis/workspaces-config';
import { appRoutes } from './app.routes';

/** `provide*` calls only (skill §2). */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(appRoutes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideMajlisCore(),
    provideThemeShared(),
    provideWorkspacesConfig(),
    provideRoomsConfig(),
    provideKnowledgeConfig(),
    provideApprovalsConfig(),
    provideTasksConfig(),
    provideMeetingsConfig(),
    provideNotificationsConfig(),
    provideAuditConfig(),
    // Top-bar widgets, loaded lazily so the shell stays independent of the features.
    { provide: TOPBAR_WIDGETS, useValue: [() => import('@majlis/notifications-ui-common').then((m) => m.NotificationBellComponent)] },
  ],
};
