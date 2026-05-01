/**
 * Unit tests for /api/positions/breakdown route
 */

import { describe, it, expect } from 'vitest'
import { breakdown } from '../src/routes/breakdown'

describe('positions breakdown', () => {
  it('GET / returns byStrategy and byAsset segments', async () => {
    const res = await breakdown.request('/')
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
})
