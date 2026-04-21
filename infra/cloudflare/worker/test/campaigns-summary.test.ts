/**
 * Unit tests for /api/campaigns route (summary only — rest out of scope for P3)
 */

import { describe, it, expect } from 'vitest'
import { campaignsSummary } from '../src/routes/campaigns-summary'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof campaignsSummary.request>[2]

describe('campaigns summary', () => {
  it('GET /summary returns counts by state', async () => {
    const res = await campaignsSummary.request('/summary', AUTH, ENV)
    expect(res.status).toBe(200)
    const body = (await res.json()) as Record<string, unknown>
    expect(typeof body.active).toBe('number')
    expect(typeof body.paused).toBe('number')
    expect(typeof body.draft).toBe('number')
    expect(typeof body.detail).toBe('string')
  })

  // Auth policy guard: ensure the router rejects unauthenticated requests.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await campaignsSummary.request('/summary', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
