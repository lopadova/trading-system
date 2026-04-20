import { create } from 'zustand'

export type AssetBucket = 'all' | 'systematic' | 'options' | 'other'

interface AssetFilterState {
  asset: AssetBucket
  setAsset: (asset: AssetBucket) => void
}

export const useAssetFilterStore = create<AssetFilterState>((set) => ({
  asset: 'all',
  setAsset: asset => set({ asset }),
}))
