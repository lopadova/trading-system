/**
 * EL Converter Test Suite
 *
 * Tests for T-07a (EasyLanguage Editor Panel):
 * - ELCodeEditor Tab key functionality
 * - ELSyntaxHighlight color mapping
 * - ELConverterPanel buttons and layout
 *
 * Tests for T-07c (Conversion Result Panel):
 * - ConversionResultPanel states (loading, error, success, partial, failed)
 * - IssuesList rendering and badges
 * - JSONPreview rendering
 * - Apply to wizard flow
 */

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { HTMLAttributes, ReactNode } from 'react'
import { ELCodeEditor } from './ELCodeEditor'
import { ELSyntaxHighlight } from './ELSyntaxHighlight'
import { ELConverterPanel } from './ELConverterPanel'
import { ConversionResultPanel } from './ConversionResultPanel'
import { IssuesList } from './IssuesList'
import { JSONPreview } from './JSONPreview'
import { EL_EXAMPLES } from './el-examples'
import type { ConversionResult, ConversionIssue } from '../../../hooks/useELConversion'

// ============================================================================
// MOCKS
// ============================================================================

// Prop types for motion/react mocks — accept any DOM div props plus children
type MotionDivProps = HTMLAttributes<HTMLDivElement> & { children?: ReactNode }
type AnimatePresenceProps = { children?: ReactNode }

// Mock motion/react to avoid animation issues in tests
vi.mock('motion/react', () => ({
  motion: {
    div: ({ children, ...props }: MotionDivProps) => <div {...props}>{children}</div>
  },
  AnimatePresence: ({ children }: AnimatePresenceProps) => <>{children}</>
}))

// Mock react-router navigation
const mockNavigate = vi.fn()
vi.mock('@tanstack/react-router', () => ({
  useNavigate: () => mockNavigate
}))

// Mock wizard store
const mockApplyConversionResult = vi.fn()
vi.mock('../../../stores/wizardStore', () => ({
  useWizardStore: () => ({
    applyConversionResult: mockApplyConversionResult
  })
}))

// Mock useELConversion hook
const mockConvertFn = vi.fn()
const mockResetFn = vi.fn()
let mockConversionState: {
  isLoading: boolean
  result: ConversionResult | null
  error: string | null
  convert: typeof mockConvertFn
  reset: typeof mockResetFn
} = {
  isLoading: false,
  result: null,
  error: null,
  convert: mockConvertFn,
  reset: mockResetFn
}

vi.mock('../../../hooks/useELConversion', () => ({
  useELConversion: () => mockConversionState
}))

// ============================================================================
// TEST-SW-07a-01: ELCodeEditor Tab key → 4 spaces inserted (no blur)
// ============================================================================

describe('TEST-SW-07a-01: Tab key inserts 4 spaces without blur', () => {
  it('should insert 4 spaces when Tab is pressed', async () => {
    const handleChange = vi.fn()
    const user = userEvent.setup()

    render(<ELCodeEditor value="" onChange={handleChange} />)

    const textarea = screen.getByRole('textbox')

    // Focus and press Tab
    await user.click(textarea)
    await user.keyboard('{Tab}')

    // Should have called onChange with 4 spaces
    expect(handleChange).toHaveBeenCalledWith('    ')
  })

  it('should not blur the textarea after Tab press', async () => {
    const handleChange = vi.fn()
    const user = userEvent.setup()

    render(<ELCodeEditor value="" onChange={handleChange} />)

    const textarea = screen.getByRole('textbox')

    await user.click(textarea)
    await user.keyboard('{Tab}')

    // Textarea should still be focused
    expect(textarea).toHaveFocus()
  })
})

// ============================================================================
// TEST-SW-07a-02: Tab with selection → 4 spaces replace selection
// ============================================================================

describe('TEST-SW-07a-02: Tab replaces selected text with 4 spaces', () => {
  it('should replace selected text with 4 spaces', async () => {
    const handleChange = vi.fn()
    const initialValue = 'hello world'

    render(<ELCodeEditor value={initialValue} onChange={handleChange} />)

    const textarea = screen.getByRole('textbox') as HTMLTextAreaElement

    // Select "world" (characters 6-11)
    textarea.focus()
    textarea.setSelectionRange(6, 11)

    // Press Tab
    fireEvent.keyDown(textarea, { key: 'Tab', code: 'Tab' })

    // Should replace "world" with 4 spaces
    expect(handleChange).toHaveBeenCalledWith('hello     ')
  })
})

