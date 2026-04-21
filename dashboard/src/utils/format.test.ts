import { describe, it, expect } from 'vitest'
import { formatCurrency, formatPercent, formatDelta } from './format'

describe('formatCurrency', () => {
  it('formats USD with 2 decimals', () => {
    expect(formatCurrency(125430.5, 'USD')).toBe('$125,430.50')
  })

  it('formats EUR with integer magnitude', () => {
    expect(formatCurrency(38900, 'EUR', 0)).toBe('€38,900')
  })

  it('always uses US-style thousands separator', () => {
    expect(formatCurrency(1234567.89, 'USD')).toBe('$1,234,567.89')
  })
})

describe('formatPercent', () => {
  it('signs positive values', () => {
    expect(formatPercent(14.3)).toBe('+14.30%')
  })

  it('signs negative values', () => {
    expect(formatPercent(-7.92)).toBe('-7.92%')
  })
})

describe('formatDelta', () => {
  it('renders arrow + amount + percent for positive', () => {
    expect(formatDelta(2340.8, 1.9, 'USD')).toBe('↑ +$2,340.80 (+1.90%)')
  })
})
