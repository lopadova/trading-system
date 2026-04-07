/**
 * Step10Review — Review & Publish UI (Final Wizard Step)
 *
 * Features:
 * - Summary cards for all 9 previous steps
 * - JSON preview with syntax highlighting
 * - Download/copy JSON buttons
 * - Validation summary with clickable errors
 * - Publish button with state machine
 * - Conflict dialog for duplicate strategy IDs
 */

import { useState } from 'react'
import { useWizardStore } from '../../../stores/wizardStore'
import StepSummaryCard from '../shared/StepSummaryCard'
import ValidationSummary from '../shared/ValidationSummary'
import PublishButton from '../shared/PublishButton'
import ConflictDialog from '../shared/ConflictDialog'
import type { StepStatus } from '../shared/StepSummaryCard'

export default function Step10Review() {
  const draft = useWizardStore((state) => state.draft)
  const stepErrors = useWizardStore((state) => state.stepErrors)
  const publishError = useWizardStore((state) => state.publishError)
  const publish = useWizardStore((state) => state.publish)
  const goToStep = useWizardStore((state) => state.goToStep)

  const [showConflict, setShowConflict] = useState(false)
  const [copied, setCopied] = useState(false)

  // Check if publish error is a conflict (409)
  const isConflict = publishError?.includes('già esistente') || publishError?.includes('Conflict')

  // Show conflict dialog when conflict error occurs
  if (isConflict && !showConflict) {
    setShowConflict(true)
  }

  // Generate JSON string for preview and download
  const jsonString = JSON.stringify(draft, null, 2)

  // Derive step statuses from stepErrors
  const getStepStatus = (step: number): StepStatus => {
    const errors = stepErrors[step] || []
    const hasErrors = errors.some((e) => e.severity === 'error')
    const hasWarnings = errors.some((e) => e.severity === 'warning')

    if (hasErrors) return 'error'
    if (hasWarnings) return 'warning'
    return 'ok'
  }

  // Step preview data
  const stepPreviews = [
    {
      stepNumber: 1,
      stepName: 'Identità',
      icon: '🆔',
      preview: [
        `ID: ${draft.strategy_id || 'N/A'}`,
        `Nome: ${draft.name || 'N/A'}`,
        `Descrizione: ${draft.description?.substring(0, 50) || 'N/A'}...`,
      ],
    },
    {
      stepNumber: 2,
      stepName: 'Sottostante',
      icon: '📈',
      preview: [
        `Symbol: ${draft.instrument?.underlying_symbol || 'N/A'}`,
        `Exchange: ${draft.instrument?.underlying_exchange || 'N/A'}`,
        `Tipo: ${draft.instrument?.type || 'N/A'}`,
      ],
    },
    {
      stepNumber: 3,
      stepName: 'Entry Filters',
      icon: '⏰',
      preview: [
        `IVTS: ${draft.entry_filters?.ivts?.enabled ? 'Abilitato' : 'Disabilitato'}`,
        `Market hours: ${draft.entry_filters?.market_hours_only ? 'Sì' : 'No'}`,
      ],
    },
    {
      stepNumber: 4,
      stepName: 'Campaign Rules',
      icon: '💰',
      preview: [
        `Max campagne attive: ${draft.campaign_rules?.max_active_campaigns || 0}`,
        `Max per settimana: ${draft.campaign_rules?.max_per_rolling_week || 0}`,
      ],
    },
    {
      stepNumber: 5,
      stepName: 'Legs',
      icon: '🦵',
      preview: [
        `Legs definiti: ${draft.structure?.legs?.length || 0}`,
        `Contratti totali: ${draft.structure?.legs?.reduce((sum: number, leg) => sum + (leg?.quantity || 0), 0) || 0}`,
      ],
    },
    {
      stepNumber: 6,
      stepName: 'Filtri Selezione',
      icon: '🔍',
      preview: [
        `Min OI: ${draft.selection_filters?.min_open_interest || 0}`,
        `Max spread: ${draft.selection_filters?.max_spread_pct_of_mid || 0}%`,
        `Scoring: ${draft.selection_filters?.scoring_method || 'N/A'}`,
      ],
    },
    {
      stepNumber: 7,
      stepName: 'Regole Uscita',
      icon: '🚪',
      preview: [
        `Target: $${draft.exit_rules?.profit_target_usd || 0}`,
        `Stop: $${draft.exit_rules?.stop_loss_usd || 0}`,
        `Hard stops: ${draft.exit_rules?.hard_stop_conditions?.length || 0}`,
      ],
    },
    {
      stepNumber: 8,
      stepName: 'Esecuzione',
      icon: '⚙️',
      preview: [
        `Order type: ${draft.execution_rules?.order_type || 'N/A'}`,
        `Repricing: ${draft.execution_rules?.repricing?.enabled ? 'Abilitato' : 'Disabilitato'}`,
      ],
    },
    {
      stepNumber: 9,
      stepName: 'Monitoring',
      icon: '📊',
      preview: [
        `Greeks snapshot: ogni ${draft.monitoring?.greeks_snapshot_interval_minutes || 0} min`,
        `Risk check: ogni ${draft.monitoring?.risk_check_interval_minutes || 0} min`,
      ],
    },
  ]

  // Download JSON
  const handleDownload = () => {
    const blob = new Blob([jsonString], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${draft.strategy_id}.json`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
  }

  // Copy JSON to clipboard
  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(jsonString)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch (err) {
      console.error('Failed to copy:', err)
    }
  }

  // Handle conflict resolution
  const handleOverwrite = async () => {
    setShowConflict(false)
    // Re-publish with overwrite=true
    // This would require updating wizardStore.publish to accept overwrite param
    // For now, we'll just re-call publish (backend should handle overwrite)
    await publish()
  }

  const handleChooseNewId = () => {
    setShowConflict(false)
    goToStep(1)
    // TODO: Focus on strategy_id field (requires ref forwarding)
  }

  const handleCancelConflict = () => {
    setShowConflict(false)
    useWizardStore.setState({ publishStatus: 'idle', publishError: null })
  }

  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold text-amber-300 mb-2">Riepilogo e Pubblicazione</h2>
        <p className="text-sm text-gray-400">
          Rivedi tutti i parametri della strategia prima di pubblicarla su Cloudflare.
        </p>
      </div>

      {/* Step Summary Grid */}
      <div>
        <h3 className="text-lg font-semibold text-gray-200 mb-4">Riepilogo Step</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {stepPreviews.map((step) => (
            <StepSummaryCard
              key={step.stepNumber}
              stepNumber={step.stepNumber}
              stepName={step.stepName}
              icon={step.icon}
              status={getStepStatus(step.stepNumber)}
              preview={step.preview}
            />
          ))}
        </div>
      </div>

      {/* Validation Summary */}
      <ValidationSummary />

      {/* JSON Preview */}
      <div>
        <h3 className="text-lg font-semibold text-gray-200 mb-4">Preview JSON</h3>
        <div className="relative">
          <pre
            data-testid="json-preview"
            className="p-4 bg-gray-900 border border-gray-700 rounded-lg overflow-x-auto text-xs text-green-400 font-mono max-h-96"
          >
            {jsonString}
          </pre>

          {/* Copy/Download Buttons */}
          <div className="mt-3 flex gap-3">
            <button
              type="button"
              onClick={handleCopy}
              className="px-4 py-2 text-sm font-medium text-gray-300 bg-gray-800 border border-gray-700 rounded-md hover:bg-gray-700 transition-colors"
            >
              {copied ? '✅ Copiato!' : '📋 Copia JSON'}
            </button>
            <button
              type="button"
              onClick={handleDownload}
              className="px-4 py-2 text-sm font-medium text-amber-300 bg-gray-800 border border-amber-600 rounded-md hover:bg-amber-600 hover:text-white transition-colors"
            >
              💾 Scarica JSON
            </button>
          </div>
        </div>
      </div>

      {/* Publish Button */}
      <div className="pt-4">
        <PublishButton
          onSuccess={() => {
            console.log('Publish successful!')
          }}
          onError={(error) => {
            console.error('Publish error:', error)
          }}
        />
      </div>

      {/* Conflict Dialog */}
      <ConflictDialog
        isOpen={showConflict}
        strategyId={draft.strategy_id || 'unknown'}
        onOverwrite={handleOverwrite}
        onChooseNewId={handleChooseNewId}
        onCancel={handleCancelConflict}
      />
    </div>
  )
}
