import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, setTheme, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test('the home page lands on the last workspace with its rooms, approvals, tasks and documents', async ({ page }, testInfo) => {
  await signIn(page, USERS.sara, 'ar');

  // Sign-in lands on the home of a workspace; the seeded legal workspace has the contracts room.
  await expect(page).toHaveURL(/\/home/);
  await expect(page.getByRole('heading', { name: /مرحباً سارة/ })).toBeVisible();
  const legal = await page.locator('.ws-select option', { hasText: 'الشؤون القانونية' }).getAttribute('value');
  await page.locator('.ws-select').selectOption(legal ?? '');
  await expect(page).toHaveURL(new RegExp(`workspaceId=${legal}`));
  await expect(page.getByTestId('home-tiles').locator('.tile')).toHaveCount(4);
  await expect(page.getByTestId('home-rooms')).toContainText('غرفة العقود');
  await expect(page.getByTestId('home-approvals')).toBeVisible();
  await expect(page.getByTestId('home-tasks')).toBeVisible();
  await expect(page.getByTestId('home-documents')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('home-ar-light.png'), fullPage: true });
  await setTheme(page, 'dark');
  await page.screenshot({ path: testInfo.outputPath('home-ar-dark.png'), fullPage: true });

  // The choice is remembered: the next visit to the menu entry opens the same workspace.
  await page.getByRole('link', { name: 'الرئيسية' }).click();
  await page.goto('/home');
  await expect(page.locator('.ws-select')).toHaveValue(legal ?? '');

  // A room opens from the home list.
  await page.getByTestId('home-rooms').getByRole('link', { name: /غرفة العقود/ }).click();
  await expect(page.locator('majlis-room h1')).toContainText('غرفة العقود');
});
