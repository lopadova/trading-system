import { test, expect } from '@playwright/test'
import { installApiMocks } from './fixtures/api-mock'

/**
 * Critical journey #1 — Asset-filter flip persists across navigation.
 *
 * Zustand's asset filter is an in-memory store (no localStorage persistence
 * by design — filter is ephemeral per session). This test verifies the
 * store DOES survive within-session navigation via the Header/Sidebar
 * anchors, which is what operators rely on when flipping between
 * Overview ↔ Positions without losing their current bucket.
 */
test.describe('asset-filter persistence', () => {
  test.beforeEach(async ({ page }) => {
    await installApiMocks(page)
    await page.goto('/')
  })

  test('flipping to Options persists when navigating to Positions and back', async ({ page }) => {
    // Locate the Options chip by its visible label — the AssetFilter
    // component renders an aria-pressed toggle button.
    const optionsChip = page.getByRole('button', { name: /options/i, pressed: false })
    await expect(optionsChip).toBeVisible()
    await optionsChip.click()

    // Once clicked, aria-pressed should flip to true — confirming the store
    // update landed before we navigate.
    await expect(
      page.getByRole('button', { name: /^options$/i }),
    ).toHaveAttribute('aria-pressed', 'true')

    // Navigate via the sidebar anchor. The App uses history.pushState for
    // in-app links so the React tree stays mounted and Zustand state survives.
    await page.getByRole('link', { name: /^positions$/i }).click()
    await expect(page).toHaveURL(/\/positions$/)

    // Back to Overview via the sidebar.
    await page.getByRole('link', { name: /^overview$/i }).click()
    await expect(page).toHaveURL(/\/$/)

    // The Options chip should still be the pressed one.
    await expect(
      page.getByRole('button', { name: /^options$/i }),
    ).toHaveAttribute('aria-pressed', 'true')
    await expect(
      page.getByRole('button', { name: /^all assets$/i }),
    ).toHaveAttribute('aria-pressed', 'false')
  })
})
