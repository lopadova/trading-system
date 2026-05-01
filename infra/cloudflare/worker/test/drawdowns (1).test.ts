/**
 * Unit tests for /api/drawdowns route
 */

import { describe, it, expect } from 'vitest'
import { drawdowns } from '../src/routes/drawdowns'

describe('drawdowns', () => {
  it('returns series + worst list', async () => {
    const res = await drawdowns.request('/?asset=all&range=10Y')
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      portfolioSeries: number[]
      sp500Series: number[]
      worst: { depthPct: number }[]
    }
    expect(body.portfolioSeries.length).toBeGreaterThan(0)
    expect(body.sp500Series.length).toBe(body.portfolioSeries.length)
    expect(Array.isArray(body.worst)).toBe(true)
    expect(body.worst[0]).toHaveProperty('depthPct')
  })
})
