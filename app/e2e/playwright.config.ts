import { defineConfig, devices } from '@playwright/test';

// The suite runs against the test stack only (see scripts/start-stack.mjs): a separate PostgreSQL
// database, the API and the built web app. It never talks to the developer's dev database.
// E2E_BASE_URL must match E2E_APP_PORT used by the stack script (default http://localhost:5100).
const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5100';

export default defineConfig({
  testDir: './specs',
  // Each test registers its own email and starts in a fresh browser context, so tests share no state.
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  use: {
    baseURL,
    headless: true,
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  // Starts the test stack once per run and waits for the API health check. reuseExistingServer is false,
  // so the suite never attaches to some other server already on the port.
  webServer: {
    command: 'node scripts/start-stack.mjs',
    url: `${baseURL}/api/health`,
    reuseExistingServer: false,
    timeout: 300_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
