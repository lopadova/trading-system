/**
 * Unit tests for /api/breakdown route
 * (Moved out of /api/positions namespace to avoid auth-middleware prefix clash.)
 */

import { describe, it, expect } from 'vitest'
import { breakdown } from '../src/routes/breakdown'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof breakdown.request>[2]

describe('positions breakdown', () => {
  it('GET / returns byStrategy and byAsset segments', async () => {
    const res = await breakdown.request('/', AUTH, ENV)
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      byStrategy: { label: string; value: number; color: string }[]
      byAsset: { label: string; value: number; color: string }[]
    }
    expect(Array.isArray(body.byStrategy)).toBe(true)
    expect(body.byStrategy.length).toBeGreaterThan(0)
    expect(body.byStrategy[0]).toHaveProperty('label')
    expect(body.byStrategy[0]).toHaveProperty('value')
    expect(body.byStrategy[0]).toHaveProperty('color')
    expect(Array.isArray(body.byAsset)).toBe(true)
    expect(body.byAsset.length).toBeGreaterThan(0)
  })

  // Auth policy guard: ensure the router rejects unauthenticated requests.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await breakdown.request('/', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
