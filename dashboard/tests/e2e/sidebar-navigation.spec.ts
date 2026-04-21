import { test, expect, type ConsoleMessage, type Request } from '@playwright/test'
import { installApiMocks } from './fixtures/api-mock'

/**
 * Critical journey #3 — Every top-level sidebar page renders without
 * console errors and without 404s on asset fetches.
 *
 * Each entry below matches one Sidebar nav anchor. The route list is
 * duplicated here (rather than imported from src/) on purpose: if a dev
 * renames a route without updating both places, this test fails loudly.
 */
const ROUTES: Array<{ label: RegExp; path: string }> = [
  { label: /^overview$/i, path: '/' },
  { label: /^system health$/i, path: '/health' },
  { label: /^positions$/i, path: '/positions' },
  { label: /^campaigns$/i, path: '/campaigns' },
  { label: /^ivts monitor$/i, path: '/ivts' },
  { label: /^alerts$/i, path: '/alerts' },
  { label: /^logs$/i, path: '/logs' },
  { label: /^strategy wizard$/i, path: '/strategies/new' },
  { label: /^settings$/i, path: '/settings' },
]

test.describe('sidebar navigation — no errors', () => {
  test('every nav item loads cleanly with no console errors or asset 404s', async ({ page }) => {
    await installApiMocks(page)

    // Collect console errors and asset 404s. We tolerate warnings (React 19
    // often emits prerelease warnings) but NEVER errors.
    const consoleErrors: string[] = []
    page.on('console', (msg: ConsoleMessage) => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text())
      }
    })

    const assetFailures: string[] = []
    page.on('requestfailed', (req: Request) => {
      const url = req.url()
      // Only flag static-asset failures. API calls are mocked; any
      // un-mocked route goes to route.abort in our fixture's 204
      // fallback, which doesn't count as a network failure.
      if (/\.(js|ts|tsx|css|woff2?|svg|png|jpg)(\?.*)?$/.test(url)) {
        assetFailures.push(`${req.failure()?.errorText ?? 'failed'} ${url}`)
      }
    })

    // Also listen for 404 responses on static assets specifically.
    const status404s: string[] = []
    page.on('response', (res) => {
      if (res.status() !== 404) return
      const url = res.url()
      if (/\.(js|ts|tsx|css|woff2?|svg|png|jpg)(\?.*)?$/.test(url)) {
        status404s.push(`404 ${url}`)
      }
    })

    await page.goto('/')

    for (const route of ROUTES) {
      // Clicking the sidebar anchor exercises the in-app click handler and
      // in turn history.pushState; reaching the destination by link click
      // (not direct goto) is what an operator does, so we test that path.
      await page.getByRole('link', { name: route.label }).first().click()
      await expect(page).toHaveURL(new RegExp(route.path.replace(/\//g, '\\/').replace(/\$$/, '') + '$'))
      // Give React a beat to render + commit + paint. Using waitForLoadState
      // instead of sleep so we don't hard-code a number.
      await page.waitForLoadState('networkidle')
    }

    expect(consoleErrors, `Unexpected console errors: ${consoleErrors.join(' | ')}`).toHaveLength(0)
    expect(assetFailures, `Unexpected asset failures: ${assetFailures.join(' | ')}`).toHaveLength(0)
    expect(status404s, `Unexpected 404 responses: ${status404s.join(' | ')}`).toHaveLength(0)
  })
})
