# Dashboard Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the "Trading System Design System" kit into the live React 19 dashboard, wire every widget to real Cloudflare Worker endpoints, and deliver a dark+light operator console matching the kit pixel-close.

**Architecture:** Hybrid Tailwind 4 + CSS custom properties. `src/index.css` owns the design tokens; `@theme` exposes them as Tailwind utilities. Primitives in `components/ui/`, widgets in `components/dashboard/` + `components/positions/`. React Query hooks talk to a Hono-based Worker at `/api/*`. Motion (Framer successor) for entrance stagger, Recharts for multi-series/drawdown charts, inline SVG for donut + monthly grid, Zustand for asset/theme stores.

**Tech Stack:** React 19 · TypeScript strict · Vite 8 · Tailwind 4 · Recharts 3 · lucide-react · motion · TanStack Query · Zustand · ky · vitest + @testing-library/react (dashboard); Hono · vitest (worker).

**Reference spec:** `docs/superpowers/specs/2026-04-20-dashboard-redesign-design.md`

**Design kit (read-only reference):** `docs/design-kit/extracted/trading-system-design-system/`

---

## File Map (locked decomposition)

### Dashboard (`dashboard/src/`)
- `index.css` — REWRITE. Design tokens, Tailwind `@theme`, base styles, keyframes.
- `types/performance.ts`, `drawdown.ts`, `risk.ts`, `system.ts`, `activity.ts`, `breakdown.ts` — NEW.
- `types/position.ts` — EXTEND with `campaign` field.
- `stores/assetFilterStore.ts`, `themeStore.ts` — NEW.
- `hooks/usePerformanceSummary.ts`, `usePerformanceSeries.ts`, `useDrawdowns.ts`, `useMonthlyReturns.ts`, `useRiskMetrics.ts`, `useRecentActivity.ts`, `useAlertsSummary.ts`, `useCampaignsSummary.ts`, `usePositionsBreakdown.ts` — NEW.
- `hooks/useSystemStatus.ts` — REWRITE hook signature (now returns `SystemMetricsSample`).
- `lib/chart-utils.ts`, `utils/format.ts` — NEW.
- `components/ui/Card.tsx`, `Badge.tsx`, `Button.tsx` — REWRITE.
- `components/ui/StatCard.tsx`, `Spinner.tsx`, `Skeleton.tsx`, `FilterOverlay.tsx`, `SegmentedControl.tsx` — NEW.
- `components/layout/Sidebar.tsx`, `Header.tsx`, `Layout.tsx` — REWRITE.
- `components/dashboard/*` — NEW directory: AssetFilter, SummaryCard, MultiSeriesChart, DrawdownsSection, MonthlyPerfSection, RiskMetricsCard, AlertsMiniCard, SystemPerfMini, RecentActivity, ActivePositionsTable.
- `components/positions/PositionsBreakdown.tsx`, `PositionsKpiStrip.tsx`, `PositionsFilterBar.tsx`, `Donut.tsx` — NEW.
- `components/positions/PositionsTable.tsx`, `PositionCard.tsx`, `PositionsSummary.tsx`, `PositionFilters.tsx` — REWRITE (replace existing).
- `pages/HomePage.tsx`, `pages/PositionsPage.tsx` — FULL REWRITE.
- `pages/AlertsPage.tsx`, `CampaignsPage.tsx`, `SettingsPage.tsx` — light restyle.
- `styles/wizard.css` — DELETE (contents move into `.wizard-root` in `index.css`).

### Worker (`infra/cloudflare/worker/src/`)
- `routes/performance.ts`, `drawdowns.ts`, `monthly-returns.ts`, `risk.ts`, `system-metrics.ts`, `breakdown.ts`, `activity.ts`, `campaigns-summary.ts` — NEW.
- `routes/positions.ts` — EXTEND (campaign join).
- `routes/alerts.ts` — EXTEND (add `/summary-24h`).
- `index.ts` — UPDATE (mount new routes, extend root endpoint list).
- `test/*` — NEW test files per route.

---

## Phase 1 — Design System Foundation

**Checkpoint goal:** Dashboard still builds; pages look flat (no glass) but keep current content; Wizard still amber.

### Task 1.1: Rewrite `dashboard/src/index.css` with kit tokens

