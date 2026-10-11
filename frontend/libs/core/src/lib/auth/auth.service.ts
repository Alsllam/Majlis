import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthConfig, OAuthService } from 'angular-oauth2-oidc';
import { ConfigService } from '../config/config.service';

/** Claims the Auth host puts in the access token (`DestinationsFor` in AuthorizationController). */
export interface CurrentUser {
  id: string;
  name: string;
  tenantId: string;
  roles: string[];
  locale: 'ar' | 'en';
}

interface AccessTokenClaims {
  sub?: string;
  name?: string;
  tenant_id?: string;
  role?: string | string[];
  locale?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly oauth = inject(OAuthService);
  private readonly config = inject(ConfigService);
  private readonly token = signal<string | null>(null);

  readonly isAuthenticated = computed(() => this.token() !== null);
  readonly user = computed<CurrentUser | null>(() => {
    const token = this.token();
    return token ? AuthService.parse(token) : null;
  });

  /** Authorization code + PKCE against the OpenIddict host, reached through the BFF (skill §3). */
  async init(): Promise<void> {
    const o = this.config.settings.oAuthConfig;
    const authConfig: AuthConfig = {
      issuer: o.issuer,
      clientId: o.clientId,
      responseType: o.responseType,
      scope: o.scope,
      requireHttps: o.requireHttps,
      skipIssuerCheck: o.skipIssuerCheck ?? false,
      strictDiscoveryDocumentValidation: o.strictDiscoveryDocumentValidation ?? true,
      redirectUri: window.location.origin,
      postLogoutRedirectUri: window.location.origin,
      useSilentRefresh: false,
      clearHashAfterLogin: true,
    };
    this.oauth.configure(authConfig);
    this.oauth.events.subscribe(() => this.token.set(this.readToken()));
    await this.oauth.loadDiscoveryDocumentAndTryLogin();
    this.oauth.setupAutomaticSilentRefresh();
    this.token.set(this.readToken());
  }

  login(returnUrl?: string): void {
    this.oauth.initCodeFlow(returnUrl ?? window.location.pathname + window.location.search);
  }

  /** The url saved by {@link login}, consumed once after the redirect back. */
  consumeReturnUrl(): string | null {
    const state = this.oauth.state;
    if (!state) {
      return null;
    }
    const decoded = decodeURIComponent(state);
    return decoded.startsWith('/') ? decoded : null;
  }

  logout(): void {
    this.oauth.logOut();
  }

  get accessToken(): string | null {
    return this.token();
  }

  private readToken(): string | null {
    return this.oauth.hasValidAccessToken() ? this.oauth.getAccessToken() : null;
  }

  static parse(token: string): CurrentUser | null {
    try {
      const payload = token.split('.')[1] ?? '';
      const bytes = Uint8Array.from(atob(payload.replace(/-/g, '+').replace(/_/g, '/')), (c) => c.charCodeAt(0));
      const claims = JSON.parse(new TextDecoder().decode(bytes)) as AccessTokenClaims; // UTF-8: names are Arabic
      const roles = claims.role === undefined ? [] : Array.isArray(claims.role) ? claims.role : [claims.role];
      return {
        id: claims.sub ?? '',
        name: claims.name ?? '',
        tenantId: claims.tenant_id ?? '',
        roles,
        locale: claims.locale === 'en' ? 'en' : 'ar',
      };
    } catch {
      return null;
    }
  }
}
