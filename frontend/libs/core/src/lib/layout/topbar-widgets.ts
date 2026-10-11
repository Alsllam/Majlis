import { InjectionToken, Type } from '@angular/core';

/** Loads a standalone component to render in the top bar (lazy, so the shell never imports a feature). */
export type TopbarWidgetLoader = () => Promise<Type<unknown>>;

/**
 * Feature components the layout shows in the top bar, in order (e.g. the notification bell). The app provides the
 * loaders in `app.config.ts`; `type:ui` libraries only read the token.
 */
export const TOPBAR_WIDGETS = new InjectionToken<TopbarWidgetLoader[]>('TOPBAR_WIDGETS');