**Files:**
- Modify: `dashboard/src/index.css` (full rewrite)

- [ ] **Step 1: Replace the entire file**

```css
@import "tailwindcss";
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=DM+Sans:wght@400;500;600;700&family=Space+Mono:wght@400;700&family=JetBrains+Mono:wght@400;500;600&display=swap');

/* ============================================================================
   DESIGN TOKENS — dark theme (default)
   ========================================================================== */
:root,
:root[data-theme='dark'] {
  --bg-1: #0d1117;
  --bg-2: #161b22;
  --bg-3: #21262d;
  --bg-inset: #010409;

  --border-1: #30363d;
  --border-2: #21262d;
  --border-focus: #2f81f7;

  --fg-1: #e6edf3;
  --fg-2: #7d8590;
  --fg-3: #484f58;
  --fg-inverse: #ffffff;

  --blue: #2f81f7;
  --blue-2: #1f6feb;
  --accent: var(--blue);

  --green: #3fb950;
  --green-2: #238636;
  --red: #f85149;
  --red-2: #da3633;
  --yellow: #d29922;
  --purple: #a371f7;

  --tint-blue: rgba(47, 129, 247, 0.15);
  --tint-green: rgba(63, 185, 80, 0.15);
  --tint-red: rgba(248, 81, 73, 0.15);
  --tint-yellow: rgba(210, 153, 34, 0.15);
  --tint-purple: rgba(163, 113, 247, 0.15);
  --tint-muted: rgba(125, 133, 144, 0.15);

  --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.3);
  --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.3);
  --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.4);
  --shadow-ring: 0 0 0 1px var(--border-1);

  --radius-sm: 4px;
  --radius-md: 6px;
  --radius-lg: 8px;
  --radius-xl: 12px;
  --radius-pill: 999px;

  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-5: 24px;
  --space-6: 32px;
  --space-7: 48px;
  --space-8: 64px;

  --font-sans: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
  --font-display: 'DM Sans', 'Inter', sans-serif;
  --font-mono: 'JetBrains Mono', 'Space Mono', ui-monospace, monospace;

  --text-xs: 11px;
  --text-sm: 12px;
  --text-body: 14px;
  --text-md: 16px;
  --text-lg: 18px;
  --text-xl: 20px;
  --text-2xl: 24px;
  --text-3xl: 30px;
  --text-4xl: 36px;

  --dur-fast: 120ms;
  --dur-normal: 180ms;
  --dur-slow: 280ms;
  --ease-out: cubic-bezier(0.4, 0, 0.2, 1);
}

:root[data-theme='light'] {
  --bg-1: #ffffff;
  --bg-2: #f6f8fa;
  --bg-3: #eaeef2;
  --bg-inset: #f6f8fa;

  --border-1: #d0d7de;
  --border-2: #d8dee4;

  --fg-1: #1f2328;
  --fg-2: #59636e;
  --fg-3: #818b98;

  --blue: #0969da;
  --blue-2: #0550ae;
  --green: #1a7f37;
  --green-2: #116329;
  --red: #cf222e;
  --red-2: #a40e26;
  --yellow: #9a6700;

  --tint-blue: rgba(9, 105, 218, 0.08);
  --tint-green: rgba(26, 127, 55, 0.08);
  --tint-red: rgba(207, 34, 46, 0.08);
  --tint-yellow: rgba(154, 103, 0, 0.08);
  --tint-purple: rgba(163, 113, 247, 0.08);

  --shadow-sm: 0 1px 2px 0 rgba(27, 31, 36, 0.08);
  --shadow-md: 0 3px 6px rgba(140, 149, 159, 0.15);
  --shadow-lg: 0 8px 24px rgba(140, 149, 159, 0.20);
}

/* Wizard sub-theme — applied to <div class="wizard-root"> */
.wizard-root {
  --bg-1: #0a0a0f;
  --bg-2: #12121a;
  --bg-3: #1a1a26;
  --border-1: #1e2433;
  --accent: #f59e0b;
  --accent-dim: rgba(245, 158, 11, 0.12);
  --accent-glow: rgba(245, 158, 11, 0.24);
  --fg-1: #f1f5f9;
  --fg-2: #64748b;
  --font-display: 'Space Mono', monospace;
  --font-sans: 'DM Sans', sans-serif;
  --shadow-accent: 0 0 20px rgba(245, 158, 11, 0.3);
}

/* ============================================================================
   TAILWIND 4 THEME — utility class generation
   ========================================================================== */
@theme {
  --color-bg: var(--bg-1);
  --color-surface: var(--bg-2);
  --color-surface-2: var(--bg-3);
  --color-surface-inset: var(--bg-inset);
  --color-fg: var(--fg-1);
  --color-muted: var(--fg-2);
  --color-subtle: var(--fg-3);
  --color-border: var(--border-1);
  --color-border-subtle: var(--border-2);
  --color-accent: var(--blue);
  --color-up: var(--green);
  --color-down: var(--red);
  --color-warn: var(--yellow);
  --color-info: var(--purple);
  --color-danger: var(--red);
  --color-success: var(--green);
  --color-warning: var(--yellow);

  --font-sans: 'Inter', sans-serif;
  --font-display: 'DM Sans', sans-serif;
  --font-mono: 'JetBrains Mono', monospace;

  --radius-card: 8px;
  --radius-pill: 999px;
}

/* ============================================================================
   BASE
   ========================================================================== */
* { box-sizing: border-box; }
html, body { margin: 0; padding: 0; }

body {
  font-family: var(--font-sans);
  font-size: var(--text-body);
  line-height: 1.5;
  color: var(--fg-1);
  background: var(--bg-1);
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

/* Scrollbar */
::-webkit-scrollbar { width: 8px; height: 8px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb { background: var(--border-1); border-radius: 4px; }
::-webkit-scrollbar-thumb:hover { background: var(--fg-3); }

/* ============================================================================
   ANIMATIONS
   ========================================================================== */
@keyframes pulseDot {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}
@keyframes spin { to { transform: rotate(360deg); } }
@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}
@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}
@keyframes fadeUp {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

.pulse-dot { animation: pulseDot 2s cubic-bezier(0.4, 0, 0.6, 1) infinite; }

/* ============================================================================
   SEMANTIC HELPERS
   ========================================================================== */
.num, .tabular { font-variant-numeric: tabular-nums; font-feature-settings: "tnum"; }
.text-up { color: var(--green); }
.text-down { color: var(--red); }
.text-muted { color: var(--fg-2); }
.text-subtle { color: var(--fg-3); }
.text-accent { color: var(--blue); }
.overline {
  font-size: var(--text-xs);
  line-height: 1.4;
  color: var(--fg-2);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-weight: 500;
}
```

