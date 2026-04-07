/**
 * Wizard Store — Global Zustand Store for Strategy Wizard
 *
 * Manages wizard state, navigation, validation, and publish flow.
 * Uses Zustand with Immer middleware for safe immutable mutations.
 */

import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import { set as lodashSet } from 'lodash-es'
import type { StrategyDraft } from '../types/sdf-v1'
import type { ValidationError } from '../utils/sdf-validator'
import { validateStep, validateAll } from '../utils/sdf-validator'
import { createDefaultStrategy } from '../utils/sdf-defaults'

// ============================================================================
// TYPES
// ============================================================================

export type WizardMode = 'new' | 'import' | 'edit' | 'convert'
export type PublishStatus = 'idle' | 'validating' | 'publishing' | 'success' | 'error'

export interface ConversionResult {
  success: boolean
  strategy?: StrategyDraft
  errors?: string[]
  warnings?: string[]
}

export interface WizardState {
  // Navigation
  currentStep: number
  totalSteps: number
  visitedSteps: number[]
  mode: WizardMode

  // Data
  draft: StrategyDraft
  originalJson: string | null
  isDirty: boolean

  // Validation
  stepErrors: Record<number, ValidationError[]>
  globalErrors: ValidationError[]

  // EL Conversion state
  elCode: string
  conversionResult: ConversionResult | null
  conversionLoading: boolean
  conversionError: string | null

  // Publish state
  publishStatus: PublishStatus
  publishedStrategyId: string | null
  publishError: string | null

  // Actions - Navigation
  goToStep: (step: number) => void
  nextStep: () => boolean
  prevStep: () => void

  // Actions - Data
  setField: (path: string, value: unknown) => void
  initFromJson: (json: string) => { ok: boolean; errors: string[] }
  validateAllSteps: () => boolean

  // Actions - EL Conversion
  setElCode: (code: string) => void
  convertElToSdf: () => Promise<void>
  applyConversionResult: (partialStrategy: Partial<StrategyDraft>) => void

  // Actions - Publish
  publish: () => Promise<void>
  resetWizard: () => void
}

// ============================================================================
// STORE
// ============================================================================

