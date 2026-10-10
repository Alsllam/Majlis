import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import localeAr from '@angular/common/locales/ar';
import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { OAuthModuleConfig, OAuthStorage, provideOAuthClient } from 'angular-oauth2-oidc';
import { AuthService } from './auth/auth.service';
import { APP_SETTINGS } from './config/app-settings';
import { ConfigService } from './config/config.service';
import { LocalizationService } from './localization/localization.service';
import { ThemeService } from './theme/theme.service';

/**
 * Everything the shell needs before the first route: runtime settings, the OAuth client (code + PKCE, token attached
 * to every call under the BFF url), translations and the theme. Order matters: settings → auth → language.
 */
export function provideMajlisCore(): EnvironmentProviders {
  registerLocaleData(localeAr, 'ar');
  return makeEnvironmentProviders([
    provideHttpClient(withInterceptorsFromDi()),
    provideOAuthClient({ resourceServer: { sendAccessToken: true, allowedUrls: [] } }),
    { provide: OAuthStorage, useFactory: () => sessionStorage },
    provideTranslateService({
      fallbackLang: 'ar',
      loader: provideTranslateHttpLoader({ prefix: '/i18n/', suffix: '.json' }),
    }),
    { provide: APP_SETTINGS, useFactory: () => inject(ConfigService).settings },
    provideAppInitializer(async () => {
      const config = inject(ConfigService);
      const auth = inject(AuthService);
      const localization = inject(LocalizationService);
      const theme = inject(ThemeService);
      const oauthModule = inject(OAuthModuleConfig);
      const settings = await config.load();
      // The bearer token goes only to the BFF (and so to every backend host behind it).
      oauthModule.resourceServer.allowedUrls = [settings.application.baseUrl];
      theme.init();
      await auth.init();
      await localization.init(auth.user()?.locale ?? null, settings.application.defaultLanguage);
    }),
  ]);
}
