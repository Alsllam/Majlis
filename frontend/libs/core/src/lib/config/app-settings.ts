import { InjectionToken } from '@angular/core';

/** Runtime configuration, loaded from `/assets/app-settings.json` before the app starts (skill §3). */
export interface AppSettings {
  application: { name: string; baseUrl: string; defaultLanguage: 'ar' | 'en' };
  oAuthConfig: {
    issuer: string;
    clientId: string;
    responseType: 'code';
    scope: string;
    requireHttps: boolean;
    skipIssuerCheck?: boolean;
    strictDiscoveryDocumentValidation?: boolean;
  };
  /** One entry per backend host behind the BFF; proxy libraries reference them by key. */
  apis: Record<string, { url: string }>;
  realtime: { hubUrl: string; ticketUrl: string };
  /** Mirror of each host's `RolePermissions`; `*` grants everything. The backend is still the authority. */
  rolePermissions: Record<string, string[]>;
}

export const APP_SETTINGS = new InjectionToken<AppSettings>('APP_SETTINGS');
