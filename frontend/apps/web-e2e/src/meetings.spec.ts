import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test('minutes from notes: the agent drafts, a person edits and approves, items become approvals, minutes reach the knowledge', async ({ page }, testInfo) => {
  test.setTimeout(180000);
  await signIn(page, USERS.sara, 'ar');

  // Create a meeting from pasted notes in the legal workspace.
  await page.goto('/meetings');
  const legal = await page.locator('.ws-select option', { hasText: 'الشؤون القانونية' }).getAttribute('value');
  await page.locator('.ws-select').selectOption(legal ?? '');
  await page.getByRole('button', { name: 'اجتماع جديد' }).click();
  const title = `اجتماع مراجعة العقود ${Date.now()}`;
  await page.locator('#meeting-title').fill(title);
  await page.locator('#meeting-attendees').fill('سارة، خالد');
  await page.locator('#meeting-text').fill('ناقشنا العقد الجديد مع المورد. اتفقنا على مراجعة البنود المالية. خالد يرسل المسودة النهائية الأسبوع القادم.');
  await page.getByRole('button', { name: 'إنشاء' }).click();
  await expect(page).toHaveURL(/\/meetings\/[0-9a-f-]+$/);
  const meetingUrl = page.url();
  await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
  await expect(page.getByTestId('meeting-status')).toHaveText('جاهز للمحضر');

  // The agent writes the draft (stub model) and the page refreshes by itself.
  await page.getByRole('button', { name: 'كتابة المحضر' }).click();
  await expect(page.getByTestId('meeting-status')).toHaveText('مسودة المحضر', { timeout: 60000 });
  const minutes = page.getByTestId('minutes');
  await expect(minutes.locator('#minutes-summary')).toHaveValue(/ناقش الفريق/);
  await expect(minutes.locator('.item-row')).toHaveCount(2);
  await page.screenshot({ path: testInfo.outputPath('meeting-draft-ar-light.png'), fullPage: true });

  // People edit: change the summary, add an item, then approve (the edit is saved first).
  await minutes.locator('#minutes-summary').fill('ملخص معدّل من الفريق.');
  await minutes.getByRole('button', { name: 'إضافة بند' }).click();
  await minutes.locator('.item-row').nth(2).locator('input').first().fill('إبلاغ الإدارة بالنتيجة');
  await minutes.getByRole('button', { name: 'اعتماد المحضر' }).click();
  await expect(page.getByTestId('meeting-status')).toHaveText('محضر معتمد');
  await expect(minutes.locator('.summary')).toHaveText('ملخص معدّل من الفريق.');
  await expect(minutes.locator('table.items tbody tr')).toHaveCount(3);
  await expect(minutes.locator('table.items').getByText('لم يُقترح بعد')).toHaveCount(3);

  // The action items go to Approvals in bulk; each shows as awaiting approval and appears in the inbox.
  await minutes.getByRole('button', { name: /اقتراح بنود العمل كمهام/ }).click();
  await expect(minutes.locator('table.items').getByText('بانتظار الموافقة')).toHaveCount(3, { timeout: 15000 });
  await expect(minutes.getByRole('button', { name: /اقتراح بنود العمل/ })).toHaveCount(0);

  // Publish to the knowledge, then export the markdown.
  await minutes.getByRole('button', { name: 'نشر في المعرفة' }).click();
  await expect(minutes.getByText('منشور في المعرفة').first()).toBeVisible();
  const download = page.waitForEvent('download');
  await minutes.getByRole('button', { name: 'تنزيل Markdown' }).click();
  expect((await download).suggestedFilename()).toMatch(/^minutes-\d{8}\.md$/);
  await page.screenshot({ path: testInfo.outputPath('meeting-approved-ar-light.png'), fullPage: true });

  // One approval per item in the approver inbox (low risk: the owner may decide); approving one creates the task.
  await page.goto('/approvals');
  const request = page.locator('article.request', { hasText: 'إبلاغ الإدارة بالنتيجة' }).first();
  await expect(request).toBeVisible();
  await request.getByRole('button', { name: 'موافقة' }).click();
  await expect(page.getByText('تمت الموافقة؛ سيُنفَّذ الإجراء الآن')).toBeVisible();

  // Tasks created it and told Meetings: the item links to the task (events through the bus, so the page is reloaded).
  await expect(async () => {
    await page.goto(meetingUrl);
    await expect(page.locator('table.items').getByText('أصبح مهمة')).toHaveCount(1);
  }).toPass({ timeout: 30000 });
  await page.goto('/tasks');
  await page.locator('.ws-select').selectOption(legal ?? '');
  await expect(page.locator('tbody tr', { hasText: 'إبلاغ الإدارة بالنتيجة' }).first()).toBeVisible({ timeout: 15000 });

  // The minutes are a document of the workspace.
  await page.goto('/documents');
  await page.locator('.ws-select').selectOption(legal ?? '');
  await expect(page.locator('tbody tr', { hasText: `محضر اجتماع: ${title}` })).toBeVisible({ timeout: 15000 });

  // The list shows the approved meeting.
  await page.goto(`/meetings?workspaceId=${legal}`);
  const row = page.locator('tbody tr', { hasText: title });
  await expect(row).toContainText('محضر معتمد');
  await expect(row).toContainText('3');
});
