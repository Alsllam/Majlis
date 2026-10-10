import { Page, expect } from '@playwright/test';

export const PASSWORD = process.env['MAJLIS_SEED_PASSWORD'] ?? '';
export const USERS = {
  sara: { email: 'sara@demo.majlis.local', name: 'سارة' },
  khalid: { email: 'khalid@demo.majlis.local', name: 'خالد' },
} as const;

/** Signs in through the real authorization-code flow: app → BFF → Auth host login page → back to the app. */
export async function signIn(page: Page, user: { email: string }, lang: 'ar' | 'en' = 'ar'): Promise<void> {
  // Only the first load picks the language; later loads keep what the user chose in the UI.
  await page.addInitScript((l) => {
    if (!localStorage.getItem('majlis.lang')) {
      localStorage.setItem('majlis.lang', l);
    }
  }, lang);
  await page.goto('/');
  await page.waitForURL(/\/Account\/Login/);
  await page.fill('input[name="Input.Email"]', user.email);
  await page.fill('input[name="Input.Password"]', PASSWORD);
  await page.click('button[type="submit"]');
  await page.waitForURL(/localhost:4200/);
  await expect(page.locator('majlis-layout')).toBeVisible();
}

export async function setTheme(page: Page, theme: 'light' | 'dark' | 'dim'): Promise<void> {
  await page.evaluate((t) => {
    localStorage.setItem('majlis.theme', t);
    document.documentElement.dataset['theme'] = t;
    document.documentElement.dataset['bsTheme'] = t === 'light' ? 'light' : 'dark';
  }, theme);
}
