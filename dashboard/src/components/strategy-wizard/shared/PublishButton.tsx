/**
 * PublishButton — Publish Strategy Button with State Machine
 *
 * States: idle → validating → publishing → success/error
 * Shows appropriate UI for each state with loading indicators and success message.
 */

import { useWizardStore } from '../../../stores/wizardStore'

export interface PublishButtonProps {
  onSuccess?: () => void
  onError?: (error: string) => void
}

export default function PublishButton({ onSuccess, onError }: PublishButtonProps) {
  const publishStatus = useWizardStore((state) => state.publishStatus)
  const publishError = useWizardStore((state) => state.publishError)
  const publishedStrategyId = useWizardStore((state) => state.publishedStrategyId)
  const publish = useWizardStore((state) => state.publish)
  const resetWizard = useWizardStore((state) => state.resetWizard)

  const handlePublish = async () => {
    await publish()
    const state = useWizardStore.getState()
    if (state.publishStatus === 'success' && onSuccess) {
      onSuccess()
    }
    if (state.publishStatus === 'error' && onError && state.publishError) {
      onError(state.publishError)
    }
  }

  const handleCreateAnother = () => {
    resetWizard()
    // Navigation to /strategies/new is handled by parent component
  }

  const handleRetry = () => {
    handlePublish()
  }

  // Idle state
  if (publishStatus === 'idle') {
    return (
      <button
        type="button"
        onClick={handlePublish}
        className="w-full px-6 py-4 text-lg font-bold text-white bg-amber-600 rounded-lg hover:bg-amber-700 transition-colors shadow-lg"
      >
        🚀 Pubblica su Cloud
      </button>
    )
  }

  // Validating state
  if (publishStatus === 'validating') {
    return (
      <button
        type="button"
        disabled
        className="w-full px-6 py-4 text-lg font-bold text-gray-400 bg-gray-700 rounded-lg cursor-not-allowed"
      >
        <span className="inline-flex items-center gap-2">
          <svg className="w-5 h-5 animate-spin" viewBox="0 0 24 24">
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
              fill="none"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
          ⏳ Validazione...
        </span>
      </button>
    )
  }

  // Publishing state
  if (publishStatus === 'publishing') {
    return (
      <button
        type="button"
        disabled
        className="w-full px-6 py-4 text-lg font-bold text-gray-400 bg-gray-700 rounded-lg cursor-not-allowed"
      >
        <span className="inline-flex items-center gap-2">
          <svg className="w-5 h-5 animate-spin" viewBox="0 0 24 24">
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
              fill="none"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
          ⬆️ Caricamento...
        </span>
      </button>
    )
  }

  // Success state
  if (publishStatus === 'success') {
    return (
      <div className="p-6 bg-green-900/30 border-2 border-green-600 rounded-lg">
        <div className="flex items-center gap-3 mb-4">
          <span className="text-4xl">✅</span>
          <div>
            <h3 className="text-lg font-bold text-green-300">Strategia Pubblicata!</h3>
            <p className="text-sm text-green-400">ID: {publishedStrategyId}</p>
          </div>
        </div>

        <p className="mb-4 text-sm text-gray-300">
          La strategia è stata caricata su Cloudflare. Il TradingSupervisorService la scaricherà
          entro 60 secondi e inizierà il monitoraggio automatico.
        </p>

        <div className="flex gap-3">
          <button
            type="button"
            onClick={() => {
              window.location.href = '/strategies'
            }}
            className="flex-1 px-4 py-3 text-sm font-semibold text-white bg-blue-600 rounded-md hover:bg-blue-700 transition-colors"
          >
            📊 Vai alle Strategie
          </button>
          <button
            type="button"
            onClick={handleCreateAnother}
            className="flex-1 px-4 py-3 text-sm font-semibold text-amber-300 bg-gray-800 border border-amber-600 rounded-md hover:bg-amber-600 hover:text-white transition-colors"
          >
            ➕ Crea un'altra
          </button>
        </div>
      </div>
    )
  }

  // Error state
  if (publishStatus === 'error') {
    return (
      <div className="p-6 bg-red-900/30 border-2 border-red-600 rounded-lg">
        <div className="flex items-center gap-3 mb-4">
          <span className="text-4xl">❌</span>
          <h3 className="text-lg font-bold text-red-300">Pubblicazione Fallita</h3>
        </div>

        <p className="mb-4 text-sm text-red-400">{publishError}</p>

        <div className="flex gap-3">
          <button
            type="button"
            onClick={handleRetry}
            className="flex-1 px-4 py-3 text-sm font-semibold text-white bg-red-600 rounded-md hover:bg-red-700 transition-colors"
          >
            Riprova
          </button>
          <button
            type="button"
            onClick={() => {
              useWizardStore.setState({ publishStatus: 'idle', publishError: null })
            }}
            className="flex-1 px-4 py-3 text-sm font-medium text-gray-400 bg-gray-800 border border-gray-700 rounded-md hover:bg-gray-700 transition-colors"
          >
            Annulla
          </button>
        </div>
      </div>
    )
  }

  return null
}