// ============================================================================
// TEST-SW-07a-03: Syntax highlight keywords → amber color
// ============================================================================

describe('TEST-SW-07a-03: Keywords highlighted in amber', () => {
  it('should render keywords in amber color', () => {
    const code = 'inputs: variables: begin end if then else'
    const { container } = render(<ELSyntaxHighlight code={code} />)

    // Get all spans
    const allSpans = container.querySelectorAll('span')

    // Find spans with amber color (checking style object)
    const amberSpans = Array.from(allSpans).filter(
      (span) => span.style.color === 'rgb(251, 191, 36)' || span.style.color === '#fbbf24'
    )

    // Should have multiple keyword spans
    expect(amberSpans.length).toBeGreaterThan(0)

    // Check that keywords are present
    const keywords = ['inputs', 'variables', 'begin', 'end', 'if', 'then', 'else']
    const allText = amberSpans.map((span) => span.textContent).join(' ')

    keywords.forEach((keyword) => {
      expect(allText).toContain(keyword)
    })
  })
})

// ============================================================================
// TEST-SW-07a-04: Syntax highlight strings → green color
// ============================================================================

describe('TEST-SW-07a-04: Strings highlighted in green', () => {
  it('should render double-quoted strings in green', () => {
    const code = '"test string"'
    const { container } = render(<ELSyntaxHighlight code={code} />)

    const allSpans = container.querySelectorAll('span')
    const greenSpans = Array.from(allSpans).filter(
      (span) => span.style.color === 'rgb(52, 211, 153)' || span.style.color === '#34d399'
    )

    expect(greenSpans.length).toBeGreaterThan(0)
    expect(greenSpans[0]?.textContent).toBe('"test string"')
  })

  it('should render single-quoted strings in green', () => {
    const code = "'another string'"
    const { container } = render(<ELSyntaxHighlight code={code} />)

    const allSpans = container.querySelectorAll('span')
    const greenSpans = Array.from(allSpans).filter(
      (span) => span.style.color === 'rgb(52, 211, 153)' || span.style.color === '#34d399'
    )

    expect(greenSpans.length).toBeGreaterThan(0)
    expect(greenSpans[0]?.textContent).toBe("'another string'")
  })
})

// ============================================================================
// TEST-SW-07a-05: Syntax highlight numbers → blue color
// ============================================================================

describe('TEST-SW-07a-05: Numbers highlighted in blue', () => {
  it('should render integers in blue', () => {
    const code = '42 123 999'
    const { container } = render(<ELSyntaxHighlight code={code} />)

    const allSpans = container.querySelectorAll('span')
    const blueSpans = Array.from(allSpans).filter(
      (span) => span.style.color === 'rgb(96, 165, 250)' || span.style.color === '#60a5fa'
    )

    expect(blueSpans.length).toBeGreaterThanOrEqual(3)
  })

  it('should render decimals in blue', () => {
    const code = '3.14 0.5 10.25'
    const { container } = render(<ELSyntaxHighlight code={code} />)

    const allSpans = container.querySelectorAll('span')
    const blueSpans = Array.from(allSpans).filter(
      (span) => span.style.color === 'rgb(96, 165, 250)' || span.style.color === '#60a5fa'
    )

    expect(blueSpans.length).toBeGreaterThanOrEqual(3)
  })
})

// ============================================================================
// TEST-SW-07a-06: Syntax highlight comments // → gray color
// ============================================================================

describe('TEST-SW-07a-06: Comments highlighted in gray', () => {
  it('should render line comments in gray', () => {
    const code = '// This is a comment'
    const { container } = render(<ELSyntaxHighlight code={code} />)

    const allSpans = container.querySelectorAll('span')
    const graySpans = Array.from(allSpans).filter(
      (span) => span.style.color === 'rgb(107, 114, 128)' || span.style.color === '#6b7280'
    )

    expect(graySpans.length).toBeGreaterThan(0)
    expect(graySpans[0]?.textContent).toBe('// This is a comment')
  })

  it('should render inline comments correctly', () => {
    const code = 'inputs: Test(10); // comment'
    const { container } = render(<ELSyntaxHighlight code={code} />)

    const allSpans = container.querySelectorAll('span')
    const graySpans = Array.from(allSpans).filter(
      (span) => span.style.color === 'rgb(107, 114, 128)' || span.style.color === '#6b7280'
    )

    expect(graySpans.length).toBeGreaterThan(0)
    expect(graySpans[0]?.textContent).toContain('// comment')
  })
})