- [ ] **Step 2: Run typecheck + build**

Run: `cd dashboard && npm run typecheck && npm run build`
Expected: Both succeed. Build will show visual differences but no errors.

- [ ] **Step 3: Commit**

```bash
git add dashboard/src/index.css
git commit -m "feat(dashboard): adopt kit design tokens in index.css

Replace glassmorphism CSS with Trading System Design System tokens.
Dark/light themes via data-theme attribute. Wizard sub-theme scoped to .wizard-root.
Tailwind 4 @theme block exposes tokens as utility classes."
```

### Task 1.2: Harmonize Strategy Wizard with `.wizard-root` wrapper

**Files:**
- Modify: `dashboard/src/pages/StrategyWizardPage.tsx`
- Modify: `dashboard/src/pages/StrategyImportPage.tsx`
- Modify: `dashboard/src/pages/StrategyConvertPage.tsx`
- Modify: `dashboard/src/pages/trading/strategies/StrategyWizardPage.tsx`

- [ ] **Step 1: Wrap each wizard page root JSX in `<div className="wizard-root">`**

In each of the 4 files above, find the root returned JSX element and wrap it. Example pattern:

```tsx
// BEFORE
return (
  <div className="max-w-7xl mx-auto">
    {/* ...existing content... */}
  </div>
)

// AFTER
return (
  <div className="wizard-root">
    <div className="max-w-7xl mx-auto">
      {/* ...existing content... */}
    </div>
  </div>
)
```

- [ ] **Step 2: Delete `dashboard/src/styles/wizard.css`**

Run: `rm dashboard/src/styles/wizard.css`

- [ ] **Step 3: Remove any `import './styles/wizard.css'` references**

Run: `grep -rn "wizard.css" dashboard/src` — remove each import found.

- [ ] **Step 4: Run typecheck + wizard tests**

