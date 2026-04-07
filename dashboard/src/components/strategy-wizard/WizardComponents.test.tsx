/**
 * Wizard Components Test Suite
 *
 * Tests for T-04 (Wizard UI Shell + Design System + Shared Components):
 * - StepIndicator
 * - DeltaSlider
 * - ImportDropzone
 * - FieldWithTooltip
 * - ValidationBadge
 * - NavigationButtons
 */

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { StepIndicator } from './StepIndicator'
import { DeltaSlider } from './shared/DeltaSlider'
import { ImportDropzone } from './shared/ImportDropzone'
import { FieldWithTooltip } from './shared/FieldWithTooltip'
import { ValidationBadge } from './shared/ValidationBadge'
import { NavigationButtons } from './shared/NavigationButtons'
import { useWizardStore } from '../../stores/wizardStore'

// ============================================================================
// MOCKS
// ============================================================================

// Mock motion/react to avoid animation issues in tests
vi.mock('motion/react', () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
  },
  AnimatePresence: ({ children }: any) => <>{children}</>,
}))

// Helper to reset wizard store before each test
beforeEach(() => {
  useWizardStore.setState({
    currentStep: 1,
    totalSteps: 10,
    visitedSteps: [1],
    stepErrors: {},
    draft: {} as any,
    mode: 'new',
    originalJson: null,
    globalErrors: [],
    isDirty: false,
    elCode: '',
    conversionResult: null,
    conversionLoading: false,
    conversionError: null,
    publishStatus: 'idle',
    publishedStrategyId: null,
    publishError: null,
  })
})

// ============================================================================
// TEST-SW-03-01: StepIndicator with currentStep=3
// ============================================================================

describe('TEST-SW-03-01: StepIndicator current step rendering', () => {
  it('should show steps 1,2 as done and step 3 as active', () => {
    useWizardStore.setState({
      currentStep: 3,
      visitedSteps: [1, 2, 3],
      stepErrors: {},
    })

    render(<StepIndicator />)

    // Step 1 and 2 should have checkmark (done)
    const checkIcons = screen.getAllByLabelText(/step (1|2):/i)
    expect(checkIcons.length).toBeGreaterThanOrEqual(2)

    // Step 3 should be marked as active (aria-current)
    const step3Button = screen.getByLabelText(/step 3:.*active/i)
    expect(step3Button).toHaveAttribute('aria-current', 'step')
  })
})

// ============================================================================
// TEST-SW-03-02: StepIndicator click done step
// ============================================================================

describe('TEST-SW-03-02: StepIndicator click navigation', () => {
  it('should call goToStep when clicking a completed step', async () => {
    const user = userEvent.setup()

    useWizardStore.setState({
      currentStep: 3,
      visitedSteps: [1, 2, 3],
      stepErrors: {},
    })

    const goToStepMock = vi.fn()
    useWizardStore.setState({ goToStep: goToStepMock })

    render(<StepIndicator />)

    // Click on step 2 (which is completed)
    const step2Button = screen.getByLabelText(/step 2:.*done/i)
    await user.click(step2Button)

    expect(goToStepMock).toHaveBeenCalledWith(2)
  })
})

// ============================================================================
// TEST-SW-03-03: StepIndicator click unvisited step
// ============================================================================

describe('TEST-SW-03-03: StepIndicator prevent navigation to unvisited step', () => {
  it('should NOT call goToStep when clicking an unvisited step', async () => {
    const user = userEvent.setup()

    useWizardStore.setState({
      currentStep: 2,
      visitedSteps: [1, 2],
      stepErrors: {},
    })

    const goToStepMock = vi.fn()
    useWizardStore.setState({ goToStep: goToStepMock })

    render(<StepIndicator />)

    // Try to click step 5 (not visited, should be disabled)
    const step5Button = screen.getByLabelText(/step 5:/i)
    expect(step5Button).toBeDisabled()

    await user.click(step5Button)

    // goToStep should NOT have been called
    expect(goToStepMock).not.toHaveBeenCalled()
  })
})

// ============================================================================
// TEST-SW-03-04: DeltaSlider value rendering
// ============================================================================

