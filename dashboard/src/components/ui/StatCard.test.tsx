import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { StatCard } from './StatCard'
import { DollarSign } from 'lucide-react'

describe('StatCard', () => {
  it('renders label, value, delta', () => {
    render(<StatCard label="Account Value" value="$125,430.50" delta="↑ +$2,340.80" deltaTone="green" />)
    expect(screen.getByText('Account Value')).toBeInTheDocument()
    expect(screen.getByText('$125,430.50')).toBeInTheDocument()
    expect(screen.getByText('↑ +$2,340.80')).toBeInTheDocument()
  })
  it('renders icon', () => {
    render(<StatCard label="x" value="1" icon={DollarSign} data-testid="s" />)
    expect(screen.getByTestId('s').querySelector('svg')).toBeTruthy()
  })
  it('renders status slot instead of value when given', () => {
    render(<StatCard label="x" value="" status={{ tone: 'green', label: 'OPERATIONAL' }} />)
    expect(screen.getByText('OPERATIONAL')).toBeInTheDocument()
  })
})
