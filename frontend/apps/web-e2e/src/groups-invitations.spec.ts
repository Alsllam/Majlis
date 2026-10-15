import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

const AUTH_HOST = process.env['AUTH_HOST_URL'] || 'http://localhost:7001';

test('a tenant admin builds a group, an owner grants it a role, and an invited stranger registers into the workspace', async ({ browser, request }, testInfo) => {
  test.setTimeout(180000);
  const sara = await (await browser.newContext()).newPage();
  const khalid = await (await browser.newContext()).newPage();
  await signIn(sara, USERS.sara, 'ar');
  await signIn(khalid, USERS.khalid, 'ar');

  // 1. Sara (tenant admin) creates a group and puts Khalid in it.
  const groupName = `الفريق القانوني ${Date.now()}`;
  await sara.goto('/admin/groups');
  await expect(sara.getByRole('heading', { name: 'المجموعات' })).toBeVisible();
  await sara.locator('#group-name').fill(groupName);
  await sara.locator('#group-name').press('Enter');
  const detail = sara.getByTestId('group-detail');
  await expect(detail.locator('#group-edit-name')).toHaveValue(groupName);
  await detail.locator('#group-member-search').fill('خالد');
  await detail.locator('button.user', { hasText: 'خالد' }).click();
  await expect(detail.locator('.members')).toContainText('خالد');
  await sara.screenshot({ path: testInfo.outputPath('groups-ar-light.png'), fullPage: true });

  // 2. Sara creates a workspace and grants the group the Viewer role; Khalid sees the workspace through the group.
  const wsName = `المراجعة ${Date.now()}`;
  await sara.goto('/workspaces');
  await sara.getByRole('button', { name: /مساحة عمل جديدة/ }).click();
  await sara.locator('#ws-name').fill(wsName);
  await sara.locator('majlis-workspace-form-modal button[type="submit"]').click();
  await sara.locator('a.ws', { hasText: wsName }).click();
  await expect(sara.locator('majlis-workspace h1')).toHaveText(wsName);
  const groups = sara.getByTestId('workspace-groups');
  await groups.locator('#ws-group').selectOption({ label: groupName });
  await groups.locator('#ws-group-role').selectOption('Viewer');
  await groups.getByRole('button', { name: 'منح الدور' }).click();
  await expect(groups.locator('tbody')).toContainText(groupName);
  // Group membership reaches Workspaces through the bus (GroupMemberAdded); on a cold host that can lag, so reload until it lands.
  await expect(async () => {
    await sara.reload();
    await expect(sara.locator('majlis-workspace .members table')).toContainText(`عبر ${groupName}`, { timeout: 3000 });
  }).toPass({ timeout: 45000, intervals: [2000] });
  await sara.screenshot({ path: testInfo.outputPath('workspace-groups-ar-light.png'), fullPage: true });

  await expect(async () => {
    await khalid.goto('/workspaces');
    await expect(khalid.locator('a.ws', { hasText: wsName })).toBeVisible({ timeout: 3000 });
  }).toPass({ timeout: 30000, intervals: [2000] });
  await khalid.locator('a.ws', { hasText: wsName }).click();
  await expect(khalid.locator('.actions .status-tag')).toHaveText('مشاهد');

  // 3. Sara invites a stranger by email; the dev Auth host keeps the email, the link opens the registration page.
  const email = `laila.${Date.now()}@example.com`;
  const invitations = sara.getByTestId('workspace-invitations');
  await invitations.locator('#ws-invite-email').fill(email);
  await invitations.locator('#ws-invite-role').selectOption('Contributor');
  await invitations.getByRole('button', { name: 'دعوة' }).click();
  await expect(invitations.locator('.pending')).toContainText(email);

  const emails = (await (await request.get(`${AUTH_HOST}/dev/emails`)).json()) as { to: string; textBody: string }[];
  const mail = emails.find((m) => m.to === email);
  expect(mail).toBeDefined();
  const link = mail?.textBody.split('\n').find((l) => l.includes('/Account/Register?token=')) ?? '';
  expect(link).toContain('token=');

  const laila = await (await browser.newContext()).newPage();
  await laila.goto(link);
  await expect(laila.locator('#email')).toHaveValue(email);
  await laila.locator('#name').fill('ليلى');
  await laila.locator('#password').fill(PASSWORD);
  await laila.getByRole('button', { name: 'إنشاء الحساب' }).click();
  await expect(laila.getByText('أُنشئ حسابك')).toBeVisible();
  await laila.screenshot({ path: testInfo.outputPath('register-ar-light.png') });

  // 4. Registration signed her in on the auth host, so the web app goes straight through the code flow; the workspace is hers as a contributor.
  await laila.goto('/');
  await laila.waitForURL(/localhost:4200\/(home|workspaces)/);
  await expect(laila.locator('majlis-layout')).toBeVisible();
  await laila.goto('/workspaces');
  await expect(async () => {
    await laila.reload();
    await expect(laila.locator('a.ws', { hasText: wsName })).toBeVisible({ timeout: 3000 });
  }).toPass({ timeout: 30000, intervals: [2000] });
  await laila.locator('a.ws', { hasText: wsName }).click();
  await expect(laila.locator('.actions .status-tag')).toHaveText('مساهم');
  await sara.reload();
  await expect(sara.getByTestId('workspace-invitations').locator('.pending')).toHaveCount(0);

  // 5. Removing Khalid from the group takes the workspace away from him.
  await sara.goto('/admin/groups');
  await sara.locator('button.group', { hasText: groupName }).click();
  await sara.getByTestId('group-detail').locator('.members li', { hasText: 'خالد' }).getByRole('button', { name: 'إزالة' }).click();
  await expect(async () => {
    await khalid.goto('/workspaces');
    await expect(khalid.locator('a.ws', { hasText: wsName })).toHaveCount(0, { timeout: 3000 });
  }).toPass({ timeout: 30000, intervals: [2000] });
});
