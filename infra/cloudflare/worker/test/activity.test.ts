/**
 * Unit tests for /api/activity route
 */

import { describe, it, expect } from 'vitest'
import { activity } from '../src/routes/activity'

describe('activity', () => {
  it('GET /recent returns a list of events', async () => {
    const res = await activity.request('/recent')
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
    const res = await activity.request('/recent?limit=2')
    const body = (await res.json()) as { events: unknown[] }
    expect(body.events.length).toBe(2)
  })
})
