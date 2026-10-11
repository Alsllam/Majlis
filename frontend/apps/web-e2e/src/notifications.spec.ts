import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, ensureDriving, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test('an approval request reaches the approver live; the decision comes back to the requester', async ({ browser }, testInfo) => {
  test.setTimeout(180000);
  const sara = await (await browser.newContext()).newPage();
  const khalid = await (await browser.newContext()).newPage();
  await signIn(sara, USERS.sara, 'ar');
  await signIn(khalid, USERS.khalid, 'ar');

  // Khalid starts from a clean inbox (earlier runs leave rows behind), then waits on the tasks page: the bell is part of the shell on every page.
  await khalid.goto('/notifications');
  await expect(khalid.locator('majlis-notifications-list .row-item, majlis-notifications-list majlis-empty-state').first()).toBeVisible();
  const markAll = khalid.getByRole('button', { name: 'تعليم الكل كمقروء' });
  // The count loads asynchronously; keep clicking until the badge is gone.
  await expect(async () => {
    await markAll.click({ timeout: 2000 }).catch(() => undefined); // disabled once nothing is unread
    await expect(khalid.getByTestId('unread-count')).toHaveCount(0);
  }).toPass({ timeout: 15000 });
  await expect(markAll).toBeDisabled();
  await khalid.goto('/tasks');
  await expect(khalid.getByTestId('notification-bell')).toBeVisible();

  // Sara asks the agent for a task in the room; the stub model proposes create_task → approval request.
  await sara.goto('/rooms');
  await sara.locator('a.room', { hasText: 'غرفة العقود' }).first().click();
  await ensureDriving(sara);
  const title = `تجهيز العرض ${Date.now()}`;
  await sara.getByPlaceholder('اكتب تعليماتك للوكيل…').fill(`أنشئ مهمة ${title}`);
  await sara.keyboard.press('Enter');
  await expect(sara.locator('majlis-approval-card', { hasText: title })).toBeVisible({ timeout: 30000 });

  // Khalid (contributor, low risk → may decide) gets it pushed through the hub without reloading.
  // Other scenarios may leave notifications in flight for Khalid, so the count is at least one; the item itself is checked below.
  await expect(khalid.getByTestId('unread-count')).toBeVisible({ timeout: 30000 });
  await khalid.getByTestId('notification-bell').click();
  const item = khalid.locator('.row-item', { hasText: title }).first();
  await expect(item).toContainText('سارة');
  await khalid.screenshot({ path: testInfo.outputPath('notification-bell-ar-light.png') });
  await item.click();
  await expect(khalid).toHaveURL(/\/rooms\//);

  // Khalid approves on the card; Sara is told about the decision and the outcome.
  const card = khalid.locator('majlis-approval-card', { hasText: title });
  await card.getByRole('button', { name: 'موافقة' }).click();
  await expect(card.getByText('نُفِّذ', { exact: true })).toBeVisible({ timeout: 30000 });
  await sara.getByTestId('notification-bell').click();
  await expect(sara.locator('.row-item', { hasText: 'خالد' }).first()).toBeVisible({ timeout: 30000 });
  await expect(sara.locator('.row-item', { hasText: 'نتيجة الإجراء' }).first()).toBeVisible({ timeout: 30000 });

  // The full list and the settings page.
  await khalid.goto('/notifications');
  await expect(khalid.locator('majlis-notifications-list .row-item', { hasText: title })).toBeVisible();
  // Opening it from the bell already marked it read, so nothing is unread and the button is disabled.
  await expect(khalid.getByTestId('unread-count')).toHaveCount(0);
  await expect(khalid.getByRole('button', { name: 'تعليم الكل كمقروء' })).toBeDisabled();
  await khalid.goto('/notifications/settings');
  const email = khalid.locator('#email-enabled');
  await expect(email).toBeVisible();
  const wasChecked = await email.isChecked();
  await email.setChecked(!wasChecked);
  await khalid.getByRole('button', { name: 'حفظ' }).click();
  await expect(khalid.getByText('حُفظت الإعدادات')).toBeVisible();
  await khalid.reload();
  await expect(khalid.locator('#email-enabled')).toBeChecked({ checked: !wasChecked });
  await khalid.locator('#email-enabled').setChecked(wasChecked);
  await khalid.getByRole('button', { name: 'حفظ' }).click();
  await expect(khalid.getByText('حُفظت الإعدادات')).toBeVisible();
});
