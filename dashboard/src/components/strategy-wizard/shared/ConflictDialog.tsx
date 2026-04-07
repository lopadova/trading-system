/**
 * ConflictDialog — Modal for Handling Duplicate Strategy ID Conflicts (HTTP 409)
 *
 * Appears when publishing a strategy that already exists on Cloudflare.
 * Offers three options:
 * 1. Overwrite existing strategy
 * 2. Choose a new ID (navigate back to step 1)
 * 3. Cancel and fix manually
 */

export interface ConflictDialogProps {
  isOpen: boolean
  strategyId: string
  onOverwrite: () => void
  onChooseNewId: () => void
  onCancel: () => void
}

export default function ConflictDialog({
  isOpen,
  strategyId,
  onOverwrite,
  onChooseNewId,
  onCancel,
}: ConflictDialogProps) {
  if (!isOpen) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70">
      <div className="w-full max-w-md p-6 bg-gray-900 border-2 border-red-600 rounded-lg shadow-2xl">
        {/* Header */}
        <div className="flex items-center gap-3 mb-4">
          <span className="text-4xl">⚠️</span>
          <h2 className="text-xl font-bold text-red-400">Conflitto Strategia</h2>
        </div>

        {/* Message */}
        <p className="mb-6 text-sm text-gray-300">
          La strategia <span className="font-semibold text-amber-300">'{strategyId}'</span> esiste
          già su Cloudflare.
        </p>

        <p className="mb-6 text-xs text-gray-400">
          Scegli come procedere:
        </p>

        {/* Action Buttons */}
        <div className="space-y-3">
          {/* Overwrite */}
          <button
            type="button"
            onClick={onOverwrite}
            className="w-full px-4 py-3 text-sm font-semibold text-white bg-red-600 rounded-md hover:bg-red-700 transition-colors"
          >
            Sovrascrivi
          </button>

          {/* Choose New ID */}
          <button
            type="button"
            onClick={onChooseNewId}
            className="w-full px-4 py-3 text-sm font-semibold text-amber-300 bg-gray-800 border border-amber-600 rounded-md hover:bg-amber-600 hover:text-white transition-colors"
          >
            Scegli Nuovo ID
          </button>

          {/* Cancel */}
          <button
            type="button"
            onClick={onCancel}
            className="w-full px-4 py-3 text-sm font-medium text-gray-400 bg-gray-800 border border-gray-700 rounded-md hover:bg-gray-700 transition-colors"
          >
            Annulla
          </button>
        </div>

        {/* Warning Note */}
        <div className="mt-4 p-3 bg-yellow-900/20 border border-yellow-700 rounded-md">
          <p className="text-xs text-yellow-300">
            <strong>Attenzione:</strong> Sovrascrivere cancellerà definitivamente la versione
            esistente della strategia. Questa azione non può essere annullata.
          </p>
        </div>
      </div>
    </div>
  )
}
