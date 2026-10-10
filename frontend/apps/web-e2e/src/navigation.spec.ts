import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test('a member sees the menu in Arabic and English and can switch language', async ({ page }) => {
  await signIn(page, USERS.khalid, 'ar');
  await expect(page.getByRole('link', { name: 'الغرف' })).toBeVisible();
  await page.getByRole('button', { name: 'اللغة' }).click();
  await page.getByRole('button', { name: 'English' }).click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  await expect(page.getByRole('link', { name: 'Rooms' })).toBeVisible();
  await page.goto('/no-such-page');
  await expect(page.getByText('Page not found')).toBeVisible();
});
