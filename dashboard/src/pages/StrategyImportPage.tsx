/**
 * Strategy Import Page
 *
 * Allows importing strategies from JSON files.
 */

import { useState } from 'react'
import { useWizardStore } from '../stores/wizardStore'
import { useNavigate } from '../hooks/useNavigate'

export function StrategyImportPage() {
  const [jsonInput, setJsonInput] = useState('')
  const [error, setError] = useState<string | null>(null)
  const { initFromJson } = useWizardStore()
  const navigate = useNavigate()

  const handleImport = () => {
    const result = initFromJson(jsonInput)

    if (result.ok) {
      // Navigate to wizard after successful import
      navigate('/strategies/new')
    } else {
      setError(result.errors.join(', '))
    }
  }

  return (
    <div className="container mx-auto px-4 py-8">
      <h1 className="text-3xl font-bold mb-6">Importa Strategia</h1>
      <div className="bg-card p-6 rounded-lg shadow">
        <div className="mb-4">
          <label className="block text-sm font-medium mb-2">
            Incolla JSON strategia
          </label>
          <textarea
            value={jsonInput}
            onChange={(e) => setJsonInput(e.target.value)}
            className="w-full h-64 px-3 py-2 border border-border rounded-md font-mono text-sm"
            placeholder='{ "strategy_id": "my-strategy", ... }'
          />
        </div>

        {error && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-md text-red-800 text-sm">
            {error}
          </div>
        )}

        <button
          onClick={handleImport}
          className="px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
        >
          Importa e continua
        </button>
      </div>
    </div>
  )
}
