import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test('an owner creates a workspace, adds a member and writes agent instructions; the member sees it', async ({ browser }, testInfo) => {
  const sara = await (await browser.newContext()).newPage();
  const khalid = await (await browser.newContext()).newPage();
  await signIn(sara, USERS.sara, 'ar');
  await signIn(khalid, USERS.khalid, 'en');

  // The home screen lists workspaces; the seeded one is there.
  await expect(sara.getByRole('heading', { name: 'مساحات العمل' })).toBeVisible();
  await expect(sara.locator('a.ws', { hasText: 'الشؤون القانونية' })).toBeVisible();
  await sara.screenshot({ path: testInfo.outputPath('workspaces-ar-light.png'), fullPage: true });

  // Create a workspace.
  const name = 'العمليات ' + Date.now();
  await sara.getByRole('button', { name: /مساحة عمل جديدة/ }).click();
  await sara.locator('#ws-name').fill(name);
  await sara.locator('#ws-desc').fill('تشغيل المكاتب والمشتريات');
  await sara.getByRole('radio', { name: 'النحاسي' }).click();
  await sara.locator('majlis-workspace-form-modal button[type="submit"]').click();
  await expect(sara.locator('a.ws', { hasText: name })).toBeVisible();
  await sara.locator('a.ws', { hasText: name }).click();
  await expect(sara.locator('majlis-workspace h1')).toHaveText(name);
  await expect(sara.locator('.actions .status-tag')).toHaveText('مالك');

  // Khalid does not see it yet.
  await khalid.goto('/workspaces');
  await expect(khalid.locator('a.ws', { hasText: name })).toHaveCount(0);

  // Add Khalid as a contributor through the user lookup.
  await sara.getByRole('button', { name: /إضافة عضو/ }).click();
  await sara.locator('#member-search').fill('خالد');
  await sara.locator('button.user', { hasText: 'خالد' }).click();
  await sara.locator('#member-role').selectOption('Contributor');
  await sara.locator('.modal-footer').getByRole('button', { name: 'إضافة عضو' }).click();
  await expect(sara.locator('majlis-workspace table')).toContainText('خالد');

  // Agent instructions.
  await sara.locator('majlis-workspace textarea').fill('استخدم العربية الفصحى واذكر رقم المادة.');
  await sara.locator('.instructions').getByRole('button', { name: 'حفظ' }).click();
  await expect(sara.getByText('تم الحفظ')).toBeVisible();
  await sara.screenshot({ path: testInfo.outputPath('workspace-ar-light.png'), fullPage: true });

  // Khalid now sees the workspace as a contributor and can create a room in it.
  await khalid.goto('/workspaces');
  await expect(khalid.locator('a.ws', { hasText: name })).toBeVisible();
  await khalid.locator('a.ws', { hasText: name }).click();
  await expect(khalid.locator('.actions .status-tag')).toHaveText('Contributor');
  await expect(khalid.getByRole('button', { name: /Add member/ })).toHaveCount(0);
  await khalid.getByRole('button', { name: 'Rooms' }).click();
  await khalid.getByRole('button', { name: /New room/ }).click();
  await expect(khalid.locator('#room-workspace')).toContainText(name);
  const roomName = 'Procurement ' + Date.now();
  await khalid.locator('#room-name').fill(roomName);
  await khalid.locator('majlis-room-create-modal button[type="submit"]').click();
  await expect(khalid.locator('a.room', { hasText: roomName })).toBeVisible();
  await expect(khalid.locator('a.room', { hasText: roomName })).toContainText(name);
  await khalid.screenshot({ path: testInfo.outputPath('rooms-en-light.png'), fullPage: true });
});
