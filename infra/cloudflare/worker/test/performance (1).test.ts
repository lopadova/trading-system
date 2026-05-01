/**
 * Unit tests for /api/performance route
 */

import { describe, it, expect } from 'vitest'
import { performance as perfRoute } from '../src/routes/performance'

describe('performance routes', () => {
  it('GET /summary returns 6 horizons for all asset bucket', async () => {
    const res = await perfRoute.request('/summary?asset=all')
    expect(res.status).toBe(200)
    const body = (await res.json()) as Record<string, unknown>
    expect(body.asset).toBe('all')
    expect(typeof body.m).toBe('number')
    expect(typeof body.base).toBe('number')
  })

  it('defaults asset=all when query missing', async () => {
    const res = await perfRoute.request('/summary')
    const body = (await res.json()) as Record<string, unknown>
    expect(body.asset).toBe('all')
  })

  it('GET /series returns 3 arrays with same length', async () => {
    const res = await perfRoute.request('/series?asset=options&range=1M')
    const body = (await res.json()) as Record<string, unknown> & {
      portfolio: number[]
      sp500: number[]
      swda: number[]
      range: string
    }
    expect(body.portfolio.length).toBe(body.sp500.length)
    expect(body.portfolio.length).toBe(body.swda.length)
    expect(body.range).toBe('1M')
  })

  it('rejects invalid asset', async () => {
    const res = await perfRoute.request('/summary?asset=bogus')
    expect(res.status).toBe(400)
  })
})
