import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, ensureDriving, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test('an admin sees control changes in the audit log, verifies the chain and exports it', async ({ page }, testInfo) => {
  await signIn(page, USERS.sara, 'ar');

  // A control change happens (start or claim a session), which the log must show.
  await page.goto('/rooms');
  await page.locator('a.room', { hasText: 'غرفة العقود' }).first().click();
  await ensureDriving(page);
  await page.getByRole('button', { name: 'التنازل عن التحكم' }).click();
  await expect(page.getByRole('button', { name: 'أخذ التحكم' })).toBeVisible({ timeout: 10000 });

  await page.locator('nav').getByRole('link', { name: 'سجل التدقيق' }).click();
  await expect(page.getByRole('heading', { name: 'سجل التدقيق' })).toBeVisible();
  await page.locator('select[name=category]').selectOption('session');
  await page.getByRole('button', { name: 'تطبيق' }).click();
  await expect(async () => {
    await page.getByRole('button', { name: 'تطبيق' }).click();
    await expect(page.getByTestId('audit-table').locator('tbody tr').first()).toContainText('تغيير التحكم');
  }).toPass({ timeout: 20000 });
  await page.getByTestId('audit-table').locator('tbody tr').first().click();
  await expect(page.locator('tr.details')).toContainText('hash');

  await page.getByTestId('audit-verify').click();
  await expect(page.getByTestId('audit-verification')).toContainText('السلسلة سليمة', { timeout: 15000 });
  await page.screenshot({ path: testInfo.outputPath('audit-ar-light.png'), fullPage: true });

  const download = page.waitForEvent('download');
  await page.getByTestId('audit-export').click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/^majlis-audit-.*\.csv$/);
  const text = await (await file.createReadStream()).toArray().then((chunks) => Buffer.concat(chunks).toString('utf8'));
  // The file starts with the UTF-8 BOM (for Excel), then the header row.
  expect(text.codePointAt(0)).toBe(0xfeff);
  expect(text.slice(1)).toMatch(/^seq,occurredAt,category/);
});

test('a member does not see the audit log', async ({ page }) => {
  await signIn(page, USERS.khalid, 'ar');
  await expect(page.locator('nav').getByRole('link', { name: 'سجل التدقيق' })).toHaveCount(0);
  await page.goto('/audit');
  await expect(page).toHaveURL(/\/403/);
});