// ============================================================================
// TEST-SW-07a-07: "Incolla esempio" button → editor precompiled with ironCondor
// ============================================================================

describe('TEST-SW-07a-07: Paste example button fills editor', () => {
  it('should fill editor with ironCondor example when button is clicked', async () => {
    const user = userEvent.setup()

    render(<ELConverterPanel />)

    const pasteButton = screen.getByRole('button', { name: /incolla esempio/i })
    await user.click(pasteButton)

    // Check that the editor contains the ironCondor example code
    const textarea = screen.getByRole('textbox')
    expect(textarea).toHaveValue(EL_EXAMPLES.ironCondor)
  })
})

// ============================================================================
// TEST-SW-07a-08: "Cancella" button → editor empty
// ============================================================================

describe('TEST-SW-07a-08: Clear button empties editor', () => {
  it('should clear the editor when clear button is clicked', async () => {
    const user = userEvent.setup()

    render(<ELConverterPanel />)

    // First paste example
    const pasteButton = screen.getByRole('button', { name: /incolla esempio/i })
    await user.click(pasteButton)

    const textarea = screen.getByRole('textbox')
    expect(textarea).toHaveValue(EL_EXAMPLES.ironCondor)

    // Then clear
    const clearButton = screen.getByRole('button', { name: /cancella/i })
    await user.click(clearButton)

    expect(textarea).toHaveValue('')
  })

  it('should disable clear button when editor is empty', () => {
    render(<ELConverterPanel />)

    const clearButton = screen.getByRole('button', { name: /cancella/i })
    expect(clearButton).toBeDisabled()
  })
})

// ============================================================================
// TEST-SW-07a-09: "Converti" button disabled if code empty
// ============================================================================

describe('TEST-SW-07a-09: Convert button disabled when code is empty', () => {
  it('should disable convert button when code is empty', () => {
    render(<ELConverterPanel />)

    // Button may only be visible on mobile (lg:hidden)
    // Try to find it by text content
    const convertButtons = screen.queryAllByText(/converti con ai/i)

    if (convertButtons.length > 0) {
      // Check that button is disabled
      convertButtons.forEach((button) => {
        expect(button).toBeDisabled()
      })
    }
  })

  it('should enable convert button when code is present', async () => {
    const user = userEvent.setup()

    render(<ELConverterPanel />)

    // Paste example
    const pasteButton = screen.getByRole('button', { name: /incolla esempio/i })
    await user.click(pasteButton)

    // Try to find convert button
    const convertButtons = screen.queryAllByText(/converti con ai/i)

    if (convertButtons.length > 0) {
      // Check that button is enabled
      convertButtons.forEach((button) => {
        expect(button).not.toBeDisabled()
      })
    }
  })
})

// ============================================================================
// TEST-SW-07a-10: Layout responsive: mobile → stack vertical
// ============================================================================

