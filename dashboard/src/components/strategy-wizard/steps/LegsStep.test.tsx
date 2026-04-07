/**
 * LegsStep Tests
 *
 * Test suite for the LegsStep wizard component.
 * Tests adding, editing, removing legs and validation behavior.
 */

import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { LegsStep } from './LegsStep'
import { useWizardStore } from '../../../stores/wizardStore'
import { createDefaultStrategy, createDefaultLeg } from '../../../utils/sdf-defaults'
import type { WizardState } from '../../../stores/wizardStore'

// Mock the wizard store
vi.mock('../../../stores/wizardStore')

describe('LegsStep', () => {
  // Setup default mock state
  const mockSetField = vi.fn()
  const defaultDraft = createDefaultStrategy()

  const createMockStore = (overrides?: Partial<WizardState>): WizardState => ({
    draft: defaultDraft,
    setField: mockSetField,
    currentStep: 5,
    totalSteps: 10,
    visitedSteps: [1, 2, 3, 4, 5],
    mode: 'new',
    originalJson: null,
    stepErrors: {},
    globalErrors: [],
    isDirty: false,
    elCode: '',
    conversionResult: null,
    conversionLoading: false,
    conversionError: null,
    publishStatus: 'idle',
    publishedStrategyId: null,
    publishError: null,
    goToStep: vi.fn(),
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    initFromJson: vi.fn(),
    validateAllSteps: vi.fn(),
    setElCode: vi.fn(),
    convertElToSdf: vi.fn(),
    applyConversionResult: vi.fn(),
    publish: vi.fn(),
    resetWizard: vi.fn(),
    ...overrides,
  })

  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useWizardStore).mockReturnValue(createMockStore())
  })

  it('TEST-03-01: should render with no legs and show empty message', () => {
    render(<LegsStep />)

    expect(screen.getByText(/Nessun leg configurato/i)).toBeInTheDocument()
    expect(screen.getByText(/Nessuna protezione configurata/i)).toBeInTheDocument()
  })

  it('TEST-03-02: should add new primary leg when clicking "+ Aggiungi Leg"', () => {
    render(<LegsStep />)

    const addButton = screen.getByText('+ Aggiungi Leg')
    fireEvent.click(addButton)

    // Check that setField was called to add a new leg
    expect(mockSetField).toHaveBeenCalledWith(
      'structure.legs',
      expect.arrayContaining([
        expect.objectContaining({
          leg_id: expect.stringContaining('leg-'),
          action: 'sell', // Primary legs default to sell
        }),
      ])
    )
  })

  it('TEST-03-03: should add new protection leg when clicking "+ Aggiungi Protection"', () => {
    render(<LegsStep />)

    const addButton = screen.getByText('+ Aggiungi Protection')
    fireEvent.click(addButton)

    // Check that setField was called to add a new protection leg
    expect(mockSetField).toHaveBeenCalledWith(
      'structure.protection_legs',
      expect.arrayContaining([
        expect.objectContaining({
          leg_id: expect.stringContaining('leg-'),
          action: 'buy', // Protection legs default to buy
        }),
      ])
    )
  })

  it('TEST-03-04: should show LegEditor when clicking "Edit" on LegCard', () => {
    const legWithData = createDefaultLeg(0, 'sell')
    const draftWithLegs = {
      ...defaultDraft,
      structure: {
        legs: [legWithData],
        protection_legs: [],
      },
    }

    vi.mocked(useWizardStore).mockReturnValue(
      createMockStore({
        draft: draftWithLegs,
      })
    )

    render(<LegsStep />)

    // Find and click Edit button
    const editButton = screen.getByText('Edit')
    fireEvent.click(editButton)

    // Should show LegEditor with "Edit Leg:" header
    expect(screen.getByText(`Edit Leg: ${legWithData.leg_id}`)).toBeInTheDocument()
    expect(screen.getByText('Save')).toBeInTheDocument()
    expect(screen.getByText('Cancel')).toBeInTheDocument()
  })

  it('TEST-03-05: should call setField when editing delta in LegEditor', async () => {
    const legWithData = createDefaultLeg(0, 'sell')
    const draftWithLegs = {
      ...defaultDraft,
      structure: {
        legs: [legWithData],
        protection_legs: [],
      },
    }

    vi.mocked(useWizardStore).mockReturnValue(
      createMockStore({
        draft: draftWithLegs,
      })
    )

    render(<LegsStep />)

    // Open editor
    fireEvent.click(screen.getByText('Edit'))

    // Find delta input and change it
    const deltaInput = screen.getByLabelText(/Target Delta/i) as HTMLInputElement
    fireEvent.change(deltaInput, { target: { value: '0.40' } })

    // Should call setField with correct path
    expect(mockSetField).toHaveBeenCalledWith('structure.legs.0.target_delta', 0.4)
  })

  it('TEST-03-06: should close LegEditor when clicking "Save"', () => {
    const legWithData = createDefaultLeg(0, 'sell')
    const draftWithLegs = {
      ...defaultDraft,
      structure: {
        legs: [legWithData],
        protection_legs: [],
      },
    }

    vi.mocked(useWizardStore).mockReturnValue(
      createMockStore({
        draft: draftWithLegs,
      })
    )

    render(<LegsStep />)

    // Open editor
    fireEvent.click(screen.getByText('Edit'))
    expect(screen.getByText('Save')).toBeInTheDocument()

    // Click Save
    fireEvent.click(screen.getByText('Save'))

    // Should show LegCard again (not editor)
    expect(screen.queryByText('Save')).not.toBeInTheDocument()
    expect(screen.getByText('Edit')).toBeInTheDocument()
  })

  it('TEST-03-07: should remove leg when clicking "Remove"', () => {
    const legWithData = createDefaultLeg(0, 'sell')
    const draftWithLegs = {
      ...defaultDraft,
      structure: {
        legs: [legWithData],
        protection_legs: [],
      },
    }

    vi.mocked(useWizardStore).mockReturnValue(
      createMockStore({
        draft: draftWithLegs,
      })
    )

    render(<LegsStep />)

    // Click Remove button
    const removeButton = screen.getByText('Remove')
    fireEvent.click(removeButton)

    // Should call setField with empty array
    expect(mockSetField).toHaveBeenCalledWith('structure.legs', [])
  })

  it('TEST-03-08: should show green badge for buy action in LegCard', () => {
    const buyLeg = createDefaultLeg(0, 'buy')
    const draftWithLegs = {
      ...defaultDraft,
      structure: {
        legs: [buyLeg],
        protection_legs: [],
      },
    }

    vi.mocked(useWizardStore).mockReturnValue(
      createMockStore({
        draft: draftWithLegs,
      })
    )

    render(<LegsStep />)

    // Find BUY badge - it should use success variant (green)
    const buyBadge = screen.getByText('BUY')
    expect(buyBadge).toBeInTheDocument()
    // The Badge component applies variant via className, check it exists
    expect(buyBadge.className).toContain('success')
  })

  it('TEST-03-09: should show orange/warning badge for put right in LegCard', () => {
    const putLeg = { ...createDefaultLeg(0, 'sell'), right: 'put' as const }
    const draftWithLegs = {
      ...defaultDraft,
      structure: {
        legs: [putLeg],
        protection_legs: [],
      },
    }

    vi.mocked(useWizardStore).mockReturnValue(
      createMockStore({
        draft: draftWithLegs,
      })
    )

    render(<LegsStep />)

    // Find PUT badge - it should use warning variant (orange)
    const putBadge = screen.getByText('PUT')
    expect(putBadge).toBeInTheDocument()
    expect(putBadge.className).toContain('warning')
  })

  it('TEST-03-10: should show correct summary count for legs', () => {
    const leg1 = createDefaultLeg(0, 'sell')
    const leg2 = createDefaultLeg(1, 'sell')
    const protectionLeg = createDefaultLeg(0, 'buy')

    const draftWithLegs = {
      ...defaultDraft,
      structure: {
        legs: [leg1, leg2],
        protection_legs: [protectionLeg],
      },
    }

    vi.mocked(useWizardStore).mockReturnValue(
      createMockStore({
        draft: draftWithLegs,
      })
    )

    render(<LegsStep />)

    // Should show summary
    expect(screen.getByText(/2 primary legs, 1 protection leg/i)).toBeInTheDocument()
  })
})
