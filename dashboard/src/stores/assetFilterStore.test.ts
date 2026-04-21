import { describe, it, expect, beforeEach } from 'vitest'
import { useAssetFilterStore } from './assetFilterStore'

describe('assetFilterStore', () => {
  beforeEach(() => useAssetFilterStore.setState({ asset: 'all' }))
  it('defaults to all', () => {
    expect(useAssetFilterStore.getState().asset).toBe('all')
  })
  it('setAsset changes value', () => {
    useAssetFilterStore.getState().setAsset('options')
    expect(useAssetFilterStore.getState().asset).toBe('options')
  })
})
