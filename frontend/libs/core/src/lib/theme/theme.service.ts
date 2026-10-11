import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark' | 'dim';
export const THEMES: readonly Theme[] = ['light', 'dark', 'dim'];
const STORAGE_KEY = 'majlis.theme';

/** Light, dark and dim come from the same tokens (`docs/brand/tokens`); the choice is `data-theme` on `<html>`. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<Theme>('light');

  readonly theme = this.current.asReadonly();

  init(): void {
    let stored: string | null = null;
    try {
      stored = localStorage.getItem(STORAGE_KEY);
    } catch {
      stored = null;
    }
    const preferred = this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    this.use(THEMES.includes(stored as Theme) ? (stored as Theme) : preferred);
  }

  use(theme: Theme): void {
    this.current.set(theme);
    const html = this.document.documentElement;
    html.dataset['theme'] = theme;
    html.dataset['bsTheme'] = theme === 'light' ? 'light' : 'dark';
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // Private mode or blocked storage: the choice just does not persist.
    }
  }
}
