/**
 * Unit tests for /api/activity route
 */

import { describe, it, expect } from 'vitest'
import { activity } from '../src/routes/activity'

// Auth helpers — aggregate endpoints now require X-Api-Key matching env.API_KEY
const AUTH = { headers: { 'X-Api-Key': 'test-key' } } as const
const ENV = { API_KEY: 'test-key' } as unknown as Parameters<typeof activity.request>[2]

describe('activity', () => {
  it('GET /recent returns a list of events', async () => {
    const res = await activity.request('/recent', AUTH, ENV)
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      events: {
        id: string
        icon: string
        tone: string
        title: string
        subtitle: string
        timestamp: string
      }[]
    }
    expect(Array.isArray(body.events)).toBe(true)
    expect(body.events.length).toBeGreaterThan(0)
    expect(body.events[0]).toHaveProperty('id')
    expect(body.events[0]).toHaveProperty('icon')
    expect(body.events[0]).toHaveProperty('tone')
    expect(body.events[0]).toHaveProperty('title')
    expect(body.events[0]).toHaveProperty('subtitle')
    expect(body.events[0]).toHaveProperty('timestamp')
  })

  it('honours ?limit=', async () => {
    const res = await activity.request('/recent?limit=2', AUTH, ENV)
    const body = (await res.json()) as { events: unknown[] }
    expect(body.events.length).toBe(2)
  })

  // Auth policy guard: ensure the router rejects unauthenticated requests.
  it('returns 401 when X-Api-Key header is missing', async () => {
    const res = await activity.request('/recent', {}, ENV)
    expect(res.status).toBe(401)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('missing_api_key')
  })
})
