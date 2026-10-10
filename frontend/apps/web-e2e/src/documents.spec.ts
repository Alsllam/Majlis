import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, ensureDriving, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

const REGULATION = `# لائحة المشتريات

## الباب الأول: أوامر الشراء

المادة 1: يلتزم المورد بتسليم البضاعة خلال ثلاثين يوماً من تاريخ أمر الشراء.

المادة 2: يتحمل المورد غرامة تأخير قدرها واحد بالمائة من قيمة أمر الشراء عن كل أسبوع تأخير، بحد أقصى عشرة بالمائة.

## الباب الثاني: الاستلام

المادة 3: تُفحص البضاعة خلال خمسة أيام عمل من الاستلام ويُحرر محضر بذلك.
`;

test('a contributor uploads a document, sees it indexed, and the agent cites it in the room', async ({ page }, testInfo) => {
  await signIn(page, USERS.sara, 'ar');

  // Upload a markdown regulation to the seeded workspace.
  await page.goto('/documents');
  await expect(page.getByRole('heading', { name: 'المستندات' })).toBeVisible();
  await page.locator('.ws-select').selectOption({ label: /الشؤون القانونية/ });
  await page.locator('.type-select').selectOption('Regulation');
  const name = `لائحة-المشتريات-${Date.now()}.md`;
  await page.locator('input[type=file]').setInputFiles({ name, mimeType: 'text/markdown', buffer: Buffer.from(REGULATION, 'utf8') });
  const row = page.locator('tbody tr', { hasText: name.replace('.md', '') });
  await expect(row).toBeVisible({ timeout: 15000 });
  await expect(row.locator('.status-tag-success')).toHaveText('مفهرس', { timeout: 60000 });
  await page.screenshot({ path: testInfo.outputPath('documents-ar-light.png'), fullPage: true });

  // Ask about it in the room; the stub model cites [S1] when sources are present.
  await page.goto('/rooms');
  await page.locator('a.room', { hasText: 'غرفة العقود' }).first().click();
  await ensureDriving(page);
  const question = 'ما غرامة التأخير في لائحة المشتريات؟ ' + Date.now();
  await page.getByPlaceholder('اكتب تعليماتك للوكيل…').fill(question);
  await page.keyboard.press('Enter');
  await expect(page.getByText(question)).toBeVisible();
  const turn = page.locator('article.turn', { hasText: question });
  await expect(turn.locator('.agent .text.streaming')).toHaveCount(0, { timeout: 30000 });
  const citation = turn.locator('a.cite').first();
  await expect(citation).toBeVisible({ timeout: 10000 });
  await expect(citation).toContainText('[S1]');
  await page.screenshot({ path: testInfo.outputPath('room-citation-ar-light.png'), fullPage: true });

  // The citation opens the document at the cited passage.
  await citation.click();
  await expect(page.locator('majlis-document h1')).toContainText('لائحة-المشتريات');
  await expect(page.locator('.cited blockquote')).toContainText('غرامة');
  await page.screenshot({ path: testInfo.outputPath('document-ar-light.png'), fullPage: true });
});
