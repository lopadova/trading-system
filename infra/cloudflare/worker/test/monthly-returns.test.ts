/**
 * Unit tests for /api/monthly-returns route
 */

import { describe, it, expect } from 'vitest'
import { monthlyReturns } from '../src/routes/monthly-returns'

describe('monthly-returns', () => {
  it('returns full matrix with 12 entries per year', async () => {
    const res = await monthlyReturns.request('/?asset=all')
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      years: Record<string, (number | null)[]>
      totals: Record<string, number>
    }
    expect(Object.keys(body.years).length).toBeGreaterThan(0)
    for (const year of Object.keys(body.years)) {
      expect(body.years[year].length).toBe(12)
    }
    const firstYear = Object.keys(body.years)[0]
    expect(typeof body.totals[firstYear]).toBe('number')
  })
})
