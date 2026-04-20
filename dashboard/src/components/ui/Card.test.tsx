import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { Card, CardContent } from './Card'

describe('Card', () => {
  it('renders children inside a bordered surface', () => {
    render(<Card data-testid="card">hello</Card>)
    const card = screen.getByTestId('card')
    expect(card).toHaveTextContent('hello')
    expect(card.className).toMatch(/bg-surface/)
    expect(card.className).toMatch(/border/)
    expect(card.className).toMatch(/rounded-card/)
  })

  it('accepts padding=0 for flush layouts', () => {
    render(<Card data-testid="card" padding={0}>x</Card>)
    expect(screen.getByTestId('card').className).toMatch(/p-0/)
  })

  it('CardContent renders inside a padded block', () => {
    render(<CardContent data-testid="cc">y</CardContent>)
    expect(screen.getByTestId('cc')).toHaveTextContent('y')
  })
})
