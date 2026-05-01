import { defineConfig, devices } from '@playwright/test'

/**
 * Playwright configuration for the Trading System dashboard E2E suite.
 *
 * Phase 7.6 deliverable: 5 critical-journey specs covering asset-filter
 * persistence, theme toggling, sidebar navigation, the Semaphore widget,
 * and the strategy-wizard happy path.
 *
 * Viewport matrix is intentionally desktop-only — the dashboard kit targets
 * 1366x768 as the floor and 1920x1080 as the baseline. Mobile is out of
 * scope for this phase.
 *
 * Base URL strategy:
 *   - Local dev (default): spin up `npm run dev` on port 5173 and point there.
 *   - CI / remote-staging: set PLAYWRIGHT_BASE_URL to the deployed dashboard
 *     URL (e.g. the Cloudflare Pages preview) and SKIP_WEBSERVER=1 to avoid
 *     starting a local server.
 */

const BASE_URL = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5173'
const SKIP_WEBSERVER = process.env.SKIP_WEBSERVER === '1'

export default defineConfig({
  testDir: './tests/e2e',
  // Each spec is small, so sharding isn't interesting. Parallel workers are
  // fine — our fixtures never touch cross-spec state (localStorage is scoped
  // to a browser context).
  fullyParallel: true,
  // Fail the build on test.only in CI. Locally we tolerate it so devs can
  // focus on a single spec during investigation.
  forbidOnly: !!process.env.CI,
  // Retries: only in CI, where occasional flakes (font load, animation
  // completion race) are acceptable if they self-heal on retry. Zero retries
  // locally so flakes surface immediately.
  retries: process.env.CI ? 2 : 0,
  // Limit worker parallelism on CI to keep memory under GH Actions' 7GB cap.
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
  ],
  use: {
    baseURL: BASE_URL,
    // Capture trace on first retry — useful for CI post-mortems without
    // blowing up storage. `screenshot: 'only-on-failure'` complements it.
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    // The kit assumes a light desktop. No reduced-motion preference because
    // some of our assertions (e.g. theme attribute flip) happen mid-transition.
    // Timeouts tuned for Vite dev-server hot-start.
    actionTimeout: 10_000,
    navigationTimeout: 20_000,
  },
  // Viewport matrix — desktop-only per product scope. 1920 is the baseline
  // laptop-display-plus-external-monitor target; 1366 is the floor for a
  // legacy corporate laptop.
  projects: [
    {
      name: 'chromium-1920x1080',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1920, height: 1080 },
      },
    },
    {
      name: 'chromium-1366x768',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1366, height: 768 },
      },
    },
  ],
  // Spin up the dev server for local runs. CI sets SKIP_WEBSERVER=1 and
  // targets a preview URL directly.
  webServer: SKIP_WEBSERVER
    ? undefined
    : {
        command: 'npm run dev -- --host 127.0.0.1 --port 5173',
        url: 'http://127.0.0.1:5173',
        reuseExistingServer: !process.env.CI,
        timeout: 60_000,
      },
})
