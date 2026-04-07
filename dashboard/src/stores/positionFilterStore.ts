import { create } from 'zustand'
import type { PositionFilters, PositionStatus, PositionType } from '../types/position'

interface PositionFilterStore {
  symbol: string | undefined
  strategy: string | undefined
  status: PositionStatus | undefined
  type: PositionType | undefined
  minPnl: number | undefined
  maxPnl: number | undefined
  setSymbol: (symbol: string | undefined) => void
  setStrategy: (strategy: string | undefined) => void
  setStatus: (status: PositionStatus | undefined) => void
  setType: (type: PositionType | undefined) => void
  clearFilters: () => void
  getFilters: () => PositionFilters
}

export const usePositionFilterStore = create<PositionFilterStore>((set, get) => ({
  symbol: undefined,
  strategy: undefined,
  status: undefined,
  type: undefined,
  minPnl: undefined,
  maxPnl: undefined,

  setSymbol: (symbol) => set({ symbol }),
  setStrategy: (strategy) => set({ strategy }),
  setStatus: (status) => set({ status }),
  setType: (type) => set({ type }),
  clearFilters: () =>
    set({
      symbol: undefined,
      strategy: undefined,
      status: undefined,
      type: undefined,
      minPnl: undefined,
      maxPnl: undefined,
    }),
  getFilters: () => {
    const state = get()
    return {
      symbol: state.symbol,
      strategy: state.strategy,
      status: state.status,
      type: state.type,
      minPnl: state.minPnl,
      maxPnl: state.maxPnl,
    }
  },
}))
