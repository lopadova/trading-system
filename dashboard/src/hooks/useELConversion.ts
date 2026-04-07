/**
 * useELConversion — React Hook for EasyLanguage to SDF Conversion
 *
 * Handles API calls to Cloudflare Worker conversion endpoint.
 * Manages loading, success, and error states for conversion flow.
 */

import { useState } from 'react'
import type { StrategyDefinition } from '../types/sdf-v1'

// ============================================================================
// TYPES
// ============================================================================

export interface ConversionIssue {
  type: 'not_supported' | 'ambiguous' | 'manual_required'
  el_construct: string
  description: string
  suggestion: string
}

export interface ConversionResult {
  convertible: boolean | 'partial'
  confidence: number
  result_json: Partial<StrategyDefinition> | null
  issues: ConversionIssue[]
  warnings: string[]
  notes: string
}

export interface ConversionState {
  isLoading: boolean
  result: ConversionResult | null
  error: string | null
}

// ============================================================================
// HOOK
// ============================================================================

export function useELConversion() {
  const [state, setState] = useState<ConversionState>({
    isLoading: false,
    result: null,
    error: null
  })

  const convert = async (easylanguageCode: string) => {
    setState({ isLoading: true, result: null, error: null })

    try {
      const response = await fetch('/api/v1/strategies/convert-el', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Api-Key': import.meta.env.VITE_WORKER_API_KEY || ''
        },
        body: JSON.stringify({ easylanguage_code: easylanguageCode })
      })

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}))

        if (response.status === 503) {
          throw new Error('Conversione AI non disponibile: API key non configurata')
        }

        throw new Error(errorData.message || `HTTP ${response.status}`)
      }

      const result: ConversionResult = await response.json()

      setState({ isLoading: false, result, error: null })
      return result

    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Errore sconosciuto'
      setState({ isLoading: false, result: null, error: errorMessage })
      throw err
    }
  }

  const reset = () => {
    setState({ isLoading: false, result: null, error: null })
  }

  return {
    ...state,
    convert,
    reset
  }
}
