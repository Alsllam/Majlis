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

/**
 * Makes the user the driver of the room on screen whatever its current state: starts a session, claims free
 * control, or takes over (tenant admins). Polls because the room loads and the hub joins asynchronously.
 */
export async function ensureDriving(page: Page): Promise<void> {
  const driving = page.getByText('أنت تقود');
  const buttons = ['بدء جلسة', 'أخذ التحكم', 'الاستحواذ'].map((name) => page.getByRole('button', { name }));
  for (let i = 0; i < 40; i++) {
    if (await driving.isVisible()) {
      return;
    }
    for (const button of buttons) {
      if (await button.isVisible()) {
        // The button may disable or disappear while a command is in flight; try again on the next tick.
        await button.click({ timeout: 2000 }).catch(() => undefined);
        break;
      }
    }
    // eslint-disable-next-line playwright/no-wait-for-timeout -- polling the live room state, not a fixed sleep
    await page.waitForTimeout(500);
  }
  await expect(driving).toBeVisible();
}
