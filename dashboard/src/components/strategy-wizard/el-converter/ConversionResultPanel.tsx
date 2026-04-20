/**
 * ConversionResultPanel — Displays EL conversion results
 *
 * Features:
 * - Loading state with animated robot
 * - Error state with clear message
 * - Success/partial/failed badges with confidence
 * - Accordion sections: issues, warnings, JSON preview, notes
 * - "Applica al Wizard" button (only if convertible)
 */

import type { ConversionResult } from '../../../hooks/useELConversion'
import { IssuesList } from './IssuesList'
import { JSONPreview } from './JSONPreview'
import { Button } from '../../ui/Button'
import { Badge } from '../../ui/Badge'

interface ConversionResultPanelProps {
  result: ConversionResult | null
  isLoading: boolean
  error: string | null
  onApplyToWizard: () => void
}

export function ConversionResultPanel({
  result,
  isLoading,
  error,
  onApplyToWizard
}: ConversionResultPanelProps) {
  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center h-full p-8 text-center">
        <div className="animate-pulse text-6xl mb-4">🤖</div>
        <p className="text-lg font-medium text-gray-200">Claude sta analizzando...</p>
        <p className="text-sm text-gray-400 mt-2">Questo può richiedere fino a 30 secondi</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center h-full p-8 text-center">
        <div className="text-6xl mb-4">❌</div>
        <p className="text-lg font-medium text-red-400 mb-2">Errore durante conversione</p>
        <p className="text-sm text-gray-400">{error}</p>
      </div>
    )
  }

  if (!result) {
    return (
      <div className="flex items-center justify-center h-full p-8 text-center text-gray-400">
        <div>
          <p className="text-lg mb-2">Nessun risultato</p>
          <p className="text-sm">Incolla codice EasyLanguage e premi Converti</p>
        </div>
      </div>
    )
  }

  // Determina stato conversione
  const isFullyConvertible = result.convertible === true
  const isPartiallyConvertible = result.convertible === 'partial'
  const isNotConvertible = result.convertible === false

  return (
    <div className="flex flex-col h-full">
      {/* Header con badge */}
      <div className="p-4 border-b border-gray-700 flex items-center justify-between">
        <div className="flex items-center gap-3">
          {isFullyConvertible && (
            <>
              <div className="text-3xl">✅</div>
              <div>
                <Badge tone="green">Convertibile</Badge>
                <p className="text-xs text-gray-400 mt-1">
                  Confidence: {(result.confidence * 100).toFixed(0)}%
                </p>
              </div>
            </>
          )}

          {isPartiallyConvertible && (
            <>
              <div className="text-3xl">⚠️</div>
              <div>
                <Badge tone="yellow">Parzialmente Convertibile</Badge>
                <p className="text-xs text-gray-400 mt-1">
                  Confidence: {(result.confidence * 100).toFixed(0)}%
                </p>
              </div>
            </>
          )}

          {isNotConvertible && (
            <>
              <div className="text-3xl">❌</div>
              <div>
                <Badge tone="red">Non Convertibile</Badge>
                <p className="text-xs text-gray-400 mt-1">
                  {result.issues.length} issues trovati
                </p>
              </div>
            </>
          )}
        </div>

        {/* Apply button (solo se convertibile) */}
        {(isFullyConvertible || isPartiallyConvertible) && result.result_json && (
          <Button onClick={onApplyToWizard}>
            🔧 Applica al Wizard →
          </Button>
        )}
      </div>

      {/* Accordion sections */}
      <div className="flex-1 overflow-auto p-4 space-y-4">
        {/* Issues */}
        {result.issues.length > 0 && (
          <details open={result.issues.length > 0}>
            <summary className="cursor-pointer font-semibold text-gray-200 mb-2">
              Issues ({result.issues.length})
            </summary>
            <IssuesList issues={result.issues} />
          </details>
        )}

        {/* Warnings */}
        {result.warnings.length > 0 && (
          <details>
            <summary className="cursor-pointer font-semibold text-gray-200 mb-2">
              Avvisi ({result.warnings.length})
            </summary>
            <ul className="space-y-2">
              {result.warnings.map((warning, i) => (
                <li key={i} className="text-sm text-yellow-400 bg-yellow-400/10 p-2 rounded">
                  {warning}
                </li>
              ))}
            </ul>
          </details>
        )}

        {/* JSON Preview */}
        {result.result_json && (
          <details>
            <summary className="cursor-pointer font-semibold text-gray-200 mb-2">
              Preview JSON
            </summary>
            <JSONPreview json={result.result_json} />
          </details>
        )}

        {/* Notes */}
        {result.notes && (
          <details>
            <summary className="cursor-pointer font-semibold text-gray-200 mb-2">
              Note
            </summary>
            <p className="text-sm text-gray-400 bg-gray-800 p-3 rounded whitespace-pre-wrap">
              {result.notes}
            </p>
          </details>
        )}
      </div>
    </div>
  )
}