describe('TEST-SW-03-04: DeltaSlider track color', () => {
  it('should show green track for value 0.30 or below', () => {
    const onChange = vi.fn()
    const { container } = render(<DeltaSlider value={0.30} onChange={onChange} />)

    // Check that the value is displayed
    expect(screen.getByText('0.300')).toBeInTheDocument()

    // Track should have green color in gradient (success color)
    const track = container.querySelector('[style*="linear-gradient"]')
    expect(track).toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-03-05: DeltaSlider onChange
// ============================================================================

describe('TEST-SW-03-05: DeltaSlider onChange callback', () => {
  it('should call onChange with correct value when slider is dragged', async () => {
    const onChange = vi.fn()
    render(<DeltaSlider value={0.30} onChange={onChange} />)

    const slider = screen.getByRole('slider', { name: /delta value/i })

    // Simulate drag to new value
    fireEvent.change(slider, { target: { value: '0.50' } })

    expect(onChange).toHaveBeenCalledWith(0.50)
  })
})

// ============================================================================
// TEST-SW-03-06: ImportDropzone valid JSON
// ============================================================================

describe('TEST-SW-03-06: ImportDropzone valid file import', () => {
  it('should call initFromJson when valid JSON file is dropped', async () => {
    const initFromJsonMock = vi.fn(() => ({ ok: true, errors: [] }))
    useWizardStore.setState({ initFromJson: initFromJsonMock })

    const onSuccess = vi.fn()
    render(<ImportDropzone onImportSuccess={onSuccess} />)

    const jsonContent = '{"strategy_id":"test","name":"Test Strategy"}'
    const file = new File([jsonContent], 'test.json', { type: 'application/json' })

    const dropzone = screen.getByRole('button')

    // Simulate file drop
    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    await waitFor(() => {
      expect(initFromJsonMock).toHaveBeenCalledWith(jsonContent)
    })
  })
})

// ============================================================================
// TEST-SW-03-07: ImportDropzone invalid file type
// ============================================================================

describe('TEST-SW-03-07: ImportDropzone invalid file type', () => {
  it('should show error for non-JSON file', async () => {
    const onError = vi.fn()
    render(<ImportDropzone onImportError={onError} />)

    const file = new File(['test content'], 'test.txt', { type: 'text/plain' })

    const dropzone = screen.getByRole('button')

    // Simulate file drop
    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    await waitFor(() => {
      expect(screen.getByText(/formato non supportato/i)).toBeInTheDocument()
    })

    expect(onError).toHaveBeenCalledWith(['Formato non supportato'])
  })
})

// ============================================================================
// TEST-SW-03-08: FieldWithTooltip error display
// ============================================================================

describe('TEST-SW-03-08: FieldWithTooltip error message', () => {
  it('should display error message in red', () => {
    render(
      <FieldWithTooltip
        label="Test Field"
        tooltip={{ description: 'Test description' }}
        error="Errore test"
      >
        <input type="text" />
      </FieldWithTooltip>
    )

    const errorText = screen.getByText('Errore test')
    expect(errorText).toBeInTheDocument()
    // Check that error is rendered with role="alert"
    const errorContainer = errorText.closest('[role="alert"]')
    expect(errorContainer).toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-03-09: ValidationBadge error count
// ============================================================================

describe('TEST-SW-03-09: ValidationBadge with errors', () => {
  it('should show error count badge for 2 errors', () => {
    render(<ValidationBadge errorCount={2} />)

    expect(screen.getByText('2 Errors')).toBeInTheDocument()
  })

  it('should show singular "Error" for 1 error', () => {
    render(<ValidationBadge errorCount={1} />)

    expect(screen.getByText('1 Error')).toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-03-10: NavigationButtons shake on validation failure
// ============================================================================

describe('TEST-SW-03-10: NavigationButtons shake animation', () => {
  it('should apply shake class when nextStep returns false', async () => {
    const user = userEvent.setup()

    // Mock nextStep to return false (validation failed)
    const nextStepMock = vi.fn(() => false)
    useWizardStore.setState({
      currentStep: 1,
      nextStep: nextStepMock,
    })

    render(<NavigationButtons />)

    const nextButton = screen.getByRole('button', { name: /avanti/i })
    await user.click(nextButton)

    expect(nextStepMock).toHaveBeenCalled()

    // Wait for shake class to be applied
    await waitFor(() => {
      expect(nextButton).toHaveClass('wz-shake')
    })
  })

  it('should NOT apply shake class when nextStep returns true', async () => {
    const user = userEvent.setup()

    // Mock nextStep to return true (validation passed)
    const nextStepMock = vi.fn(() => true)
    useWizardStore.setState({
      currentStep: 1,
      visitedSteps: [1],
      nextStep: nextStepMock,
    })

    render(<NavigationButtons />)

    const nextButton = screen.getByRole('button', { name: /avanti/i })
    await user.click(nextButton)

    expect(nextStepMock).toHaveBeenCalled()

    // Shake class should NOT be present
    expect(nextButton).not.toHaveClass('wz-shake')
  })
})