Run: `cd dashboard && npm run typecheck && npm test -- wizard`
Expected: Typecheck passes. Wizard tests pass.

- [ ] **Step 5: Commit**

```bash
git add dashboard/src
git commit -m "refactor(dashboard): move wizard styles into .wizard-root token override

Wrap wizard pages in <div class=wizard-root>; delete standalone wizard.css.
Amber sub-theme now centralized in index.css."
```

### Task 1.3: Rewrite `Card` primitive (TDD)

**Files:**
- Create: `dashboard/src/components/ui/Card.test.tsx`
- Modify: `dashboard/src/components/ui/Card.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// dashboard/src/components/ui/Card.test.tsx
import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { Card, CardContent } from './Card'

describe('Card', () => {
  it('renders children inside a bordered surface', () => {
    render(<Card data-testid="card">hello</Card>)
    const card = screen.getByTestId('card')
    expect(card).toHaveTextContent('hello')
    expect(card.className).toMatch(/bg-surface/)
    expect(card.className).toMatch(/border/)
    expect(card.className).toMatch(/rounded-card/)
  })

  it('accepts padding=0 for flush layouts', () => {
    render(<Card data-testid="card" padding={0}>x</Card>)
    expect(screen.getByTestId('card').className).toMatch(/p-0/)
  })

  it('CardContent renders inside a padded block', () => {
    render(<CardContent data-testid="cc">y</CardContent>)
    expect(screen.getByTestId('cc')).toHaveTextContent('y')
  })
})
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd dashboard && npm test -- Card.test`
Expected: FAIL (Card component signature does not match).

- [ ] **Step 3: Rewrite `Card.tsx`**

```tsx
// dashboard/src/components/ui/Card.tsx
import { forwardRef, type HTMLAttributes } from 'react'
import { cn } from '../../utils/cn'

type CardPadding = 0 | 16 | 20 | 24

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  padding?: CardPadding
}

const paddingClass: Record<CardPadding, string> = {
  0: 'p-0',
  16: 'p-4',
  20: 'p-5',
  24: 'p-6',
}

export const Card = forwardRef<HTMLDivElement, CardProps>(
  ({ padding = 20, className, children, ...rest }, ref) => (
    <div
      ref={ref}
      className={cn(
        'bg-surface border border-border rounded-card',
        paddingClass[padding],
        className
      )}
      {...rest}
    >
      {children}
    </div>
  )
)
Card.displayName = 'Card'

export const CardContent = forwardRef<HTMLDivElement, HTMLAttributes<HTMLDivElement>>(
  ({ className, children, ...rest }, ref) => (
    <div ref={ref} className={cn('p-5', className)} {...rest}>
      {children}
    </div>
  )
)
CardContent.displayName = 'CardContent'
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd dashboard && npm test -- Card.test`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add dashboard/src/components/ui/Card.tsx dashboard/src/components/ui/Card.test.tsx
git commit -m "refactor(dashboard): rewrite Card primitive to kit tokens

Bg-surface + border + rounded-card. padding prop accepts 0/16/20/24.
Removes glassmorphism, backdrop-filter, gradient overlays."
```

### Task 1.4: Rewrite `Badge` primitive (TDD)

**Files:**
- Create: `dashboard/src/components/ui/Badge.test.tsx`
- Modify: `dashboard/src/components/ui/Badge.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// dashboard/src/components/ui/Badge.test.tsx
import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { Badge } from './Badge'

describe('Badge', () => {
  it('renders green tone', () => {
    render(<Badge tone="green">OPERATIONAL</Badge>)
    const el = screen.getByText('OPERATIONAL')
    expect(el.className).toMatch(/text-\[var\(--green\)\]|text-up/)
  })

  it('renders pulse dot when pulse=true', () => {
    render(<Badge tone="green" pulse data-testid="b">LIVE</Badge>)
    const el = screen.getByTestId('b')
    expect(el.querySelector('.pulse-dot')).toBeTruthy()
  })

  it('small size has compact padding', () => {
    render(<Badge tone="muted" size="sm" data-testid="b">sm</Badge>)
    expect(screen.getByTestId('b').className).toMatch(/px-2|text-\[10\.5px\]|text-xs/)
  })
})
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd dashboard && npm test -- Badge.test`

- [ ] **Step 3: Rewrite `Badge.tsx`**

```tsx
// dashboard/src/components/ui/Badge.tsx
import type { HTMLAttributes } from 'react'
import { cn } from '../../utils/cn'

