import { defineConfig } from '@playwright/test';

// Temporary local-only config: uses the already-installed system Google Chrome
// via `channel: 'chrome'` instead of downloading Playwright's bundled chromium.
export default defineConfig({
  testDir: 'e2e',
  fullyParallel: false,
  timeout: 30_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: 'npm start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
  },
  projects: [{ name: 'chrome-system', use: { browserName: 'chromium', channel: 'chrome' } }],
});
