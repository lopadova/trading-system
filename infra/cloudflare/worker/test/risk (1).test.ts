/**
 * Unit tests for /api/risk route
 */

import { describe, it, expect } from 'vitest'
import { risk } from '../src/routes/risk'

describe('risk metrics', () => {
  it('GET /metrics returns risk metrics with expected keys', async () => {
    const res = await risk.request('/metrics')
    expect(res.status).toBe(200)
    const body = (await res.json()) as Record<string, unknown>
    expect(body).toHaveProperty('vix')
    expect(body).toHaveProperty('vix1d')
    expect(typeof body.delta).toBe('number')
    expect(typeof body.theta).toBe('number')
    expect(typeof body.vega).toBe('number')
    expect(typeof body.buyingPower).toBe('number')
    expect(typeof body.marginUsedPct).toBe('number')
  })
})
