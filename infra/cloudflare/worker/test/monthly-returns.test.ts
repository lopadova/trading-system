/**
 * Unit tests for /api/monthly-returns route
 */

import { describe, it, expect } from 'vitest'
import { monthlyReturns } from '../src/routes/monthly-returns'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof monthlyReturns.request>[2]

describe('monthly-returns', () => {
  it('returns full matrix with 12 entries per year', async () => {
    const res = await monthlyReturns.request('/?asset=all', AUTH, ENV)
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

  // Auth policy guard: ensure the router rejects unauthenticated requests.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await monthlyReturns.request('/?asset=all', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
