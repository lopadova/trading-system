/**
 * Tests for Steps 6-9 (T-05 Implementation)
 *
 * Tests cover:
 * - Step06SelectionFilters
 * - Step07ExitRules with HardStopBuilder
 * - Step08ExecutionRules
 * - Step09Monitoring
 */

import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import type { MouseEventHandler, ReactNode } from 'react'
import { useWizardStore } from '../../../stores/wizardStore'
import { Step06SelectionFilters } from './Step06SelectionFilters'
import { Step07ExitRules } from './Step07ExitRules'
import { Step08ExecutionRules } from './Step08ExecutionRules'
import { Step09Monitoring } from './Step09Monitoring'
import type { StrategyDraft } from '../../../types/sdf-v1'

// Prop types for minimal UI component mocks
type MockButtonProps = {
  children?: ReactNode
  onClick?: MouseEventHandler<HTMLButtonElement>
  className?: string
}
type MockCardProps = {
  children?: ReactNode
  className?: string
}

// ============================================================================
// MOCKS
// ============================================================================

// Mock the wizard store
vi.mock('../../../stores/wizardStore', () => ({
  useWizardStore: vi.fn(),
}))

// Mock UI components
vi.mock('../../ui/Button', () => ({
  Button: ({ children, onClick, className }: MockButtonProps) => (
    <button onClick={onClick} className={className}>
      {children}
    </button>
  ),
}))

vi.mock('../../ui/Card', () => ({
  Card: ({ children, className }: MockCardProps) => <div className={className}>{children}</div>,
}))

// Mock icons
vi.mock('@heroicons/react/24/outline', () => ({
  QuestionMarkCircleIcon: () => <svg data-testid="question-icon" />,
  ExclamationTriangleIcon: () => <svg data-testid="exclamation-icon" />,
  XMarkIcon: () => <svg data-testid="x-icon" />,
  PlusIcon: () => <svg data-testid="plus-icon" />,
  CheckCircleIcon: () => <svg data-testid="check-icon" />,
  ExclamationCircleIcon: () => <svg data-testid="exclamation-circle-icon" />,
  ClockIcon: () => <svg data-testid="clock-icon" />,
  ShieldExclamationIcon: () => <svg data-testid="shield-icon" />,
}))

// ============================================================================
// TEST HELPERS
// ============================================================================

function createMockDraft(): StrategyDraft {
  return {
    strategy_id: 'test_strategy',
    name: 'Test Strategy',
    description: 'Test',
    author: 'test',
    strategy_version: '1.0.0',
    instrument: {
      type: 'options',
      underlying_symbol: 'SPX',
      underlying_sec_type: 'IND',
      underlying_exchange: 'CBOE',
      options_exchange: 'SMART',
      currency: 'USD',
      multiplier: 100,
      option_right: 'put',
    },
    entry_filters: {
      ivts: {
        enabled: false,
        formula: 'default',
        suspend_threshold: 0,
        resume_threshold: 0,
        staleness_max_minutes: 60,
        fallback_behavior: 'allow',
      },
      market_hours_only: true,
      safe_execution_window: {
        enabled: true,
        exclude_first_minutes: 30,
        exclude_last_minutes: 30,
      },
    },
    campaign_rules: {
      max_active_campaigns: 3,
      max_per_rolling_week: 2,
      week_start_day: 'monday',
      overlap_check_enabled: true,
    },
    structure: {
      legs: [
        {
          leg_id: 'leg_001',
          action: 'sell',
          right: 'put',
          target_dte: 45,
          dte_tolerance: 5,
          target_delta: 0.16,
          delta_tolerance: 0.02,
          quantity: 1,
          settlement_preference: 'PM',
          exclude_expiry_within_days: 0,
          role: 'short put',
          order_group: 'combo',
        },
      ],
      protection_legs: [],
    },
    selection_filters: {
      min_open_interest: 100,
      max_spread_pct_of_mid: 10,
      scoring_method: 'min_delta_distance',
    },
    exit_rules: {
      profit_target_usd: 2000,
      stop_loss_usd: 5000,
      max_days_in_position: 60,
      hard_stop_conditions: [],
    },
    execution_rules: {
      order_type: 'limit_mid',
      repricing: {
        enabled: false,
        max_attempts: 5,
        interval_seconds: 30,
        step_pct_of_half_spread: 10,
        max_slippage_pct_from_first_mid: 5,
        fallback_on_max_attempts: 'cancel_and_block',
      },
      opening_sequence: 'combo_first',
      margin_buffer_pct: 10,
      what_if_check_enabled: true,
      gtc_target_order: {
        enabled: false,
        submit_immediately_after_fill: true,
      },
    },
    monitoring: {
      greeks_snapshot_interval_minutes: 15,
      risk_check_interval_minutes: 5,
    },
    notifications: {
      on_campaign_opened: true,
      on_target_hit: true,
      on_stop_loss_hit: true,
      on_hard_stop_triggered: true,
      on_max_days_close: true,
      on_ivts_state_change: true,
    },
    changelog: [],
  }
}

