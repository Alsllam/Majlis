import { expect, test } from '@playwright/test';
import { PASSWORD, USERS, setTheme, signIn } from './helpers';

// eslint-disable-next-line playwright/no-skipped-test -- needs the local backend; skipped where it is not running
test.skip(!PASSWORD, 'MAJLIS_SEED_PASSWORD is not set: needs the local backend');

test.describe('shared agent session', () => {
  test('two users see the same stream, then hand control over', async ({ browser }, testInfo) => {
    const sara = await (await browser.newContext()).newPage();
    const khalid = await (await browser.newContext()).newPage();
    await signIn(sara, USERS.sara, 'ar');
    await signIn(khalid, USERS.khalid, 'en');

    // Rooms list, RTL for Sara and LTR for Khalid.
    await expect(sara.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(khalid.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(sara.getByRole('heading', { name: 'الغرف' })).toBeVisible();
    await sara.screenshot({ path: testInfo.outputPath('rooms-ar-light.png'), fullPage: true });

    // Both open the first room.
    await sara.locator('a.room').first().click();
    await khalid.locator('a.room').first().click();
    await expect(sara.locator('majlis-room h1')).not.toHaveText(/…/);

    // Sara drives: start a session when none is active, or take control. The steps adapt to the live backend state.
    /* eslint-disable playwright/no-conditional-in-test */
    const start = sara.getByRole('button', { name: 'بدء جلسة' });
    if (await start.isVisible()) {
      await start.click();
    }
    const claim = sara.getByRole('button', { name: 'أخذ التحكم' });
    if (await claim.isVisible({ timeout: 2000 }).catch(() => false)) {
      await claim.click();
    }
    const takeOver = sara.getByRole('button', { name: 'الاستحواذ' });
    if (await takeOver.isVisible({ timeout: 2000 }).catch(() => false)) {
      await takeOver.click();
    }
    /* eslint-enable playwright/no-conditional-in-test */
    await expect(sara.getByText('أنت تقود')).toBeVisible();

    // Instruction → both timelines show the user turn and the streamed agent text.
    const instruction = 'لخّص البنود الرئيسية في عقد المورد ' + Date.now();
    await sara.getByPlaceholder('اكتب تعليماتك للوكيل…').fill(instruction);
    await sara.keyboard.press('Enter');
    await expect(sara.getByText(instruction)).toBeVisible();
    await expect(khalid.getByText(instruction)).toBeVisible({ timeout: 10000 });
    const agentText = 'يمر النص عبر قناة البث';
    await expect(khalid.locator('.agent .text').last()).toContainText(agentText, { timeout: 20000 });
    await expect(sara.locator('.agent .text').last()).toContainText(agentText);
    await expect(sara.locator('.agent .text.streaming')).toHaveCount(0, { timeout: 20000 });
    await expect(khalid.getByText('AI-generated').last()).toBeVisible();
    await sara.screenshot({ path: testInfo.outputPath('room-ar-light.png'), fullPage: true });
    await setTheme(khalid, 'dark');
    await khalid.screenshot({ path: testInfo.outputPath('room-en-dark.png'), fullPage: true });

    // Presence shows both people.
    await expect(sara.locator('.people li', { hasText: USERS.khalid.name })).toBeVisible({ timeout: 10000 });

    // Khalid requests control; Sara accepts; Khalid now drives.
    await khalid.getByRole('button', { name: 'Request control' }).click();
    await expect(sara.getByText('خالد يطلب التحكم')).toBeVisible({ timeout: 10000 });
    await sara.getByRole('button', { name: 'قبول' }).click();
    await expect(khalid.getByText('You are driving')).toBeVisible({ timeout: 10000 });
    await expect(sara.getByText('القائد: خالد')).toBeVisible({ timeout: 10000 });

    // Khalid hands control back with a note; Sara accepts.
    await khalid.getByRole('button', { name: 'Hand off' }).click();
    await khalid.locator('#handoff-to').selectOption({ label: USERS.sara.name });
    await khalid.locator('#handoff-note').fill('دورك');
    await khalid.locator('majlis-hand-off-modal button[type="submit"]').click();
    await expect(sara.getByText('خالد يعرض عليك قيادة الجلسة')).toBeVisible({ timeout: 10000 });
    await sara.getByRole('button', { name: 'قبول' }).click();
    await expect(sara.getByText('أنت تقود')).toBeVisible({ timeout: 10000 });
    await setTheme(sara, 'dim');
    await sara.screenshot({ path: testInfo.outputPath('room-ar-dim.png'), fullPage: true });
  });
});
