import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { Button } from './Button'
import { Plus } from 'lucide-react'

describe('Button', () => {
  it('renders children', () => {
    render(<Button>Apply</Button>)
    expect(screen.getByText('Apply')).toBeInTheDocument()
  })

  it('renders icon when provided', () => {
    render(<Button icon={Plus} data-testid="b">New</Button>)
    expect(screen.getByTestId('b').querySelector('svg')).toBeTruthy()
  })

  it('calls onClick', () => {
    const fn = vi.fn()
    render(<Button onClick={fn}>go</Button>)
    fireEvent.click(screen.getByText('go'))
    expect(fn).toHaveBeenCalledOnce()
  })

  it('disabled=true blocks clicks', () => {
    const fn = vi.fn()
    render(<Button onClick={fn} disabled>no</Button>)
    fireEvent.click(screen.getByText('no'))
    expect(fn).not.toHaveBeenCalled()
  })

  it('loading shows spinner', () => {
    render(<Button loading data-testid="b">x</Button>)
    expect(screen.getByTestId('b').querySelector('[data-spinner]')).toBeTruthy()
  })
})
