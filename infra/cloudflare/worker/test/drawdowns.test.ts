/**
 * Unit tests for /api/drawdowns route
 */

import { describe, it, expect } from 'vitest'
import { drawdowns } from '../src/routes/drawdowns'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof drawdowns.request>[2]

describe('drawdowns', () => {
  it('returns series + worst list', async () => {
    const res = await drawdowns.request('/?asset=all&range=10Y', AUTH, ENV)
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

  // Auth policy guard: ensure the router rejects unauthenticated requests.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await drawdowns.request('/?asset=all&range=10Y', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
