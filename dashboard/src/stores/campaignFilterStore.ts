// Campaign filter store using Zustand
// Manages filter state for the campaigns view

import { create } from 'zustand'
import type { CampaignFilters, CampaignStatus, StrategyType } from '../types/campaign'

interface CampaignFilterStore {
  search: string
  status: CampaignStatus | 'all'
  strategyType: StrategyType | 'all'
  underlying: string
  dateFrom: string
  dateTo: string

  setSearch: (search: string) => void
  setStatus: (status: CampaignStatus | 'all') => void
  setStrategyType: (strategyType: StrategyType | 'all') => void
  setUnderlying: (underlying: string) => void
  setDateFrom: (dateFrom: string) => void
  setDateTo: (dateTo: string) => void
  clearFilters: () => void
  getFilters: () => CampaignFilters
}

export const useCampaignFilterStore = create<CampaignFilterStore>((set, get) => ({
  // Default filter state
  search: '',
  status: 'all',
  strategyType: 'all',
  underlying: '',
  dateFrom: '',
  dateTo: '',

  // Individual setters
  setSearch: (search) => set({ search }),
  setStatus: (status) => set({ status }),
  setStrategyType: (strategyType) => set({ strategyType }),
  setUnderlying: (underlying) => set({ underlying }),
  setDateFrom: (dateFrom) => set({ dateFrom }),
  setDateTo: (dateTo) => set({ dateTo }),

  // Clear all filters
  clearFilters: () =>
    set({
      search: '',
      status: 'all',
      strategyType: 'all',
      underlying: '',
      dateFrom: '',
      dateTo: '',
    }),

  // Get filters in API-compatible format
  getFilters: (): CampaignFilters => {
    const state = get()
    return {
      ...(state.search && { search: state.search }),
      ...(state.status !== 'all' && { status: state.status }),
      ...(state.strategyType !== 'all' && { strategyType: state.strategyType }),
      ...(state.underlying && { underlying: state.underlying }),
      ...(state.dateFrom && { dateFrom: state.dateFrom }),
      ...(state.dateTo && { dateTo: state.dateTo }),
    }
  },
}))
