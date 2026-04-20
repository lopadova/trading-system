/**
 * StrategyWizardPage — Main Page for Strategy Wizard
 *
 * Renders the wizard container with step indicator, content area, and help panel.
 * Routes to different step components based on current wizard state.
 */

import { WizardContainer } from '../../../components/strategy-wizard/WizardContainer'
import { NavigationButtons } from '../../../components/strategy-wizard/shared/NavigationButtons'
import { useWizardStore } from '../../../stores/wizardStore'

// Import step components (will be implemented in subsequent tasks)
// import { Step01Identity } from '../../../components/strategy-wizard/steps/Step01Identity'
// import { Step02Instrument } from '../../../components/strategy-wizard/steps/Step02Instrument'
// ... etc

// ============================================================================
// PLACEHOLDER STEP COMPONENTS
// ============================================================================
// These will be replaced by actual implementations in T-04, T-05, T-06, etc.

function PlaceholderStep({ stepNumber, title }: { stepNumber: number; title: string }) {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-display font-bold text-[var(--wz-text)] mb-2">
          {title}
        </h2>
        <p className="text-[var(--wz-muted)]">
          Step {stepNumber} implementation — coming soon
        </p>
      </div>

      <div className="wz-card p-8 text-center">
        <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-[var(--wz-amber-dim)] text-[var(--wz-amber)] mb-4">
          <span className="text-2xl font-display font-bold">{stepNumber}</span>
        </div>
        <p className="text-[var(--wz-muted)]">
          This step will be implemented in a future task.
        </p>
      </div>
    </div>
  )
}

// ============================================================================
// HELP CONTENT PER STEP
// ============================================================================

function getHelpContent(step: number) {
  switch (step) {
    case 1:
      return (
        <div>
          <h3 className="font-semibold mb-2">Identità Strategia</h3>
          <p>Definisci le informazioni di base della strategia: nome, ID, autore e tag.</p>
        </div>
      )
    case 2:
      return (
        <div>
          <h3 className="font-semibold mb-2">Strumento Finanziario</h3>
          <p>Configura il sottostante, il tipo di opzioni e la borsa di riferimento.</p>
        </div>
      )
    case 3:
      return (
        <div>
          <h3 className="font-semibold mb-2">Filtri di Ingresso</h3>
          <p>Imposta i filtri IVTS e le finestre temporali per l'apertura delle posizioni.</p>
        </div>
      )
    case 4:
      return (
        <div>
          <h3 className="font-semibold mb-2">Regole Campagna</h3>
          <p>Definisci i limiti sul numero di campagne attive e settimanali.</p>
        </div>
      )
    case 5:
      return (
        <div>
          <h3 className="font-semibold mb-2">Struttura Legs</h3>
          <p>Costruisci la struttura delle opzioni (legs) con delta, DTE e quantità.</p>
        </div>
      )
    case 6:
      return (
        <div>
          <h3 className="font-semibold mb-2">Filtri di Selezione</h3>
          <p>Configura i filtri avanzati per la selezione degli strike.</p>
        </div>
      )
    case 7:
      return (
        <div>
          <h3 className="font-semibold mb-2">Regole di Uscita</h3>
          <p>Imposta profit target, stop loss, trailing stop e altre regole di chiusura.</p>
        </div>
      )
    case 8:
      return (
        <div>
          <h3 className="font-semibold mb-2">Esecuzione</h3>
          <p>Configura le impostazioni di esecuzione: tipo di ordine, retry, sizing.</p>
        </div>
      )
    case 9:
      return (
        <div>
          <h3 className="font-semibold mb-2">Monitoring</h3>
          <p>Definisci come monitorare le posizioni e gestire gli alert.</p>
        </div>
      )
    case 10:
      return (
        <div>
          <h3 className="font-semibold mb-2">Review & Publish</h3>
          <p>Rivedi la strategia completa e pubblicala per l'esecuzione.</p>
        </div>
      )
    default:
      return null
  }
}

// ============================================================================
// MAIN PAGE COMPONENT
// ============================================================================

export function StrategyWizardPage() {
  const currentStep = useWizardStore((state) => state.currentStep)

  return (
    <div className="wizard-root">
      <WizardContainer helpContent={getHelpContent(currentStep)}>
        {/* Render Current Step */}
        <div className="min-h-[600px]">
          {currentStep === 1 && <PlaceholderStep stepNumber={1} title="Identità Strategia" />}
          {currentStep === 2 && <PlaceholderStep stepNumber={2} title="Strumento Finanziario" />}
          {currentStep === 3 && <PlaceholderStep stepNumber={3} title="Filtri di Ingresso" />}
          {currentStep === 4 && <PlaceholderStep stepNumber={4} title="Regole Campagna" />}
          {currentStep === 5 && <PlaceholderStep stepNumber={5} title="Struttura Legs" />}
          {currentStep === 6 && <PlaceholderStep stepNumber={6} title="Filtri di Selezione" />}
          {currentStep === 7 && <PlaceholderStep stepNumber={7} title="Regole di Uscita" />}
          {currentStep === 8 && <PlaceholderStep stepNumber={8} title="Esecuzione" />}
          {currentStep === 9 && <PlaceholderStep stepNumber={9} title="Monitoring" />}
          {currentStep === 10 && <PlaceholderStep stepNumber={10} title="Review & Publish" />}
        </div>

        {/* Navigation Footer */}
        <NavigationButtons className="mt-8" />
      </WizardContainer>
    </div>
  )
}
