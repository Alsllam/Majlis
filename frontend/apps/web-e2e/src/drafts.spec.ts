import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, ensureDriving, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test('the agent drafts a document, a person approves it, reads it and adds it to the knowledge', async ({ page }, testInfo) => {
  test.setTimeout(150000);
  await signIn(page, USERS.sara, 'ar');
  await page.goto('/rooms');
  await page.locator('a.room', { hasText: 'غرفة العقود' }).first().click();
  await ensureDriving(page);
  const title = `سياسة الحضور ${Date.now()}`;
  await page.getByPlaceholder('اكتب تعليماتك للوكيل…').fill(`اكتب مسودة ${title}`);
  await page.keyboard.press('Enter');

  // The card shows the draft text in a collapsible block, then runs once approved.
  const card = page.locator('majlis-approval-card', { hasText: title });
  await expect(card).toBeVisible({ timeout: 30000 });
  await card.getByText('عرض النص الكامل').click();
  await expect(card.locator('pre')).toContainText('البند الأول');
  await card.getByRole('button', { name: 'موافقة' }).click();
  await expect(card.getByText('نُفِّذ', { exact: true })).toBeVisible({ timeout: 30000 });
  await page.screenshot({ path: testInfo.outputPath('draft-card-ar-light.png'), fullPage: true });

  // The draft opens from the card: readable in the page, marked as a draft, not indexed yet.
  await card.getByTestId('open-draft').click();
  await expect(page.locator('majlis-document h1')).toContainText(title);
  await expect(page.getByTestId('draft-tag')).toBeVisible();
  await expect(page.getByTestId('document-content')).toContainText('البند الأول');
  await expect(page.locator('.versions tbody tr').first()).toContainText('محفوظة');
  await page.screenshot({ path: testInfo.outputPath('draft-page-ar-light.png'), fullPage: true });

  // A person adds it to the knowledge: it becomes active and gets indexed like an upload.
  await page.getByRole('button', { name: 'إضافة إلى المعرفة' }).click();
  await expect(page.getByText('أُضيفت إلى المعرفة')).toBeVisible();
  await expect(page.getByTestId('draft-tag')).toHaveCount(0);
  await page.goto('/documents');
  const legal = await page.locator('.ws-select option', { hasText: 'الشؤون القانونية' }).getAttribute('value');
  await page.locator('.ws-select').selectOption(legal ?? '');
  const row = page.locator('tbody tr', { hasText: title });
  await expect(row.locator('.status-tag-success')).toHaveText('مفهرس', { timeout: 60000 });
});