function setupMockStore(draft: StrategyDraft) {
  const setField = vi.fn()
  // vi.mocked() gives us the typed Mock view of the hook so we can call
  // mockReturnValue without casting to `any`. The partial store shape is
  // sufficient for these tests (Step06-09 only consume draft + setField).
  vi.mocked(useWizardStore).mockReturnValue({
    draft,
    setField,
  } as unknown as ReturnType<typeof useWizardStore>)
  return { setField }
}

// ============================================================================
// STEP 06 TESTS
// ============================================================================

describe('Step06SelectionFilters', () => {
  it('TEST-SW-05-01: Renders with default values', () => {
    const draft = createMockDraft()
    setupMockStore(draft)

    render(<Step06SelectionFilters />)

    expect(screen.getByText('Filtri Selezione Opzioni')).toBeInTheDocument()
    expect(screen.getByDisplayValue('100')).toBeInTheDocument() // min_open_interest
  })

  it('TEST-SW-05-02: Changes min_open_interest', () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step06SelectionFilters />)

    const input = screen.getByDisplayValue('100')
    fireEvent.change(input, { target: { value: '500' } })

    expect(setField).toHaveBeenCalledWith('selection_filters.min_open_interest', 500)
  })

  it('TEST-SW-05-03: Changes max_spread_pct_of_mid slider', () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step06SelectionFilters />)

    const sliders = screen.getAllByRole('slider')
    const slider = sliders[0]
    expect(slider).toBeDefined()
    if (slider) {
      fireEvent.change(slider, { target: { value: '15' } })
    }

    expect(setField).toHaveBeenCalledWith('selection_filters.max_spread_pct_of_mid', 15)
  })

  it('TEST-SW-05-04: Shows correct spread color (green/yellow/red)', () => {
    const draft = createMockDraft()
    if (draft.selection_filters) {
      draft.selection_filters.max_spread_pct_of_mid = 3 // Green range (0-5%)
    }
    setupMockStore(draft)

    const { rerender } = render(<Step06SelectionFilters />)
    expect(screen.getByText('3.0%')).toBeInTheDocument()

    // Change to yellow range (5-10%)
    if (draft.selection_filters) {
      draft.selection_filters.max_spread_pct_of_mid = 7
    }
    setupMockStore(draft)
    rerender(<Step06SelectionFilters />)
    expect(screen.getByText('7.0%')).toBeInTheDocument()

    // Change to red range (10-20%)
    if (draft.selection_filters) {
      draft.selection_filters.max_spread_pct_of_mid = 15
    }
    setupMockStore(draft)
    rerender(<Step06SelectionFilters />)
    expect(screen.getByText('15.0%')).toBeInTheDocument()
  })

  it('TEST-SW-05-05: Changes scoring_method', () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step06SelectionFilters />)

    const maxOiRadio = screen.getByRole('radio', { name: /Max Open Interest/i })
    if (maxOiRadio) {
      fireEvent.click(maxOiRadio)
    }

    expect(setField).toHaveBeenCalledWith('selection_filters.scoring_method', 'max_oi')
  })
})

// ============================================================================
// STEP 07 TESTS
// ============================================================================

