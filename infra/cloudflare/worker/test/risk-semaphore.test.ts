/**
 * Unit tests for /api/risk/semaphore route — Options Trading Semaphore indicator.
 */

import { describe, it, expect } from 'vitest'
import { risk, computeSemaphore } from '../src/routes/risk'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof risk.request>[2]

describe('risk semaphore', () => {
  it('returns semaphore payload with 5 indicators', async () => {
    const res = await risk.request('/semaphore', AUTH, ENV)
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      score: number
      status: string
      indicators: { id: string; label: string; status: string; value: number | string; detail: string }[]
      asOf: string
      spx: { price: number; change: number; changePct: number }
      vix: { price: number; change: number; changePct: number }
      regime: string
      exchangeTime: string
    }
    expect(typeof body.score).toBe('number')
    expect(['green', 'orange', 'red']).toContain(body.status)
    expect(Array.isArray(body.indicators)).toBe(true)
    expect(body.indicators.length).toBe(5)
    expect(typeof body.asOf).toBe('string')

    // Expected indicator ids in order
    const ids = body.indicators.map(i => i.id)
    expect(ids).toEqual(['regime', 'vix_level', 'vix_rolling_yield', 'ivts', 'overall'])

    // Each indicator has the required shape
    for (const ind of body.indicators) {
      expect(ind).toHaveProperty('label')
      expect(['green', 'orange', 'red']).toContain(ind.status)
      expect(ind).toHaveProperty('value')
      expect(typeof ind.detail).toBe('string')
    }

    // Reference-layout extras: SPX / VIX quotes, regime, exchange time.
    expect(typeof body.spx.price).toBe('number')
    expect(typeof body.spx.change).toBe('number')
    expect(typeof body.spx.changePct).toBe('number')
    expect(typeof body.vix.price).toBe('number')
    expect(typeof body.vix.change).toBe('number')
    expect(typeof body.vix.changePct).toBe('number')
    expect(['BULLISH', 'BEARISH']).toContain(body.regime)
    expect(typeof body.exchangeTime).toBe('string')
    expect(body.exchangeTime.length).toBeGreaterThan(0)
  })

  it('score is clamped to 0..100', () => {
    const payload = computeSemaphore()
    expect(payload.score).toBeGreaterThanOrEqual(0)
    expect(payload.score).toBeLessThanOrEqual(100)
  })

  it('overall indicator status matches top-level status', () => {
    const payload = computeSemaphore()
    const overall = payload.indicators.find(i => i.id === 'overall')
    expect(overall).toBeDefined()
    expect(overall?.status).toBe(payload.status)
  })

  // Auth policy guard: ensure the router rejects unauthenticated requests.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await risk.request('/semaphore', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
