import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

export type Language = 'ar' | 'en';
export const LANGUAGES: readonly Language[] = ['ar', 'en'];
const STORAGE_KEY = 'majlis.lang';

/** Current language and direction as signals; sets `lang`/`dir` on `<html>` and swaps the Bootstrap stylesheet. */
@Injectable({ providedIn: 'root' })
export class LocalizationService {
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<Language>('ar');

  readonly lang = this.current.asReadonly();
  readonly dir = computed(() => (this.current() === 'ar' ? 'rtl' : 'ltr'));
  readonly isRtl = computed(() => this.dir() === 'rtl');

  /** Chooses the stored language, else the user's preferred one, else the product default (`ar`). */
  async init(preferred: Language | null, fallback: Language): Promise<void> {
    const stored = this.read();
    await this.use(stored ?? preferred ?? fallback);
  }

  async use(lang: Language): Promise<void> {
    await firstValueFrom(this.translate.use(lang));
    this.current.set(lang);
    const html = this.document.documentElement;
    html.lang = lang;
    html.dir = this.dir();
    this.swapBootstrap(lang);
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Private mode or blocked storage: the choice just does not persist.
    }
  }

  instant(key: string, params?: Record<string, unknown>): string {
    return this.translate.instant(key, params);
  }

  /** Bootstrap ships separate LTR and RTL builds (copied to `/vendor` by the app build); swap by direction. */
  private swapBootstrap(lang: Language): void {
    let link = this.document.getElementById('bootstrap-css') as HTMLLinkElement | null;
    if (!link) {
      link = this.document.createElement('link');
      link.id = 'bootstrap-css';
      link.rel = 'stylesheet';
      this.document.head.prepend(link);
    }
    link.href = lang === 'ar' ? '/vendor/bootstrap.rtl.min.css' : '/vendor/bootstrap.min.css';
  }

  private read(): Language | null {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      return value === 'ar' || value === 'en' ? value : null;
    } catch {
      return null;
    }
  }
}