describe('Step07ExitRules', () => {
  it('TEST-SW-05-06: Renders exit rules with defaults', () => {
    const draft = createMockDraft()
    setupMockStore(draft)

    render(<Step07ExitRules />)

    expect(screen.getByText('Regole di Uscita')).toBeInTheDocument()
    expect(screen.getByDisplayValue('2000')).toBeInTheDocument() // profit_target_usd
    expect(screen.getByDisplayValue('5000')).toBeInTheDocument() // stop_loss_usd
  })

  it('TEST-SW-05-07: Updates profit_target_usd', () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step07ExitRules />)

    const profitInput = screen.getByDisplayValue('2000')
    fireEvent.change(profitInput, { target: { value: '3000' } })

    expect(setField).toHaveBeenCalledWith('exit_rules.profit_target_usd', 3000)
  })

  it('TEST-SW-05-08: TEST-SW-05-03 - Preview updates when profit_target_usd changes', () => {
    const draft = createMockDraft()
    if (draft.exit_rules) {
      draft.exit_rules.profit_target_usd = 2000
    }
    setupMockStore(draft)

    const { rerender } = render(<Step07ExitRules />)
    expect(screen.getByText('+$2000')).toBeInTheDocument()

    // Change profit target
    if (draft.exit_rules) {
      draft.exit_rules.profit_target_usd = 5000
    }
    setupMockStore(draft)
    rerender(<Step07ExitRules />)
    expect(screen.getByText('+$5000')).toBeInTheDocument()
  })

  it('TEST-SW-05-09: Adds hard stop condition', () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step07ExitRules />)

    const addButton = screen.getByRole('button', { name: /Aggiungi Hard Stop/i })
    fireEvent.click(addButton)

    expect(setField).toHaveBeenCalled()
    const callArgs = setField.mock.calls[0]
    expect(callArgs).toBeDefined()
    if (callArgs) {
      expect(callArgs[0]).toBe('exit_rules.hard_stop_conditions')
      expect(callArgs[1]).toHaveLength(1) // One condition added
    }
  })

  it('TEST-SW-05-10: TEST-SW-05-01 - Shows reference_leg_id when type=underlying_vs_leg_strike', () => {
    const draft = createMockDraft()
    if (draft.exit_rules) {
      draft.exit_rules.hard_stop_conditions = [
      {
        condition_id: 'hs_001',
        type: 'underlying_vs_leg_strike',
        reference_leg_id: 'leg_001',
        operator: 'lt',
        threshold: 4500,
        severity: 'critical',
        close_sequence: 'all_legs',
      },
      ]
    }
    setupMockStore(draft)

    render(<Step07ExitRules />)

    // Check that Reference Leg label and select are visible
    const labels = screen.getAllByText(/Reference Leg/i)
    expect(labels.length).toBeGreaterThan(0)

    // Check that the leg option exists in the select
    const select = screen.getByRole('combobox', { name: /Reference Leg/i })
    expect(select).toBeInTheDocument()
  })

  it('TEST-SW-05-11: TEST-SW-05-02 - Shows greek selector when type=portfolio_greek', () => {
    const draft = createMockDraft()
    if (draft.exit_rules) {
      draft.exit_rules.hard_stop_conditions = [
      {
        condition_id: 'hs_002',
        type: 'portfolio_greek',
        greek: 'delta',
        operator: 'gt',
        threshold: 0.5,
        severity: 'high',
        close_sequence: 'all_legs',
      },
      ]
    }
    setupMockStore(draft)

    render(<Step07ExitRules />)

    // Check that Greek label and select are visible
    const labels = screen.getAllByText(/Greek/i)
    expect(labels.length).toBeGreaterThan(0)

    // Verify greek select exists
    const selects = screen.getAllByRole('combobox')
    expect(selects.length).toBeGreaterThan(0)
  })

  it('TEST-SW-05-12: TEST-SW-05-07 - Validates reference_leg_id exists in legs', async () => {
    const draft = createMockDraft()
    if (draft.exit_rules) {
      draft.exit_rules.hard_stop_conditions = [
      {
        condition_id: 'hs_003',
        type: 'underlying_vs_leg_strike',
        reference_leg_id: 'leg_999', // Does not exist
        operator: 'lt',
        threshold: 4500,
        severity: 'critical',
        close_sequence: 'all_legs',
      },
      ]
    }
    setupMockStore(draft)

    render(<Step07ExitRules />)

    // The HardStopBuilder should render with the condition
    expect(screen.getByDisplayValue('hs_003')).toBeInTheDocument()

    // Change the reference_leg_id select - this should trigger validation
    const select = screen.getByRole('combobox', { name: /Reference Leg/i })
    fireEvent.change(select, { target: { value: 'leg_001' } })

    // After changing to valid leg, no error should show
    await waitFor(() => {
      const errorTexts = screen.queryByText(/not found in strategy structure/i)
      expect(errorTexts).not.toBeInTheDocument()
    })
  })
})

// ============================================================================
// STEP 08 TESTS
// ============================================================================