export const useWizardStore = create<WizardState>()(
  immer((set, get) => ({
    // Initial state
    currentStep: 1,
    totalSteps: 10,
    visitedSteps: [1],
    mode: 'new',
    draft: createDefaultStrategy(),
    originalJson: null,
    stepErrors: {},
    globalErrors: [],
    isDirty: false,

    // EL Conversion initial state
    elCode: '',
    conversionResult: null,
    conversionLoading: false,
    conversionError: null,

    // Publish initial state
    publishStatus: 'idle',
    publishedStrategyId: null,
    publishError: null,

    // ========================================================================
    // NAVIGATION ACTIONS
    // ========================================================================

    goToStep: (step: number) =>
      set((state) => {
        const isVisited = state.visitedSteps.includes(step)
        const isNext = step === state.currentStep + 1
        const isValid = step >= 1 && step <= state.totalSteps

        // Only navigate if step is visited OR is the immediate next step
        if ((isVisited || isNext) && isValid) {
          state.currentStep = step
          if (!isVisited) {
            state.visitedSteps.push(step)
          }
        }
      }),

    nextStep: () => {
      const { currentStep, draft } = get()

      // Validate current step before advancing
      const errors = validateStep(currentStep, draft)

      set((state) => {
        state.stepErrors[currentStep] = errors
      })

      // Block advancement if there are validation errors
      const hasErrors = errors.some((e) => e.severity === 'error')
      if (hasErrors) {
        return false
      }

      // Advance to next step
      get().goToStep(currentStep + 1)
      return true
    },

    prevStep: () => {
      const { currentStep } = get()
      if (currentStep > 1) {
        get().goToStep(currentStep - 1)
      }
    },

    // ========================================================================
    // DATA ACTIONS
    // ========================================================================

    setField: (path: string, value: unknown) =>
      set((state) => {
        // Use lodash set to handle deeply nested paths
        lodashSet(state.draft as Record<string, unknown>, path, value)
        state.isDirty = true

        // Re-validate current step after field change
        const errors = validateStep(state.currentStep, state.draft)
        state.stepErrors[state.currentStep] = errors
      }),

    initFromJson: (json: string) => {
      try {
        const parsed = JSON.parse(json) as StrategyDraft

        // Basic validation: strategy_id is required
        if (!parsed.strategy_id || typeof parsed.strategy_id !== 'string') {
          return { ok: false, errors: ['strategy_id mancante o non valido'] }
        }

        // Merge with defaults to ensure all required fields exist
        set((state) => {
          state.draft = { ...createDefaultStrategy(), ...parsed }
          state.mode = 'import'
          state.originalJson = json
          state.isDirty = false
          // Mark all steps as visited when importing
          state.visitedSteps = Array.from({ length: state.totalSteps }, (_, i) => i + 1)
          state.currentStep = 1
        })

        return { ok: true, errors: [] }
      } catch (e) {
        const error = e as Error
        return { ok: false, errors: [`JSON non valido: ${error.message}`] }
      }
    },

    validateAllSteps: () => {
      const { draft } = get()
      const result = validateAll(draft)

      set((state) => {
        state.globalErrors = result.errors
      })

      return result.valid
    },

    // ========================================================================
    // EL CONVERSION ACTIONS
    // ========================================================================

    setElCode: (code: string) =>
      set((state) => {
        state.elCode = code
      }),

    convertElToSdf: async () => {
      const { elCode } = get()

      if (!elCode.trim()) {
        set((state) => {
          state.conversionError = 'Codice EasyLanguage vuoto'
        })
        return
      }

      set((state) => {
        state.conversionLoading = true
        state.conversionError = null
        state.conversionResult = null
      })

      try {
        const apiKey = import.meta.env.VITE_API_KEY as string | undefined
        if (!apiKey) {
          throw new Error('VITE_API_KEY non configurata')
        }

        const res = await fetch('/api/v1/strategies/convert-el', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-Api-Key': apiKey,
          },
          body: JSON.stringify({ el_code: elCode }),
        })

        if (!res.ok) {
          const err = await res.json()
          throw new Error(err.error || `HTTP ${res.status}`)
        }

        const result = (await res.json()) as ConversionResult

        set((state) => {
          state.conversionResult = result
          state.conversionLoading = false

          // If conversion successful, merge into draft
          if (result.success && result.strategy) {
            state.draft = { ...createDefaultStrategy(), ...result.strategy }
            state.mode = 'convert'
            state.isDirty = true
          }
        })
      } catch (e) {
        const error = e as Error
        set((state) => {
          state.conversionLoading = false
          state.conversionError = error.message
        })
      }
    },

    applyConversionResult: (partialStrategy: Partial<StrategyDraft>) => {
      set((state) => {
        // Merge conversion result into draft, preserving defaults
        state.draft = { ...createDefaultStrategy(), ...partialStrategy }
        state.mode = 'convert'
        state.isDirty = true
        // Mark all steps as visited when applying conversion
        state.visitedSteps = Array.from({ length: state.totalSteps }, (_, i) => i + 1)
        state.currentStep = 1
      })
    },

    // ========================================================================
    // PUBLISH ACTIONS
    // ========================================================================

    publish: async () => {
      // Step 1: Validate all steps
      set((state) => {
        state.publishStatus = 'validating'
      })

      const isValid = get().validateAllSteps()
      if (!isValid) {
        set((state) => {
          state.publishStatus = 'error'
          state.publishError = 'Validazione fallita: correggi gli errori prima di pubblicare'
        })
        return
      }

      // Step 2: Publish to API
      set((state) => {
        state.publishStatus = 'publishing'
      })

      try {
        const apiKey = import.meta.env.VITE_API_KEY as string | undefined
        if (!apiKey) {
          throw new Error('VITE_API_KEY non configurata')
        }

        const { draft } = get()

        const res = await fetch('/api/v1/strategies/publish', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-Api-Key': apiKey,
          },
          body: JSON.stringify({ strategy: draft, overwrite: false }),
        })

        if (!res.ok) {
          const err = await res.json()

          // Handle conflict (strategy already exists)
          if (res.status === 409) {
            throw new Error('Strategia già esistente. Usa modalità edit per sovrascrivere.')
          }

          throw new Error(err.error || `HTTP ${res.status}`)
        }

        const result = (await res.json()) as { strategy_id: string }

        set((state) => {
          state.publishStatus = 'success'
          state.publishedStrategyId = result.strategy_id
          state.isDirty = false
        })
      } catch (e) {
        const error = e as Error
        set((state) => {
          state.publishStatus = 'error'
          state.publishError = error.message
        })
      }
    },

    resetWizard: () =>
      set((state) => {
        state.currentStep = 1
        state.visitedSteps = [1]
        state.mode = 'new'
        state.draft = createDefaultStrategy()
        state.originalJson = null
        state.stepErrors = {}
        state.globalErrors = []
        state.isDirty = false
        state.elCode = ''
        state.conversionResult = null
        state.conversionLoading = false
        state.conversionError = null
        state.publishStatus = 'idle'
        state.publishedStrategyId = null
        state.publishError = null
      }),
  }))
)
