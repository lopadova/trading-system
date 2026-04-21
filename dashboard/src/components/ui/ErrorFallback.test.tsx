import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ErrorFallback } from './ErrorFallback'

describe('ErrorFallback', () => {
  // window.location.reload is read-only on the real object; we stash + restore.
  const originalLocation = window.location

  beforeEach(() => {
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { ...originalLocation, reload: vi.fn() }
    })
  })

  afterEach(() => {
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: originalLocation
    })
  })

  it('renders title and message', () => {
    render(<ErrorFallback />)
    expect(screen.getByText('Something went wrong')).toBeInTheDocument()
    expect(screen.getByText(/unexpected error/i)).toBeInTheDocument()
  })

  it('renders Reload button', () => {
    render(<ErrorFallback />)
    expect(screen.getByRole('button', { name: /reload/i })).toBeInTheDocument()
  })

  it('calls resetError and reloads on Reload click', () => {
    const resetError = vi.fn()
    render(<ErrorFallback resetError={resetError} />)
    fireEvent.click(screen.getByRole('button', { name: /reload/i }))
    expect(resetError).toHaveBeenCalledOnce()
    expect(window.location.reload).toHaveBeenCalledOnce()
  })

  it('works without resetError prop (only reload is called)', () => {
    render(<ErrorFallback />)
    fireEvent.click(screen.getByRole('button', { name: /reload/i }))
    expect(window.location.reload).toHaveBeenCalledOnce()
  })
})
