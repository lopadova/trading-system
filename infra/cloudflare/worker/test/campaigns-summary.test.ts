/**
 * Unit tests for /api/campaigns route (summary only — rest out of scope for P3)
 */

import { describe, it, expect } from 'vitest'
import { campaignsSummary } from '../src/routes/campaigns-summary'

describe('campaigns summary', () => {
  it('GET /summary returns counts by state', async () => {
    const res = await campaignsSummary.request('/summary')
    expect(res.status).toBe(200)
    const body = (await res.json()) as Record<string, unknown>
    expect(typeof body.active).toBe('number')
    expect(typeof body.paused).toBe('number')
    expect(typeof body.draft).toBe('number')
    expect(typeof body.detail).toBe('string')
  })
})
