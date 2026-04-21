import { test, expect } from '@playwright/test'
import { installApiMocks } from './fixtures/api-mock'

/**
 * Critical journey #2 — Theme toggle persists after reload.
 *
 * themeStore writes the chosen theme to localStorage under key 'ts-theme'
 * AND applies it as a data-theme attribute on <html>. Operators expect
 * their choice to survive a browser refresh so they're not stuck in a
 * blinding-light terminal at 3AM.
 */
test.describe('theme persistence', () => {
  test('switching to light mode survives page reload', async ({ page }) => {
    await installApiMocks(page)
    await page.goto('/')

    // Default theme is dark (themeStore.readInitial fallback).
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')

    // Header theme toggle — aria-label flips between "Switch to light theme"
    // and "Switch to dark theme". Dark-mode page shows the Sun icon with the
    // "Switch to light theme" label.
    const toggle = page.getByRole('button', { name: /switch to light theme/i })
    await toggle.click()

    // Immediate state check — both the attribute and localStorage.
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
    const stored = await page.evaluate(() => localStorage.getItem('ts-theme'))
    expect(stored).toBe('light')

    // Full reload. A soft navigation isn't enough here — we want to prove
    // the store boots up from localStorage on a cold start.
    await page.reload()

    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
    // And the toggle button is now the "Switch to dark" flavor.
    await expect(page.getByRole('button', { name: /switch to dark theme/i })).toBeVisible()
  })
})
