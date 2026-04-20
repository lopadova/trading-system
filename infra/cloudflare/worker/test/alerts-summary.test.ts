/**
 * Unit tests for /api/alerts/summary-24h
 */

import { describe, it, expect } from 'vitest'
import { alerts } from '../src/routes/alerts'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof alerts.request>[2]

describe('alerts summary-24h', () => {
  it('returns counts by severity', async () => {
    const res = await alerts.request('/summary-24h', AUTH, ENV)
    expect(res.status).toBe(200)
    const body = (await res.json()) as Record<string, unknown>
    expect(typeof body.total).toBe('number')
    expect(typeof body.critical).toBe('number')
    expect(typeof body.warning).toBe('number')
    expect(typeof body.info).toBe('number')
    expect(body.total as number).toBeGreaterThanOrEqual(body.critical as number)
  })

  // Auth policy guard: /summary-24h is now behind authMiddleware (consistency
  // with the rest of the aggregate endpoints). Protects against regression.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await alerts.request('/summary-24h', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
