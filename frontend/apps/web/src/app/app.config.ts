import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideMajlisCore } from '@majlis/core';
import { provideKnowledgeConfig } from '@majlis/knowledge-config';
import { provideRoomsConfig } from '@majlis/rooms-config';
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
  ],
};
