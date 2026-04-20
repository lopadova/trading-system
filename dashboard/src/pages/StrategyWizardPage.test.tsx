// Smoke test for StrategyWizardPage — guards against the crash we saw when
// /strategies/new was wrapped in <Layout> and the wizard.css vars conflicted
// with the parent page's bg-[var(--bg-1)]. We only assert that the wizard
// mounts without throwing and shows its step heading.

import { render, screen } from '@testing-library/react'
import { describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { StrategyWizardPage } from './StrategyWizardPage'
import { useWizardStore } from '../stores/wizardStore'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

describe('StrategyWizardPage', () => {
  beforeEach(() => {
    // Reset the wizard to step 1 before each test so the heading is deterministic
    useWizardStore.getState().resetWizard()
  })

  it('mounts in new mode without crashing', () => {
    render(wrap(<StrategyWizardPage mode="new" />))
    // The page renders a heading that depends on the mode
    expect(screen.getByRole('heading', { name: /Nuova Strategia/i })).toBeInTheDocument()
  })

  it('renders wizard-root wrapper so the dark sub-theme applies', () => {
    const { container } = render(wrap(<StrategyWizardPage mode="new" />))
    // CSS vars for the wizard live on .wizard-root — if this wrapper is missing,
    // the whole wizard renders with the wrong colours.
    expect(container.querySelector('.wizard-root')).not.toBeNull()
  })

  it('mounts in edit mode with a strategyId', () => {
    render(wrap(<StrategyWizardPage mode="edit" strategyId="strat-123" />))
    expect(screen.getByRole('heading', { name: /Modifica Strategia/i })).toBeInTheDocument()
  })
})
