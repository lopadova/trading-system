import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { Spinner } from './Spinner'
import { Skeleton } from './Skeleton'
import { FilterOverlay } from './FilterOverlay'

describe('Spinner', () => {
  it('renders SVG', () => {
    render(<Spinner data-testid="s" />)
    expect(screen.getByTestId('s').tagName.toLowerCase()).toBe('svg')
  })
})
describe('Skeleton', () => {
  it('renders a shimmer element', () => {
    render(<Skeleton data-testid="k" />)
    expect(screen.getByTestId('k').className).toMatch(/animate-|shimmer/)
  })
})
describe('FilterOverlay', () => {
  it('renders label when visible', () => {
    render(<FilterOverlay visible label="Loading…" />)
    expect(screen.getByText('Loading…')).toBeInTheDocument()
  })
  it('renders nothing when not visible', () => {
    const { container } = render(<FilterOverlay visible={false} label="x" />)
    expect(container.firstChild).toBeNull()
  })
})
