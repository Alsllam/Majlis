import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, ensureDriving, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test('the agent proposes a task, a person approves it in the room, and the task appears in the list', async ({ page }, testInfo) => {
  await signIn(page, USERS.sara, 'ar');

  // Ask the agent to create a task; the stub model answers with a create_task call that becomes an approval request.
  await page.goto('/rooms');
  await page.locator('a.room', { hasText: 'غرفة العقود' }).first().click();
  await ensureDriving(page);
  const title = `مراجعة عقد المورد ${Date.now()}`;
  await page.getByPlaceholder('اكتب تعليماتك للوكيل…').fill(`أنشئ مهمة ${title}`);
  await page.keyboard.press('Enter');
  const card = page.locator('majlis-approval-card', { hasText: title });
  await expect(card).toBeVisible({ timeout: 30000 });
  await expect(card.getByText('بانتظار الموافقة')).toBeVisible();
  const turn = page.locator('article.turn', { hasText: title });
  await expect(turn.locator('.agent .text.streaming')).toHaveCount(0, { timeout: 30000 });
  await expect(turn.locator('.agent .text')).toContainText('للموافقة');
  await page.screenshot({ path: testInfo.outputPath('approval-pending-ar-light.png'), fullPage: true });

  // Approve: the card moves to approved then done once Tasks has created the task (events through the hub).
  await card.getByRole('button', { name: 'موافقة' }).click();
  await expect(card.getByText('نُفِّذ', { exact: true })).toBeVisible({ timeout: 30000 });
  await expect(card).toContainText('نُفِّذت');
  await page.screenshot({ path: testInfo.outputPath('approval-executed-ar-light.png'), fullPage: true });

  // The approvals history lists it as done; the tasks list shows the agent-created task.
  await page.goto('/approvals');
  await page.getByRole('tab', { name: 'السجل' }).click();
  await expect(page.locator('article.request', { hasText: title })).toContainText('نُفِّذ');
  await page.goto('/tasks');
  const legal = await page.locator('.ws-select option', { hasText: 'الشؤون القانونية' }).getAttribute('value');
  await page.locator('.ws-select').selectOption(legal ?? '');
  const row = page.locator('tbody tr', { hasText: title });
  await expect(row).toBeVisible({ timeout: 15000 });
  await expect(row.getByText('من الوكيل')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('tasks-ar-light.png'), fullPage: true });
});

test('a rejected proposal never becomes a task', async ({ page }) => {
  await signIn(page, USERS.sara, 'ar');
  await page.goto('/rooms');
  await page.locator('a.room', { hasText: 'غرفة العقود' }).first().click();
  await ensureDriving(page);
  const title = `مهمة مرفوضة ${Date.now()}`;
  await page.getByPlaceholder('اكتب تعليماتك للوكيل…').fill(`أنشئ مهمة ${title}`);
  await page.keyboard.press('Enter');
  const card = page.locator('majlis-approval-card', { hasText: title });
  await expect(card).toBeVisible({ timeout: 30000 });

  await card.getByRole('button', { name: 'رفض' }).click();
  await page.locator('#reason-text').fill('ليست أولوية هذا الأسبوع');
  await page.getByRole('dialog').getByRole('button', { name: 'رفض' }).click();
  await expect(card.getByText('مرفوض', { exact: true })).toBeVisible({ timeout: 30000 });
  await expect(card).toContainText('ليست أولوية هذا الأسبوع');

  await page.goto('/tasks');
  const legal = await page.locator('.ws-select option', { hasText: 'الشؤون القانونية' }).getAttribute('value');
  await page.locator('.ws-select').selectOption(legal ?? '');
  await expect(page.locator('tbody, majlis-empty-state').first()).toBeVisible();
  await expect(page.locator('tbody tr', { hasText: title })).toHaveCount(0);
});
