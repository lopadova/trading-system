import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { Donut } from './Donut'

describe('Donut', () => {
  it('renders an SVG with the expected segment count', () => {
    render(
      <Donut
        segments={[
          { label: 'A', value: 10, color: '#f00' },
          { label: 'B', value: 20, color: '#0f0' },
        ]}
        centerLabel="€30"
        centerSub="total"
        data-testid="d"
      />
    )
    const svg = screen.getByTestId('d').querySelector('svg')
    expect(svg?.querySelectorAll('circle').length).toBe(3) // bg + 2 segments
    expect(screen.getByText('€30')).toBeInTheDocument()
    expect(screen.getByText('total')).toBeInTheDocument()
  })
})
