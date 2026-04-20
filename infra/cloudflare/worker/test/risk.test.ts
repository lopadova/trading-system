/**
 * Unit tests for /api/risk route
 */

import { describe, it, expect } from 'vitest'
import { risk } from '../src/routes/risk'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof risk.request>[2]

describe('risk metrics', () => {
  it('GET /metrics returns risk metrics with expected keys', async () => {
    const res = await risk.request('/metrics', AUTH, ENV)
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

  // Auth policy guard: ensure the router rejects unauthenticated requests.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await risk.request('/metrics', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