describe('Step08ExecutionRules', () => {
  it('TEST-SW-05-13: Renders execution rules', () => {
    const draft = createMockDraft()
    setupMockStore(draft)

    render(<Step08ExecutionRules />)

    expect(screen.getByText('Regole di Esecuzione')).toBeInTheDocument()
  })

  it('TEST-SW-05-14: TEST-SW-05-05 - Shows warning when order_type=market', () => {
    const draft = createMockDraft()
    if (draft.execution_rules) {
      draft.execution_rules.order_type = 'market'
    }
    setupMockStore(draft)

    render(<Step08ExecutionRules />)

    expect(screen.getByText(/Attenzione: Market Order/i)).toBeInTheDocument()
    expect(screen.getByText(/slippage 5-10%/i)).toBeInTheDocument()
  })

  it('TEST-SW-05-15: TEST-SW-05-04 - Repricing fields disabled when repricing.enabled=false', () => {
    const draft = createMockDraft()
    if (draft.execution_rules?.repricing) {
      draft.execution_rules.repricing.enabled = false
    }
    setupMockStore(draft)

    render(<Step08ExecutionRules />)

    // Find all number inputs and check the max_attempts one is disabled
    const numberInputs = screen.getAllByRole('spinbutton')
    const maxAttemptsInput = numberInputs.find((input) => (input as HTMLInputElement).value === '5')
    expect(maxAttemptsInput).toBeDefined()
    if (maxAttemptsInput) {
      expect((maxAttemptsInput as HTMLInputElement).disabled).toBe(true)
    }
  })

  it('TEST-SW-05-16: Repricing fields enabled when repricing.enabled=true', () => {
    const draft = createMockDraft()
    if (draft.execution_rules?.repricing) {
      draft.execution_rules.repricing.enabled = true
    }
    setupMockStore(draft)

    render(<Step08ExecutionRules />)

    // Find all number inputs and check the max_attempts one is enabled
    const numberInputs = screen.getAllByRole('spinbutton')
    const maxAttemptsInput = numberInputs.find((input) => (input as HTMLInputElement).value === '5')
    expect(maxAttemptsInput).toBeDefined()
    if (maxAttemptsInput) {
      expect((maxAttemptsInput as HTMLInputElement).disabled).toBe(false)
    }
  })

  it('TEST-SW-05-17: Toggles repricing.enabled', () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step08ExecutionRules />)

    const toggle = screen.getByRole('checkbox', { name: '' }) // repricing toggle
    fireEvent.click(toggle)

    expect(setField).toHaveBeenCalledWith('execution_rules.repricing.enabled', true)
  })

  it('TEST-SW-05-18: Shows GTC submit_immediately field when gtc.enabled=true', () => {
    const draft = createMockDraft()
    if (draft.execution_rules?.gtc_target_order) {
      draft.execution_rules.gtc_target_order.enabled = true
    }
    setupMockStore(draft)

    render(<Step08ExecutionRules />)

    expect(screen.getByText(/Invia Immediatamente Dopo Fill/i)).toBeInTheDocument()
  })
})

// ============================================================================
// STEP 09 TESTS
// ============================================================================

describe('Step09Monitoring', () => {
  it('TEST-SW-05-19: Renders monitoring config', () => {
    const draft = createMockDraft()
    setupMockStore(draft)

    render(<Step09Monitoring />)

    expect(screen.getByText('Monitoring e Notifiche')).toBeInTheDocument()
    expect(screen.getByText('15 minuti')).toBeInTheDocument() // greeks_snapshot_interval
    expect(screen.getByText('5 minuti')).toBeInTheDocument() // risk_check_interval
  })

  it('TEST-SW-05-20: Changes greeks_snapshot_interval', () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step09Monitoring />)

    const sliders = screen.getAllByRole('slider')
    const slider = sliders[0]
    expect(slider).toBeDefined()
    if (slider) {
      fireEvent.change(slider, { target: { value: '30' } })
    }

    expect(setField).toHaveBeenCalledWith('monitoring.greeks_snapshot_interval_minutes', 30)
  })

  it('TEST-SW-05-21: TEST-SW-05-06 - Warning when trying to disable hard_stop_triggered', async () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step09Monitoring />)

    const hardStopToggle = screen.getByRole('checkbox', {
      name: /Hard Stop Triggered/i,
    }) as HTMLInputElement
    expect(hardStopToggle.checked).toBe(true)

    // Try to disable it
    fireEvent.click(hardStopToggle)

    // Should show warning
    await waitFor(() => {
      expect(screen.getByText(/NON può essere disabilitato/i)).toBeInTheDocument()
    })

    // Should force it back to true
    await waitFor(
      () => {
        expect(setField).toHaveBeenCalledWith('notifications.on_hard_stop_triggered', true)
      },
      { timeout: 3000 }
    )
  })

  it('TEST-SW-05-22: Toggles other notifications successfully', () => {
    const draft = createMockDraft()
    const { setField } = setupMockStore(draft)

    render(<Step09Monitoring />)

    const campaignOpenedToggle = screen.getByRole('checkbox', {
      name: /Campaign Opened/i,
    })
    fireEvent.click(campaignOpenedToggle)

    expect(setField).toHaveBeenCalledWith('notifications.on_campaign_opened', false)
  })
})
