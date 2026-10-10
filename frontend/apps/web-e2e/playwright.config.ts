import { defineConfig, devices } from '@playwright/test';
import { nxE2EPreset } from '@nx/playwright/preset';
import { workspaceRoot } from '@nx/devkit';

const baseURL = process.env['BASE_URL'] || 'http://localhost:4200';

/**
 * Browser smoke tests against a running local stack (`docker compose up -d`, `backend/scripts/run-local.sh`).
 * Sign-in uses the seeded demo users; set MAJLIS_SEED_PASSWORD to run them, otherwise they are skipped.
 */
export default defineConfig({
  ...nxE2EPreset(__filename, { testDir: './src' }),
  use: {
    baseURL,
    trace: 'on-first-retry',
    locale: 'ar-SA',
    // A pre-installed Chromium can be used instead of a download (PLAYWRIGHT_CHROMIUM_PATH=/path/to/chromium).
    launchOptions: process.env['PLAYWRIGHT_CHROMIUM_PATH'] ? { executablePath: process.env['PLAYWRIGHT_CHROMIUM_PATH'] } : {},
  },
  webServer: {
    command: 'npx nx run web:serve',
    url: 'http://localhost:4200',
    reuseExistingServer: true,
    cwd: workspaceRoot,
    timeout: 120000,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
