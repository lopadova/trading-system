/**
 * Unit tests for /api/system route
 */

import { describe, it, expect } from 'vitest'
import { systemMetrics } from '../src/routes/system-metrics'

describe('system metrics', () => {
  it('GET /metrics returns cpu/ram/network arrays and disk info', async () => {
    const res = await systemMetrics.request('/metrics')
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
})
