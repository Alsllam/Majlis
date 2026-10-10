import { Injectable, signal } from '@angular/core';
import { AppSettings } from './app-settings';

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly loaded = signal<AppSettings | null>(null);

  /** Fetched with `fetch`, not `HttpClient`, so it runs before interceptors and the OAuth client exist. */
  async load(url = '/assets/app-settings.json'): Promise<AppSettings> {
    const response = await fetch(url, { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`app-settings.json could not be loaded (${response.status})`);
    }
    const settings = (await response.json()) as AppSettings;
    this.loaded.set(settings);
    return settings;
  }

  get settings(): AppSettings {
    const value = this.loaded();
    if (!value) {
      throw new Error('AppSettings are not loaded yet');
    }
    return value;
  }

  apiUrl(apiName: string): string {
    const api = this.settings.apis[apiName];
    if (!api) {
      throw new Error(`apis.${apiName} is missing from app-settings.json`);
    }
    return api.url.replace(/\/+$/, '');
  }
}
