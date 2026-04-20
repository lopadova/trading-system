import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { Badge } from './Badge'

describe('Badge', () => {
  it('renders green tone', () => {
    render(<Badge tone="green">OPERATIONAL</Badge>)
    const el = screen.getByText('OPERATIONAL')
    expect(el.className).toMatch(/text-\[var\(--green\)\]|text-up/)
  })

  it('renders pulse dot when pulse=true', () => {
    render(<Badge tone="green" pulse data-testid="b">LIVE</Badge>)
    const el = screen.getByTestId('b')
    expect(el.querySelector('.pulse-dot')).toBeTruthy()
  })

  it('small size has compact padding', () => {
    render(<Badge tone="muted" size="sm" data-testid="b">sm</Badge>)
    expect(screen.getByTestId('b').className).toMatch(/px-2|text-\[10\.5px\]|text-xs/)
  })
})
