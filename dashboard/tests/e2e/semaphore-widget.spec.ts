import { test, expect, type Route } from '@playwright/test'
import { installApiMocks } from './fixtures/api-mock'

/**
 * Critical journey #4 — Semaphore widget renders with the 3 wired colours
 * (green / orange / red) driven by the API response.
 *
 * We run three variants in a single spec: for each status we override the
 * /api/risk/semaphore response ONLY and verify the widget reflects it. The
 * remaining mocks come from the shared fixture.
 */
test.describe('semaphore widget colours', () => {
  const STATUSES: Array<{ status: 'green' | 'orange' | 'red'; score: number; label: RegExp }> = [
    { status: 'green', score: 22, label: /operative/i },
    { status: 'orange', score: 55, label: /caution/i },
    { status: 'red', score: 90, label: /halt/i },
  ]

  for (const s of STATUSES) {
    test(`reflects ${s.status} status from API`, async ({ page }) => {
      await installApiMocks(page)
      // Override the semaphore endpoint with this variant's payload. This
      // runs AFTER the broad mock but Playwright matches routes in the
      // order handlers were registered — so we route-unregister and
      // re-register specifically.
      await page.route(/\/risk\/semaphore/, async (route: Route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            score: s.score,
            status: s.status,
            regime: 'BULLISH',
            asOf: '2026-04-20T14:30:00Z',
            exchangeTime: '14:30:00 NYT',
            spx: { price: 5410.25, change: 12.18, changePct: 0.226 },
            vix: { price: 14.22, change: -0.34, changePct: -2.335 },
            indicators: [
              { id: 'regime', label: 'Long-term regime', status: s.status, value: 'BULLISH', detail: 'n/a' },
              { id: 'vix_level', label: 'VIX level', status: s.status, value: 14.22, detail: 'n/a' },
              { id: 'vix_rolling_yield', label: 'VIX rolling yield', status: s.status, value: 0.92, detail: 'n/a' },
              { id: 'ivts', label: 'IV term structure', status: s.status, value: 1.05, detail: 'n/a' },
              { id: 'overall', label: 'Overall', status: s.status, value: s.score, detail: 'n/a' },
            ],
          }),
        })
      })

      await page.goto('/')

      // The card title is stable regardless of status.
      await expect(page.getByRole('heading', { name: /options trading semaphore/i })).toBeVisible()

      // The BIG status label changes per status — green → OPERATIVE,
      // orange → CAUTION, red → HALT.
      await expect(page.getByText(s.label)).toBeVisible()

      // The accessible description on the SVG gauge encodes the numeric score
      // and status into one string: "Risk score N of 100, status X". Asserting
      // on it is more stable than sniffing for an exact hex colour.
      const gauge = page.getByRole('img', {
        name: new RegExp(`risk score ${s.score} of 100, status ${s.status}`, 'i'),
      })
      await expect(gauge).toBeVisible()
    })
  }
})
