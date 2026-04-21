import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { SegmentedControl } from './SegmentedControl'

describe('SegmentedControl', () => {
  it('renders options and marks selected', () => {
    render(<SegmentedControl<string> value="a" onChange={() => {}} options={[{ value: 'a', label: 'A' }, { value: 'b', label: 'B' }]} />)
    expect(screen.getByRole('button', { name: 'A' }).getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('button', { name: 'B' }).getAttribute('aria-pressed')).toBe('false')
  })
  it('calls onChange on click', () => {
    const fn = vi.fn()
    render(<SegmentedControl<string> value="a" onChange={fn} options={[{ value: 'a', label: 'A' }, { value: 'b', label: 'B' }]} />)
    fireEvent.click(screen.getByRole('button', { name: 'B' }))
    expect(fn).toHaveBeenCalledWith('b')
  })
  it('does not call onChange when locked', () => {
    const fn = vi.fn()
    render(<SegmentedControl<string> value="a" onChange={fn} options={[{ value: 'a', label: 'A' }, { value: 'b', label: 'B', locked: true }]} />)
    fireEvent.click(screen.getByRole('button', { name: 'B' }))
    expect(fn).not.toHaveBeenCalled()
  })
})
