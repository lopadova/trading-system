/**
 * Unit tests for /api/alerts/summary-24h
 */

import { describe, it, expect } from 'vitest'
import { alerts } from '../src/routes/alerts'

describe('alerts summary-24h', () => {
  it('returns counts by severity', async () => {
    const res = await alerts.request('/summary-24h')
    expect(res.status).toBe(200)
    const body = (await res.json()) as Record<string, unknown>
    expect(typeof body.total).toBe('number')
    expect(typeof body.critical).toBe('number')
    expect(typeof body.warning).toBe('number')
    expect(typeof body.info).toBe('number')
    expect(body.total as number).toBeGreaterThanOrEqual(body.critical as number)
  })
})
