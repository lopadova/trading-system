import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, beforeEach } from 'vitest'
import { AssetFilter } from './AssetFilter'
import { useAssetFilterStore } from '../../stores/assetFilterStore'

describe('AssetFilter', () => {
  beforeEach(() => useAssetFilterStore.setState({ asset: 'all' }))

  it('renders 4 chips', () => {
    render(<AssetFilter />)
    expect(screen.getByRole('button', { name: /all/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /systematic/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /options/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /other/i })).toBeInTheDocument()
  })

  it('sets asset on click', () => {
    render(<AssetFilter />)
    fireEvent.click(screen.getByRole('button', { name: /options/i }))
    expect(useAssetFilterStore.getState().asset).toBe('options')
  })
})
