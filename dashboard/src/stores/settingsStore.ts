import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface DashboardSettings {
  // API Configuration
  apiEndpoint: string
  apiKey: string

  // Auto-refresh intervals (in milliseconds)
  refreshIntervalPositions: number
  refreshIntervalHealth: number
  refreshIntervalAlerts: number
  refreshIntervalIvts: number

  // Alert preferences
  enableDesktopNotifications: boolean
  enableSoundAlerts: boolean
  alertThreshold: 'all' | 'warning' | 'critical'

  // Data export options
  exportFormat: 'json' | 'csv'
  includeTimestamps: boolean
}

interface SettingsStore extends DashboardSettings {
  // Actions
  updateSettings: (settings: Partial<DashboardSettings>) => void
  resetSettings: () => void
  validateSettings: () => { valid: boolean; errors: string[] }
}

const defaultSettings: DashboardSettings = {
  apiEndpoint: 'http://localhost:8787',
  apiKey: '',
  refreshIntervalPositions: 30000, // 30s
  refreshIntervalHealth: 15000, // 15s
  refreshIntervalAlerts: 10000, // 10s
  refreshIntervalIvts: 60000, // 60s
  enableDesktopNotifications: false,
  enableSoundAlerts: false,
  alertThreshold: 'warning',
  exportFormat: 'json',
  includeTimestamps: true,
}

export const useSettingsStore = create<SettingsStore>()(
  persist(
    (set, get) => ({
      ...defaultSettings,

      updateSettings: (settings) => {
        set((state) => ({ ...state, ...settings }))
      },

      resetSettings: () => {
        set(defaultSettings)
      },

      validateSettings: () => {
        const state = get()
        const errors: string[] = []

        // Validate API endpoint
        if (!state.apiEndpoint.trim()) {
          errors.push('API endpoint is required')
        } else {
          try {
            new URL(state.apiEndpoint)
          } catch {
            errors.push('API endpoint must be a valid URL')
          }
        }

        // Validate refresh intervals (must be > 0)
        if (state.refreshIntervalPositions <= 0) {
          errors.push('Positions refresh interval must be greater than 0')
        }
        if (state.refreshIntervalHealth <= 0) {
          errors.push('Health refresh interval must be greater than 0')
        }
        if (state.refreshIntervalAlerts <= 0) {
          errors.push('Alerts refresh interval must be greater than 0')
        }
        if (state.refreshIntervalIvts <= 0) {
          errors.push('IVTS refresh interval must be greater than 0')
        }

        return { valid: errors.length === 0, errors }
      },
    }),
    {
      name: 'trading-settings',
      partialize: (state) => {
        // Exclude functions from persisted state (action names prefixed with _
        // to satisfy @typescript-eslint/no-unused-vars — they exist only to be
        // destructured away from the persisted payload)
        const {
          updateSettings: _updateSettings,
          resetSettings: _resetSettings,
          validateSettings: _validateSettings,
          ...settings
        } = state
        return settings
      },
    }
  )
)