export type BadgeTone = 'green' | 'red' | 'yellow' | 'blue' | 'purple' | 'muted'
export type BadgeSize = 'sm' | 'md'

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone
  size?: BadgeSize
  pulse?: boolean
}

const toneStyles: Record<BadgeTone, string> = {
  green: 'bg-[var(--tint-green)] text-[var(--green)] border-[color:var(--green)]/25',
  red: 'bg-[var(--tint-red)] text-[var(--red)] border-[color:var(--red)]/25',
  yellow: 'bg-[var(--tint-yellow)] text-[var(--yellow)] border-[color:var(--yellow)]/25',
  blue: 'bg-[var(--tint-blue)] text-[var(--blue)] border-[color:var(--blue)]/25',
  purple: 'bg-[var(--tint-purple)] text-[var(--purple)] border-[color:var(--purple)]/25',
  muted: 'bg-[var(--tint-muted)] text-[var(--fg-2)] border-[color:var(--fg-2)]/25',
}

const dotColor: Record<BadgeTone, string> = {
  green: 'bg-[var(--green)]',
  red: 'bg-[var(--red)]',
  yellow: 'bg-[var(--yellow)]',
  blue: 'bg-[var(--blue)]',
  purple: 'bg-[var(--purple)]',
  muted: 'bg-[var(--fg-2)]',
}

export function Badge({ tone = 'muted', size = 'md', pulse, className, children, ...rest }: BadgeProps) {
  const padding = size === 'sm' ? 'px-[7px] py-[1px] text-[10.5px]' : 'px-2.5 py-0.5 text-[11px]'
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-pill font-semibold tracking-wide whitespace-nowrap border',
        toneStyles[tone],
        padding,
        className
      )}
      {...rest}
    >
      {pulse && <span className={cn('w-1.5 h-1.5 rounded-full pulse-dot', dotColor[tone])} />}
      {children}
    </span>
  )
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd dashboard && npm test -- Badge.test`

- [ ] **Step 5: Commit**

```bash
git add dashboard/src/components/ui/Badge.tsx dashboard/src/components/ui/Badge.test.tsx
git commit -m "refactor(dashboard): rewrite Badge primitive to kit tones

6 tones (green/red/yellow/blue/purple/muted), sm/md sizes, optional pulse dot."
```

### Task 1.5: Rewrite `Button` primitive (TDD)

**Files:**
- Create: `dashboard/src/components/ui/Button.test.tsx`
- Modify: `dashboard/src/components/ui/Button.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// dashboard/src/components/ui/Button.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { Button } from './Button'
import { Plus } from 'lucide-react'

describe('Button', () => {
  it('renders children', () => {
    render(<Button>Apply</Button>)
    expect(screen.getByText('Apply')).toBeInTheDocument()
  })

  it('renders icon when provided', () => {
    render(<Button icon={Plus} data-testid="b">New</Button>)
    expect(screen.getByTestId('b').querySelector('svg')).toBeTruthy()
  })

  it('calls onClick', () => {
    const fn = vi.fn()
    render(<Button onClick={fn}>go</Button>)
    fireEvent.click(screen.getByText('go'))
    expect(fn).toHaveBeenCalledOnce()
  })

  it('disabled=true blocks clicks', () => {
    const fn = vi.fn()
    render(<Button onClick={fn} disabled>no</Button>)
    fireEvent.click(screen.getByText('no'))
    expect(fn).not.toHaveBeenCalled()
  })

  it('loading shows spinner', () => {
    render(<Button loading data-testid="b">x</Button>)
    expect(screen.getByTestId('b').querySelector('[data-spinner]')).toBeTruthy()
  })
})
```

- [ ] **Step 2: Run — expect FAIL**

Run: `cd dashboard && npm test -- Button.test`

- [ ] **Step 3: Rewrite `Button.tsx`**

```tsx
// dashboard/src/components/ui/Button.tsx
import { forwardRef, type ButtonHTMLAttributes } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '../../utils/cn'

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger'
export type ButtonSize = 'sm' | 'md' | 'lg'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
  icon?: LucideIcon
  loading?: boolean
}

