import { create } from 'zustand'
import type { AlertFilters, AlertSeverity, AlertType, AlertStatus } from '../types/alert'

interface AlertFilterStore {
  severity: AlertSeverity | undefined
  type: AlertType | undefined
  status: AlertStatus | undefined
  search: string | undefined
  dateFrom: string | undefined
  dateTo: string | undefined
  setSeverity: (severity: AlertSeverity | undefined) => void
  setType: (type: AlertType | undefined) => void
  setStatus: (status: AlertStatus | undefined) => void
  setSearch: (search: string | undefined) => void
  setDateFrom: (dateFrom: string | undefined) => void
  setDateTo: (dateTo: string | undefined) => void
  clearFilters: () => void
  getFilters: () => AlertFilters
}

export const useAlertFilterStore = create<AlertFilterStore>((set, get) => ({
  severity: undefined,
  type: undefined,
  status: 'active', // Default to showing active alerts
  search: undefined,
  dateFrom: undefined,
  dateTo: undefined,

  setSeverity: (severity) => set({ severity }),
  setType: (type) => set({ type }),
  setStatus: (status) => set({ status }),
  setSearch: (search) => set({ search }),
  setDateFrom: (dateFrom) => set({ dateFrom }),
  setDateTo: (dateTo) => set({ dateTo }),

  clearFilters: () =>
    set({
      severity: undefined,
      type: undefined,
      status: 'active', // Keep default status filter
      search: undefined,
      dateFrom: undefined,
      dateTo: undefined,
    }),

  getFilters: () => {
    const state = get()
    return {
      severity: state.severity,
      type: state.type,
      status: state.status,
      search: state.search,
      dateFrom: state.dateFrom,
      dateTo: state.dateTo,
    }
  },
}))
