import { describe, it, expect } from 'vitest'
import { normalizeYAxis, generateDateLabels } from './chart-utils'

describe('normalizeYAxis', () => {
  it('returns min/max with padding', () => {
    const r = normalizeYAxis([100, 110, 120])
    expect(r.min).toBeLessThan(100)
    expect(r.max).toBeGreaterThan(120)
  })

  it('handles empty arrays', () => {
    const r = normalizeYAxis([])
    expect(r.min).toBeLessThan(r.max)
  })
})

describe('generateDateLabels', () => {
  it('returns the requested number of labels', () => {
    const labels = generateDateLabels(new Date('2026-04-20'), 30, 5)
    expect(labels.length).toBe(5)
  })
})