const variantClass: Record<ButtonVariant, string> = {
  primary: 'bg-[var(--blue)] text-white border-transparent hover:brightness-110',
  secondary: 'bg-[var(--bg-3)] text-[var(--fg-1)] border-[var(--border-1)] hover:brightness-110',
  ghost: 'bg-transparent text-[var(--fg-1)] border-transparent hover:bg-[var(--bg-3)]',
  danger: 'bg-[var(--red)] text-white border-transparent hover:brightness-110',
}

const sizeClass: Record<ButtonSize, string> = {
  sm: 'px-2.5 py-1 text-[12px] gap-1.5',
  md: 'px-3.5 py-1.5 text-[13px] gap-1.5',
  lg: 'px-4.5 py-2.5 text-[14px] gap-2',
}

const iconSize: Record<ButtonSize, number> = { sm: 12, md: 14, lg: 16 }

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = 'primary', size = 'md', icon: Icon, loading, disabled, className, children, ...rest }, ref) => {
    const isDisabled = disabled || loading
    return (
      <button
        ref={ref}
        disabled={isDisabled}
        className={cn(
          'inline-flex items-center justify-center rounded-md border font-medium whitespace-nowrap transition-[filter,background-color,border-color] duration-150',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--border-focus)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--bg-1)]',
          'disabled:opacity-50 disabled:cursor-not-allowed',
          variantClass[variant],
          sizeClass[size],
          className
        )}
        {...rest}
      >
        {loading ? (
          <svg data-spinner width={iconSize[size]} height={iconSize[size]} viewBox="0 0 24 24" fill="none" className="animate-spin">
            <circle cx="12" cy="12" r="9" stroke="currentColor" strokeOpacity=".2" strokeWidth="3" />
            <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
          </svg>
        ) : Icon ? (
          <Icon size={iconSize[size]} />
        ) : null}
        {children}
      </button>
    )
  }
)
Button.displayName = 'Button'
```

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add dashboard/src/components/ui/Button.tsx dashboard/src/components/ui/Button.test.tsx
git commit -m "refactor(dashboard): rewrite Button primitive

4 variants x 3 sizes, optional Lucide icon, loading spinner, accessible focus ring."
```

### Task 1.6: Remove glassmorphism class usages across pages

**Files:**
- Modify: any file using `card-clean`, `metric-value`, `positive-glow`, `negative-glow`, `badge badge-green`, `badge badge-red`, `badge badge-yellow`, `badge badge-blue`

- [ ] **Step 1: Find all usages**

Run: `cd dashboard && grep -rn "card-clean\|metric-value\|positive-glow\|negative-glow\|badge-green\|badge-red\|badge-yellow\|badge-blue" src`

- [ ] **Step 2: Replace each with `<Card>` / `<Badge>` or plain Tailwind**

Replacement rules:
- `<div className="card-clean p-6">` → `<Card padding={24}>`
- `<div className="metric-value ...">` → `<div className="font-mono font-bold tabular-nums">`
- `text-green-400 positive-glow` → `text-up`
- `text-red-400 negative-glow` → `text-down`
- `<span className="badge badge-green">X</span>` → `<Badge tone="green">X</Badge>`
- `badge-red` → tone="red", `badge-yellow` → tone="yellow", `badge-blue` → tone="blue"

- [ ] **Step 3: Run typecheck + full test suite**

Run: `cd dashboard && npm run typecheck && npm test`
Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add dashboard/src
git commit -m "refactor(dashboard): replace glassmorphism classes with kit primitives

Migrate card-clean/metric-value/glow utility classes to <Card>/<Badge>/Tailwind tokens."
```

### Task 1.7: Phase 1 checkpoint build & smoke

- [ ] **Step 1: Full build**

Run: `cd dashboard && npm run typecheck && npm run lint && npm run build`
Expected: all pass.

- [ ] **Step 2: Dev server smoke**

Run: `cd dashboard && npm run dev`
Open browser: pages load with flat dark look, wizard still amber, no visual crashes. `Ctrl+C` to stop.

- [ ] **Step 3: No extra commit** — nothing changed.

---

(continues in next file — this plan is split into two parts for readability. See `2026-04-20-dashboard-redesign-part2.md` for Phases 2–5.)