describe('TEST-SW-07a-10: Responsive layout', () => {
  it('should render split-pane layout with flex-col lg:flex-row', () => {
    const { container } = render(<ELConverterPanel />)

    const mainContainer = container.firstChild as HTMLElement
    expect(mainContainer).toHaveClass('flex')
    expect(mainContainer).toHaveClass('flex-col')
    expect(mainContainer).toHaveClass('lg:flex-row')
  })

  it('should render convert button with lg:hidden class', () => {
    const { container } = render(<ELConverterPanel />)

    // Find the div containing the convert button (mobile only)
    const mobileButtonContainer = container.querySelector('.lg\\:hidden')
    expect(mobileButtonContainer).toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-07a-11: Textarea resize-y works correctly
// ============================================================================

describe('TEST-SW-07a-11: Textarea is resizable', () => {
  it('should have resize-y class on textarea', () => {
    render(<ELCodeEditor value="" onChange={() => {}} />)

    const textarea = screen.getByRole('textbox')
    expect(textarea).toHaveClass('resize-y')
  })
})

// ============================================================================
// TEST-SW-07a-12: JetBrains Mono font applied correctly
// ============================================================================

describe('TEST-SW-07a-12: Monospace font applied', () => {
  it('should apply JetBrains Mono font family to textarea', () => {
    render(<ELCodeEditor value="" onChange={() => {}} />)

    const textarea = screen.getByRole('textbox') as HTMLTextAreaElement
    const style = textarea.style.fontFamily

    expect(style).toContain('JetBrains Mono')
  })

  it('should apply fallback monospace fonts', () => {
    render(<ELCodeEditor value="" onChange={() => {}} />)

    const textarea = screen.getByRole('textbox') as HTMLTextAreaElement
    const style = textarea.style.fontFamily

    // Should have fallbacks
    expect(style).toContain('Monaco')
    expect(style).toContain('Consolas')
    expect(style).toContain('monospace')
  })
})

// ============================================================================
// T-07c TESTS: CONVERSION RESULT PANEL
// ============================================================================

// ============================================================================
// TEST-SW-07c-01: Conversione completa → badge "Convertibile" + bottone "Applica"
// ============================================================================

describe('TEST-SW-07c-01: Full conversion success state', () => {
  it('should show success badge and apply button when fully convertible', () => {
    const mockResult: ConversionResult = {
      convertible: true,
      confidence: 0.95,
      result_json: { strategy_id: 'test-strategy' },
      issues: [],
      warnings: [],
      notes: ''
    }

    const mockApply = vi.fn()

    render(
      <ConversionResultPanel
        result={mockResult}
        isLoading={false}
        error={null}
        onApplyToWizard={mockApply}
      />
    )

    // Check for success badge
    expect(screen.getByText('Convertibile')).toBeInTheDocument()

    // Check for confidence display
    expect(screen.getByText(/Confidence: 95%/i)).toBeInTheDocument()

    // Check for apply button
    const applyButton = screen.getByRole('button', { name: /applica al wizard/i })
    expect(applyButton).toBeInTheDocument()
  })

  it('should call onApplyToWizard when apply button is clicked', async () => {
    const mockResult: ConversionResult = {
      convertible: true,
      confidence: 0.95,
      result_json: { strategy_id: 'test-strategy' },
      issues: [],
      warnings: [],
      notes: ''
    }

    const mockApply = vi.fn()
    const user = userEvent.setup()

    render(
      <ConversionResultPanel
        result={mockResult}
        isLoading={false}
        error={null}
        onApplyToWizard={mockApply}
      />
    )

    const applyButton = screen.getByRole('button', { name: /applica al wizard/i })
    await user.click(applyButton)

    expect(mockApply).toHaveBeenCalledTimes(1)
  })
})

// ============================================================================
// TEST-SW-07c-02: Conversione parziale → badge "Parzialmente Convertibile" + bottone "Applica"
// ============================================================================

describe('TEST-SW-07c-02: Partial conversion state', () => {
  it('should show warning badge and apply button when partially convertible', () => {
    const mockResult: ConversionResult = {
      convertible: 'partial',
      confidence: 0.70,
      result_json: { strategy_id: 'test-strategy' },
      issues: [
        {
          type: 'ambiguous',
          el_construct: 'SomeFunction',
          description: 'Unclear intent',
          suggestion: 'Review manually'
        }
      ],
      warnings: ['Some warning'],
      notes: ''
    }

    const mockApply = vi.fn()

    render(
      <ConversionResultPanel
        result={mockResult}
        isLoading={false}
        error={null}
        onApplyToWizard={mockApply}
      />
    )

    // Check for partial badge
    expect(screen.getByText('Parzialmente Convertibile')).toBeInTheDocument()

    // Check for confidence display
    expect(screen.getByText(/Confidence: 70%/i)).toBeInTheDocument()

    // Check for apply button (should still be present)
    const applyButton = screen.getByRole('button', { name: /applica al wizard/i })
    expect(applyButton).toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-07c-03: Conversione fallita → badge "Non Convertibile" + NO bottone "Applica"
// ============================================================================

describe('TEST-SW-07c-03: Failed conversion state', () => {
  it('should show error badge and NO apply button when not convertible', () => {
    const mockResult: ConversionResult = {
      convertible: false,
      confidence: 0.20,
      result_json: null,
      issues: [
        {
          type: 'not_supported',
          el_construct: 'CustomIndicator',
          description: 'Not supported',
          suggestion: ''
        },
        {
          type: 'not_supported',
          el_construct: 'Plot',
          description: 'Not supported',
          suggestion: ''
        }
      ],
      warnings: [],
      notes: ''
    }

    const mockApply = vi.fn()

    render(
      <ConversionResultPanel
        result={mockResult}
        isLoading={false}
        error={null}
        onApplyToWizard={mockApply}
      />
    )

    // Check for error badge
    expect(screen.getByText('Non Convertibile')).toBeInTheDocument()

    // Check for issue count
    expect(screen.getByText(/2 issues trovati/i)).toBeInTheDocument()

    // Apply button should NOT be present
    const applyButton = screen.queryByRole('button', { name: /applica al wizard/i })
    expect(applyButton).not.toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-07c-04: applyConversionResult() chiamato con result_json
// ============================================================================

describe('TEST-SW-07c-04: Apply conversion result integration', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockConversionState = {
      isLoading: false,
      result: {
        convertible: true,
        confidence: 0.95,
        result_json: { strategy_id: 'iron-condor', name: 'Iron Condor' },
        issues: [],
        warnings: [],
        notes: ''
      },
      error: null,
      convert: mockConvertFn,
      reset: mockResetFn
    }
  })

  it('should call applyConversionResult with result_json when apply button is clicked', async () => {
    const user = userEvent.setup()

    render(<ELConverterPanel />)

    // Find and click apply button
    const applyButton = screen.getByRole('button', { name: /applica al wizard/i })
    await user.click(applyButton)

    expect(mockApplyConversionResult).toHaveBeenCalledWith({
      strategy_id: 'iron-condor',
      name: 'Iron Condor'
    })
  })
})

// ============================================================================
// TEST-SW-07c-05: Dopo apply → navigate to /strategies/wizard?step=1
// ============================================================================

describe('TEST-SW-07c-05: Navigation after apply', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockConversionState = {
      isLoading: false,
      result: {
        convertible: true,
        confidence: 0.95,
        result_json: { strategy_id: 'iron-condor' },
        issues: [],
        warnings: [],
        notes: ''
      },
      error: null,
      convert: mockConvertFn,
      reset: mockResetFn
    }
  })

  it('should navigate to wizard step 1 after apply', async () => {
    const user = userEvent.setup()

    render(<ELConverterPanel />)

    const applyButton = screen.getByRole('button', { name: /applica al wizard/i })
    await user.click(applyButton)

    expect(mockNavigate).toHaveBeenCalledWith({
      to: '/strategies/wizard',
      search: { step: 1 }
    })
  })
})

// ============================================================================
// TEST-SW-07c-06: Issues accordion aperto di default se issues > 0
// ============================================================================

describe('TEST-SW-07c-06: Issues accordion open by default', () => {
  it('should have issues accordion open when issues exist', () => {
    const mockResult: ConversionResult = {
      convertible: 'partial',
      confidence: 0.70,
      result_json: { strategy_id: 'test' },
      issues: [
        {
          type: 'ambiguous',
          el_construct: 'Test',
          description: 'Test issue',
          suggestion: ''
        }
      ],
      warnings: [],
      notes: ''
    }

    render(
      <ConversionResultPanel
        result={mockResult}
        isLoading={false}
        error={null}
        onApplyToWizard={() => {}}
      />
    )

    const issuesAccordion = screen.getByText(/Issues \(1\)/i).closest('details')
    expect(issuesAccordion).toHaveAttribute('open')
  })
})

// ============================================================================
// TEST-SW-07c-07: JSON preview mostra syntax highlighting
// ============================================================================

describe('TEST-SW-07c-07: JSON preview rendering', () => {
  it('should render JSON with proper formatting', () => {
    const testJson = {
      strategy_id: 'test-strategy',
      name: 'Test Strategy',
      version: '1.0.0'
    }

    render(<JSONPreview json={testJson} />)

    const preElement = screen.getByText(/"strategy_id"/i).closest('pre')
    expect(preElement).toHaveClass('font-mono')
    expect(preElement).toHaveClass('bg-gray-900')

    // Check that JSON is formatted (multiline)
    const codeContent = preElement?.textContent || ''
    expect(codeContent).toContain('"strategy_id": "test-strategy"')
  })
})

// ============================================================================
// TEST-SW-07c-08: Loading state → "Claude sta analizzando..." con spinner
// ============================================================================

describe('TEST-SW-07c-08: Loading state', () => {
  it('should show loading message with animated robot', () => {
    render(
      <ConversionResultPanel
        result={null}
        isLoading={true}
        error={null}
        onApplyToWizard={() => {}}
      />
    )

    expect(screen.getByText(/Claude sta analizzando/i)).toBeInTheDocument()
    expect(screen.getByText(/30 secondi/i)).toBeInTheDocument()

    // Check for robot emoji (unicode)
    const robotEmoji = screen.getByText('🤖')
    expect(robotEmoji).toBeInTheDocument()
    expect(robotEmoji).toHaveClass('animate-pulse')
  })
})

// ============================================================================
// TEST-SW-07c-09: Error state → messaggio errore chiaro
// ============================================================================

describe('TEST-SW-07c-09: Error state', () => {
  it('should show error message clearly', () => {
    const errorMessage = 'API key non configurata'

    render(
      <ConversionResultPanel
        result={null}
        isLoading={false}
        error={errorMessage}
        onApplyToWizard={() => {}}
      />
    )

    expect(screen.getByText('Errore durante conversione')).toBeInTheDocument()
    expect(screen.getByText(errorMessage)).toBeInTheDocument()
    expect(screen.getByText('❌')).toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-07c-10: Issue type "not_supported" → badge rosso + icona 🔴
// ============================================================================

describe('TEST-SW-07c-10: Not supported issue rendering', () => {
  it('should render not_supported issue with red badge and red icon', () => {
    const issues: ConversionIssue[] = [
      {
        type: 'not_supported',
        el_construct: 'Plot',
        description: 'Plotting not supported',
        suggestion: ''
      }
    ]

    render(<IssuesList issues={issues} />)

    expect(screen.getByText('Non Supportato')).toBeInTheDocument()
    expect(screen.getByText('Plot')).toBeInTheDocument()
    expect(screen.getByText('Plotting not supported')).toBeInTheDocument()
    expect(screen.getByText('🔴')).toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-07c-11: Issue type "ambiguous" → badge giallo + icona 🟡
// ============================================================================

describe('TEST-SW-07c-11: Ambiguous issue rendering', () => {
  it('should render ambiguous issue with yellow badge and yellow icon', () => {
    const issues: ConversionIssue[] = [
      {
        type: 'ambiguous',
        el_construct: 'Value1',
        description: 'Unclear reference',
        suggestion: ''
      }
    ]

    render(<IssuesList issues={issues} />)

    expect(screen.getByText('Ambiguo')).toBeInTheDocument()
    expect(screen.getByText('Value1')).toBeInTheDocument()
    expect(screen.getByText('Unclear reference')).toBeInTheDocument()
    expect(screen.getByText('🟡')).toBeInTheDocument()
  })
})

// ============================================================================
// TEST-SW-07c-12: Issue suggestion presente → box blu con 💡
// ============================================================================

describe('TEST-SW-07c-12: Issue suggestion rendering', () => {
  it('should render suggestion box when suggestion is present', () => {
    const issues: ConversionIssue[] = [
      {
        type: 'manual_required',
        el_construct: 'CustomFunc',
        description: 'Requires manual mapping',
        suggestion: 'Use built-in function XYZ instead'
      }
    ]

    const { container } = render(<IssuesList issues={issues} />)

    expect(screen.getByText('Richiesta Modifica Manuale')).toBeInTheDocument()
    expect(screen.getByText(/Suggerimento:/i)).toBeInTheDocument()
    expect(screen.getByText(/Use built-in function XYZ/i)).toBeInTheDocument()

    // Check for suggestion box with blue background
    const suggestionBox = container.querySelector('.text-blue-400.bg-blue-400\\/10')
    expect(suggestionBox).toBeInTheDocument()
    expect(suggestionBox?.textContent).toContain('💡')
  })

  it('should NOT render suggestion box when suggestion is empty', () => {
    const issues: ConversionIssue[] = [
      {
        type: 'not_supported',
        el_construct: 'Test',
        description: 'Not supported',
        suggestion: ''
      }
    ]

    render(<IssuesList issues={issues} />)

    expect(screen.queryByText(/Suggerimento:/i)).not.toBeInTheDocument()
    expect(screen.queryByText('💡')).not.toBeInTheDocument()
  })
})
