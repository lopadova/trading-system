/**
 * Unit tests for /api/system route
 */

import { describe, it, expect } from 'vitest'
import { systemMetrics } from '../src/routes/system-metrics'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof systemMetrics.request>[2]

describe('system metrics', () => {
  it('GET /metrics returns cpu/ram/network arrays and disk info', async () => {
    const res = await systemMetrics.request('/metrics', AUTH, ENV)
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      cpu: number[]
      ram: number[]
      network: number[]
      diskUsedPct: number
      diskFreeGb: number
      diskTotalGb: number
      asOf: string
    }
    expect(Array.isArray(body.cpu)).toBe(true)
    expect(body.cpu.length).toBeGreaterThan(0)
    expect(Array.isArray(body.ram)).toBe(true)
    expect(Array.isArray(body.network)).toBe(true)
    expect(typeof body.diskUsedPct).toBe('number')
    expect(typeof body.diskFreeGb).toBe('number')
    expect(typeof body.diskTotalGb).toBe('number')
    expect(typeof body.asOf).toBe('string')
  })

  // Auth policy guard: ensure the router rejects unauthenticated requests.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await systemMetrics.request('/metrics', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
