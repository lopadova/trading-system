/**
 * Strategy Convert Page
 *
 * Converts EasyLanguage code to SDF format using Claude API.
 */

import { useWizardStore } from '../stores/wizardStore'
import { useNavigate } from '../hooks/useNavigate'

export function StrategyConvertPage() {
  const { elCode, setElCode, convertElToSdf, conversionLoading, conversionError, conversionResult } =
    useWizardStore()
  const navigate = useNavigate()

  const handleConvert = async () => {
    await convertElToSdf()
  }

  const handleContinue = () => {
    if (conversionResult?.success) {
      navigate('/strategies/new')
    }
  }

  return (
    <div className="wizard-root">
      <div className="container mx-auto px-4 py-8">
        <h1 className="text-3xl font-bold mb-6">Converti EasyLanguage</h1>
        <div className="bg-card p-6 rounded-lg shadow">
        <div className="mb-4">
          <label className="block text-sm font-medium mb-2">
            Incolla codice EasyLanguage
          </label>
          <textarea
            value={elCode}
            onChange={(e) => setElCode(e.target.value)}
            className="w-full h-64 px-3 py-2 border border-border rounded-md font-mono text-sm"
            placeholder="Inputs:
  DTE = 45,
  Delta = 0.30,
  ..."
            disabled={conversionLoading}
          />
        </div>

        {conversionError && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-md text-red-800 text-sm">
            {conversionError}
          </div>
        )}

        {conversionResult?.success && (
          <div className="mb-4 p-3 bg-green-50 border border-green-200 rounded-md text-green-800 text-sm">
            Conversione completata con successo!
          </div>
        )}

        {conversionResult?.warnings && conversionResult.warnings.length > 0 && (
          <div className="mb-4 p-3 bg-yellow-50 border border-yellow-200 rounded-md text-yellow-800 text-sm">
            <strong>Avvisi:</strong>
            <ul className="list-disc list-inside mt-2">
              {conversionResult.warnings.map((warning, i) => (
                <li key={i}>{warning}</li>
              ))}
            </ul>
          </div>
        )}

        <div className="flex gap-2">
          <button
            onClick={handleConvert}
            disabled={conversionLoading || !elCode.trim()}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90 disabled:opacity-50"
          >
            {conversionLoading ? 'Conversione in corso...' : 'Converti'}
          </button>

          {conversionResult?.success && (
            <button
              onClick={handleContinue}
              className="px-4 py-2 bg-secondary text-secondary-foreground rounded-md hover:bg-secondary/90"
            >
              Continua nel wizard
            </button>
          )}
        </div>
        </div>
      </div>
    </div>
  )
}
