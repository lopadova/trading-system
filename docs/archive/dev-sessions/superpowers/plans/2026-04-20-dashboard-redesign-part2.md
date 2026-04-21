# Dashboard Redesign — Implementation Plan (Part 2: Phases 2–3)

> Continuation of `2026-04-20-dashboard-redesign.md`. Same execution rules.

---

## Phase 2 — Primitives + Layout + Theme

**Checkpoint:** All pages re-chromed; sidebar/header match kit; theme toggle works.

### Task 2.1: Create `SegmentedControl` primitive (TDD)

**Files:**
- Create: `dashboard/src/components/ui/SegmentedControl.test.tsx`
- Create: `dashboard/src/components/ui/SegmentedControl.tsx`

- [ ] **Step 1: Test**

```tsx
// SegmentedControl.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { SegmentedControl } from './SegmentedControl'

describe('SegmentedControl', () => {
  it('renders options and marks selected', () => {
    render(<SegmentedControl<string> value="a" onChange={() => {}} options={[{ value: 'a', label: 'A' }, { value: 'b', label: 'B' }]} />)
    expect(screen.getByRole('button', { name: 'A' }).getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('button', { name: 'B' }).getAttribute('aria-pressed')).toBe('false')
  })
  it('calls onChange on click', () => {
    const fn = vi.fn()
    render(<SegmentedControl<string> value="a" onChange={fn} options={[{ value: 'a', label: 'A' }, { value: 'b', label: 'B' }]} />)
    fireEvent.click(screen.getByRole('button', { name: 'B' }))
    expect(fn).toHaveBeenCalledWith('b')
  })
  it('does not call onChange when locked', () => {
    const fn = vi.fn()
    render(<SegmentedControl<string> value="a" onChange={fn} options={[{ value: 'a', label: 'A' }, { value: 'b', label: 'B', locked: true }]} />)
    fireEvent.click(screen.getByRole('button', { name: 'B' }))
    expect(fn).not.toHaveBeenCalled()
  })
})
```

- [ ] **Step 2: Run — FAIL**: `cd dashboard && npm test -- SegmentedControl.test`

- [ ] **Step 3: Implement**

```tsx
// SegmentedControl.tsx
import { cn } from '../../utils/cn'
import { Lock } from 'lucide-react'

export interface SegmentOption<T extends string> {
  value: T
  label: string
  locked?: boolean
}

export interface SegmentedControlProps<T extends string> {
  value: T
  onChange: (value: T) => void
  options: SegmentOption<T>[]
  size?: 'sm' | 'md'
  className?: string
}

export function SegmentedControl<T extends string>({ value, onChange, options, size = 'md', className }: SegmentedControlProps<T>) {
  const pad = size === 'sm' ? 'px-2.5 py-0.5 text-[11px]' : 'px-3 py-1 text-[12px]'
  return (
    <div className={cn('inline-flex gap-0.5 p-[3px] bg-[var(--bg-1)] border border-border rounded-md', className)}>
      {options.map(opt => {
        const on = opt.value === value
        return (
          <button
            key={opt.value}
            type="button"
            aria-pressed={on}
            disabled={opt.locked}
            onClick={() => !opt.locked && onChange(opt.value)}
            className={cn(
              'inline-flex items-center gap-1 rounded font-medium transition-colors',
              pad,
              on ? 'bg-[var(--bg-3)] text-[var(--fg-1)] font-semibold' : 'text-[var(--fg-2)] hover:text-[var(--fg-1)]',
              opt.locked && 'opacity-60 cursor-not-allowed'
            )}
          >
            {opt.locked && <Lock size={10} />}
            {opt.label}
          </button>
        )
      })}
    </div>
  )
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/components/ui/SegmentedControl* && git commit -m "feat(dashboard): SegmentedControl primitive"`

### Task 2.2: Create `StatCard` primitive

**Files:** Create `dashboard/src/components/ui/StatCard.test.tsx` and `StatCard.tsx`.

- [ ] **Step 1: Test**

```tsx
// StatCard.test.tsx
import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { StatCard } from './StatCard'
import { DollarSign } from 'lucide-react'

describe('StatCard', () => {
  it('renders label, value, delta', () => {
    render(<StatCard label="Account Value" value="$125,430.50" delta="↑ +$2,340.80" deltaTone="green" />)
    expect(screen.getByText('Account Value')).toBeInTheDocument()
    expect(screen.getByText('$125,430.50')).toBeInTheDocument()
    expect(screen.getByText('↑ +$2,340.80')).toBeInTheDocument()
  })
  it('renders icon', () => {
    render(<StatCard label="x" value="1" icon={DollarSign} data-testid="s" />)
    expect(screen.getByTestId('s').querySelector('svg')).toBeTruthy()
  })
  it('renders status slot instead of value when given', () => {
    render(<StatCard label="x" value="" status={{ tone: 'green', label: 'OPERATIONAL' }} />)
    expect(screen.getByText('OPERATIONAL')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```tsx
// StatCard.tsx
import type { LucideIcon } from 'lucide-react'
import type { HTMLAttributes } from 'react'
import { Card } from './Card'
import { Badge, type BadgeTone } from './Badge'
import { cn } from '../../utils/cn'

export type DeltaTone = 'green' | 'red' | 'yellow' | 'muted'

export interface StatCardProps extends HTMLAttributes<HTMLDivElement> {
  label: string
  value: string
  delta?: string
  deltaTone?: DeltaTone
  icon?: LucideIcon
  status?: { tone: BadgeTone; label: string }
}

const deltaColor: Record<DeltaTone, string> = {
  green: 'text-up',
  red: 'text-down',
  yellow: 'text-[var(--yellow)]',
  muted: 'text-muted',
}

export function StatCard({ label, value, delta, deltaTone = 'muted', icon: Icon, status, className, ...rest }: StatCardProps) {
  return (
    <Card className={cn('cursor-default', className)} {...rest}>
      <div className="flex items-center justify-between mb-3">
        <span className="text-[12px] text-muted font-medium">{label}</span>
        {Icon && <Icon size={16} className="text-subtle" />}
      </div>
      {status ? (
        <div className="mb-2"><Badge tone={status.tone}>{status.label}</Badge></div>
      ) : (
        <div className="text-[26px] font-semibold text-[var(--fg-1)] mb-1.5 tabular-nums tracking-tight">{value}</div>
      )}
      {delta && (
        <div className={cn('text-[12px] font-medium', deltaColor[deltaTone])}>{delta}</div>
      )}
    </Card>
  )
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/components/ui/StatCard* && git commit -m "feat(dashboard): StatCard primitive"`

### Task 2.3: Create `Spinner`, `Skeleton`, `FilterOverlay`

**Files:** Create `Spinner.tsx`, `Skeleton.tsx`, `FilterOverlay.tsx` (+ one shared test file).

- [ ] **Step 1: Test**

```tsx
// dashboard/src/components/ui/LoadingPrimitives.test.tsx
import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { Spinner } from './Spinner'
import { Skeleton } from './Skeleton'
import { FilterOverlay } from './FilterOverlay'

describe('Spinner', () => {
  it('renders SVG', () => {
    render(<Spinner data-testid="s" />)
    expect(screen.getByTestId('s').tagName.toLowerCase()).toBe('svg')
  })
})
describe('Skeleton', () => {
  it('renders a shimmer element', () => {
    render(<Skeleton data-testid="k" />)
    expect(screen.getByTestId('k').className).toMatch(/animate-|shimmer/)
  })
})
describe('FilterOverlay', () => {
  it('renders label when visible', () => {
    render(<FilterOverlay visible label="Loading…" />)
    expect(screen.getByText('Loading…')).toBeInTheDocument()
  })
  it('renders nothing when not visible', () => {
    const { container } = render(<FilterOverlay visible={false} label="x" />)
    expect(container.firstChild).toBeNull()
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```tsx
// Spinner.tsx
import type { SVGAttributes } from 'react'
export interface SpinnerProps extends SVGAttributes<SVGSVGElement> {
  size?: number
  color?: string
}
export function Spinner({ size = 14, color = 'currentColor', ...rest }: SpinnerProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" className="animate-spin shrink-0" {...rest}>
      <circle cx="12" cy="12" r="9" stroke={color} strokeOpacity=".2" strokeWidth="3" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke={color} strokeWidth="3" strokeLinecap="round" />
    </svg>
  )
}
```

```tsx
// Skeleton.tsx
import type { HTMLAttributes } from 'react'
import { cn } from '../../utils/cn'
export interface SkeletonProps extends HTMLAttributes<HTMLDivElement> {
  w?: string | number
  h?: number
  radius?: number
}
export function Skeleton({ w = '100%', h = 14, radius = 4, className, style, ...rest }: SkeletonProps) {
  return (
    <div
      className={cn('bg-[linear-gradient(90deg,#21262d_0%,#2d333b_50%,#21262d_100%)] bg-[length:200%_100%]', className)}
      style={{ width: w, height: h, borderRadius: radius, animation: 'shimmer 1.4s infinite', ...style }}
      {...rest}
    />
  )
}
```

```tsx
// FilterOverlay.tsx
import { Spinner } from './Spinner'
export interface FilterOverlayProps {
  visible: boolean
  label?: string
}
export function FilterOverlay({ visible, label = 'Loading…' }: FilterOverlayProps) {
  if (!visible) return null
  return (
    <div
      className="absolute inset-0 flex items-center justify-center gap-2.5 z-10"
      style={{ background: 'rgba(13,17,23,0.72)', backdropFilter: 'blur(2px)', animation: 'fadeIn 140ms ease-out' }}
    >
      <Spinner size={16} color="var(--blue)" />
      <span className="text-[12.5px] text-muted font-medium">{label}</span>
    </div>
  )
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/components/ui/{Spinner,Skeleton,FilterOverlay,LoadingPrimitives.test}.tsx && git commit -m "feat(dashboard): Spinner, Skeleton, FilterOverlay primitives"`

### Task 2.4: Create `themeStore` with localStorage persistence (TDD)

**Files:**
- Create: `dashboard/src/stores/themeStore.test.ts`
- Create: `dashboard/src/stores/themeStore.ts`

- [ ] **Step 1: Test**

```ts
// themeStore.test.ts
import { describe, it, expect, beforeEach } from 'vitest'
import { useThemeStore } from './themeStore'

describe('themeStore', () => {
  beforeEach(() => {
    localStorage.clear()
    document.documentElement.removeAttribute('data-theme')
    useThemeStore.setState({ theme: 'dark' })
  })
  it('defaults to dark', () => {
    expect(useThemeStore.getState().theme).toBe('dark')
  })
  it('toggles to light and persists', () => {
    useThemeStore.getState().toggle()
    expect(useThemeStore.getState().theme).toBe('light')
    expect(localStorage.getItem('ts-theme')).toBe('light')
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })
  it('setTheme applies value', () => {
    useThemeStore.getState().setTheme('light')
    expect(useThemeStore.getState().theme).toBe('light')
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```ts
// themeStore.ts
import { create } from 'zustand'

export type Theme = 'dark' | 'light'

interface ThemeState {
  theme: Theme
  setTheme: (theme: Theme) => void
  toggle: () => void
}

const STORAGE_KEY = 'ts-theme'

function applyTheme(theme: Theme) {
  document.documentElement.setAttribute('data-theme', theme)
  try { localStorage.setItem(STORAGE_KEY, theme) } catch { /* ignore quota */ }
}

function readInitial(): Theme {
  try {
    const v = localStorage.getItem(STORAGE_KEY)
    if (v === 'light' || v === 'dark') return v
  } catch { /* ignore */ }
  return 'dark'
}

export const useThemeStore = create<ThemeState>((set, get) => ({
  theme: readInitial(),
  setTheme: theme => { applyTheme(theme); set({ theme }) },
  toggle: () => { const next: Theme = get().theme === 'dark' ? 'light' : 'dark'; applyTheme(next); set({ theme: next }) },
}))

// Ensure attribute matches store on module load
applyTheme(readInitial())
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/stores/themeStore* && git commit -m "feat(dashboard): themeStore with dark/light toggle + localStorage"`

### Task 2.5: Create `assetFilterStore` (TDD)

**Files:**
- Create: `dashboard/src/stores/assetFilterStore.test.ts`
- Create: `dashboard/src/stores/assetFilterStore.ts`

- [ ] **Step 1: Test**

```ts
import { describe, it, expect, beforeEach } from 'vitest'
import { useAssetFilterStore } from './assetFilterStore'

describe('assetFilterStore', () => {
  beforeEach(() => useAssetFilterStore.setState({ asset: 'all' }))
  it('defaults to all', () => {
    expect(useAssetFilterStore.getState().asset).toBe('all')
  })
  it('setAsset changes value', () => {
    useAssetFilterStore.getState().setAsset('options')
    expect(useAssetFilterStore.getState().asset).toBe('options')
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```ts
// assetFilterStore.ts
import { create } from 'zustand'

export type AssetBucket = 'all' | 'systematic' | 'options' | 'other'

interface AssetFilterState {
  asset: AssetBucket
  setAsset: (asset: AssetBucket) => void
}

export const useAssetFilterStore = create<AssetFilterState>((set) => ({
  asset: 'all',
  setAsset: asset => set({ asset }),
}))
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/stores/assetFilterStore* && git commit -m "feat(dashboard): assetFilterStore"`

### Task 2.6: Rewrite `Sidebar` layout component

**Files:** Modify `dashboard/src/components/layout/Sidebar.tsx`. Base on kit `components.jsx` Sidebar.

- [ ] **Step 1: Replace file contents**

```tsx
// dashboard/src/components/layout/Sidebar.tsx
import {
  Home, Activity, TrendingUp, FolderKanban, BarChart3,
  AlertCircle, FileText, Settings as SettingsIcon, Sparkles
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '../../utils/cn'

interface NavItem {
  path: string
  icon: LucideIcon
  label: string
  badge?: number
}

const items: NavItem[] = [
  { path: '/', icon: Home, label: 'Overview' },
  { path: '/health', icon: Activity, label: 'System Health' },
  { path: '/positions', icon: TrendingUp, label: 'Positions' },
  { path: '/campaigns', icon: FolderKanban, label: 'Campaigns' },
  { path: '/ivts', icon: BarChart3, label: 'IVTS Monitor' },
  { path: '/alerts', icon: AlertCircle, label: 'Alerts' },
  { path: '/logs', icon: FileText, label: 'Logs' },
  { path: '/strategies/new', icon: Sparkles, label: 'Strategy Wizard' },
  { path: '/settings', icon: SettingsIcon, label: 'Settings' },
]

interface SidebarProps {
  currentPath: string
}

function Logo({ size = 22 }: { size?: number }) {
  return (
    <svg width={size} height={size * 28 / 30} viewBox="0 0 30 28" fill="none" aria-label="Trading System logo">
      <path d="M19 2 L5 15 L13 15 L11 26 L25 13 L17 13 L19 2 Z" fill="var(--purple)" stroke="var(--purple)" strokeWidth="1.2" strokeLinejoin="round" />
    </svg>
  )
}

export function Sidebar({ currentPath }: SidebarProps) {
  return (
    <aside className="w-60 bg-[var(--bg-1)] border-r border-border flex flex-col h-screen shrink-0">
      <div className="flex items-center gap-2.5 h-16 px-4 border-b border-border">
        <Logo />
        <div>
          <div className="font-display font-bold text-[14px] tracking-tight text-[var(--fg-1)]">Trading System</div>
          <div className="text-[10.5px] text-muted font-mono">dashboard · v0.1.0</div>
        </div>
      </div>
      <nav className="flex-1 p-2.5 overflow-auto flex flex-col gap-0.5">
        {items.map(it => {
          const active = currentPath === it.path || (it.path === '/strategies/new' && currentPath.startsWith('/strategies'))
          return (
            <a
              key={it.path}
              href={it.path}
              className={cn(
                'flex items-center gap-2.5 px-2.5 py-1.5 rounded-md text-[13px] transition-colors',
                active ? 'bg-[var(--tint-blue)] text-[var(--blue)] font-medium' : 'text-[var(--fg-1)] hover:bg-[color:var(--fg-2)]/10'
              )}
            >
              <it.icon size={16} className={active ? 'text-[var(--blue)]' : 'text-muted'} />
              <span className="flex-1">{it.label}</span>
              {it.badge != null && (
                <span className="bg-[var(--red)] text-white text-[10px] font-semibold px-1.5 py-[1px] rounded-full">{it.badge}</span>
              )}
            </a>
          )
        })}
      </nav>
      <div className="border-t border-border px-4 py-3 text-[11px] text-muted flex items-center justify-between">
        <div>
          <div>Paper Trading</div>
          <div className="font-mono">IBKR · Connected</div>
        </div>
        <span className="w-2 h-2 rounded-full bg-[var(--green)] pulse-dot" style={{ boxShadow: '0 0 6px var(--green)' }} />
      </div>
    </aside>
  )
}
```

- [ ] **Step 2: Run typecheck**: `cd dashboard && npm run typecheck`
- [ ] **Step 3: Commit**: `git add dashboard/src/components/layout/Sidebar.tsx && git commit -m "refactor(dashboard): kit-compliant Sidebar"`

### Task 2.7: Rewrite `Header` with theme toggle

**Files:** Modify `dashboard/src/components/layout/Header.tsx`.

- [ ] **Step 1: Replace contents**

```tsx
// dashboard/src/components/layout/Header.tsx
import { Bell, Sun, Moon } from 'lucide-react'
import { Badge } from '../ui/Badge'
import { useThemeStore } from '../../stores/themeStore'

interface HeaderProps {
  title: string
  subtitle?: string
}

export function Header({ title, subtitle }: HeaderProps) {
  const theme = useThemeStore(s => s.theme)
  const toggle = useThemeStore(s => s.toggle)
  return (
    <header className="h-16 border-b border-border bg-[var(--bg-1)] flex items-center justify-between px-8 shrink-0">
      <div>
        <h1 className="font-display text-[20px] font-semibold text-[var(--fg-1)] m-0 tracking-tight">{title}</h1>
        {subtitle && <div className="text-[12px] text-muted mt-0.5">{subtitle}</div>}
      </div>
      <div className="flex items-center gap-3">
        <Badge tone="green" pulse>OPERATIONAL</Badge>
        <div className="w-px h-6 bg-border" />
        <button
          type="button"
          aria-label="Notifications"
          className="w-8 h-8 rounded-md border border-border flex items-center justify-center text-muted hover:text-[var(--fg-1)] transition-colors"
        >
          <Bell size={15} />
        </button>
        <button
          type="button"
          aria-label={theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
          onClick={toggle}
          className="w-8 h-8 rounded-md border border-border flex items-center justify-center text-muted hover:text-[var(--fg-1)] transition-colors"
        >
          {theme === 'dark' ? <Sun size={15} /> : <Moon size={15} />}
        </button>
        <div className="w-8 h-8 rounded-full bg-[var(--blue)] text-white flex items-center justify-center text-[12px] font-semibold">LP</div>
      </div>
    </header>
  )
}
```

- [ ] **Step 2: Typecheck**: `cd dashboard && npm run typecheck`
- [ ] **Step 3: Commit**: `git add dashboard/src/components/layout/Header.tsx && git commit -m "refactor(dashboard): kit-compliant Header with theme toggle"`

### Task 2.8: Rewrite `Layout` shell

**Files:** Modify `dashboard/src/components/layout/Layout.tsx`.

- [ ] **Step 1: Replace contents**

```tsx
// dashboard/src/components/layout/Layout.tsx
import type { ReactNode } from 'react'
import { Sidebar } from './Sidebar'
import { Header } from './Header'

interface LayoutProps {
  children: ReactNode
  currentPath: string
  title: string
  subtitle?: string
}

export function Layout({ children, currentPath, title, subtitle }: LayoutProps) {
  return (
    <div className="flex h-screen">
      <Sidebar currentPath={currentPath} />
      <div className="flex-1 flex flex-col min-w-0 bg-[var(--bg-1)]">
        <Header title={title} subtitle={subtitle} />
        <main className="flex-1 overflow-auto">{children}</main>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Update `App.tsx` to pass `currentPath`, `title`, `subtitle` to Layout**

In `dashboard/src/App.tsx`, replace the `renderPage()` consumer:

```tsx
// Map route -> header copy
const headerCopy: Record<string, { title: string; subtitle?: string }> = {
  '/': { title: 'Overview', subtitle: 'Account performance · live positions · open risk' },
  '/positions': { title: 'Positions', subtitle: 'Open and closed contract positions' },
  '/campaigns': { title: 'Campaigns', subtitle: 'Strategy campaigns · running and paused' },
  '/alerts': { title: 'Alerts', subtitle: 'Active warnings and system events' },
  '/health': { title: 'System Health', subtitle: 'Workers, queues, and data-feed telemetry' },
  '/ivts': { title: 'IVTS Monitor', subtitle: 'Implied volatility term structure' },
  '/logs': { title: 'Logs', subtitle: 'Service and worker log stream' },
  '/settings': { title: 'Settings', subtitle: 'Account, execution, and integrations' },
  '/strategies/new': { title: 'Strategy Wizard', subtitle: 'Author a new trading strategy' },
  '/strategies/import': { title: 'Import Strategy', subtitle: 'Import from SDF JSON' },
  '/strategies/convert': { title: 'Convert EL', subtitle: 'TradeStation EL → SDF converter' },
}

// Inside component body, after computing currentRoute:
const copy = headerCopy[currentRoute] ?? { title: 'Trading System' }

// Render:
return (
  <>
    <Layout currentPath={currentRoute} title={copy.title} subtitle={copy.subtitle}>
      {renderPage()}
    </Layout>
    <ToastContainer />
  </>
)
```

- [ ] **Step 3: Typecheck + test**: `cd dashboard && npm run typecheck && npm test`
- [ ] **Step 4: Commit**: `git add dashboard/src/components/layout/Layout.tsx dashboard/src/App.tsx && git commit -m "refactor(dashboard): wire Layout with route-driven header copy"`

### Task 2.9: Phase 2 smoke test

- [ ] **Step 1: Build + dev**: `cd dashboard && npm run build && npm run dev`
- [ ] **Step 2: Open browser**: pages load, sidebar correct, header shows title/subtitle/theme toggle, click Moon/Sun — palette flips. Ctrl+C.
- [ ] **Step 3: No commit** — already committed.

---

## Phase 3 — Worker API Extensions

**Checkpoint:** Every new endpoint returns valid JSON, passes vitest integration tests, CORS OK.

All routes use Hono. Pattern (copy from existing `routes/positions.ts`):

```ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
export const myRoute = new Hono<{ Bindings: Env }>()
myRoute.get('/', async (c) => { /* ... */ return c.json({ /* shape */ }) })
```

### Task 3.1: Add `types/api.ts` shared types in worker

**Files:** Create `infra/cloudflare/worker/src/types/api.ts`.

- [ ] **Step 1: Create file**

```ts
// infra/cloudflare/worker/src/types/api.ts
export type AssetBucket = 'all' | 'systematic' | 'options' | 'other'
export type PerfRange = '1W' | '1M' | '3M' | 'YTD' | '1Y' | 'ALL'
export type DrawdownRange = 'Max' | '10Y' | '5Y' | '1Y' | 'YTD' | '6M'

export interface SummaryData {
  asset: AssetBucket
  m: number; ytd: number; y2: number; y5: number; y10: number; ann: number
  base: number
}

export interface PerfSeries {
  asset: AssetBucket; range: PerfRange
  portfolio: number[]; sp500: number[]; swda: number[]
  startDate: string; endDate: string
}

export interface WorstDrawdown { depthPct: number; start: string; end: string; months: number }
export interface DrawdownsResponse {
  asset: AssetBucket; range: DrawdownRange
  portfolioSeries: number[]; sp500Series: number[]
  worst: WorstDrawdown[]
}

export interface MonthlyReturnsResponse {
  asset: AssetBucket
  years: Record<string, (number | null)[]>
  totals: Record<string, number>
}

export interface RiskMetrics {
  vix: number | null; vix1d: number | null
  delta: number; theta: number; vega: number
  ivRankSpy: number | null; buyingPower: number; marginUsedPct: number
}

export interface SystemMetricsSample {
  cpu: number[]; ram: number[]; network: number[]
  diskUsedPct: number; diskFreeGb: number; diskTotalGb: number
  asOf: string
}

export interface ExposureSegment { label: string; value: number; color: string }
export interface PositionsBreakdownResponse {
  byStrategy: ExposureSegment[]; byAsset: ExposureSegment[]
}

export type ActivityIcon = 'check-circle-2' | 'alert-triangle' | 'play' | 'x-circle' | 'repeat' | 'trending-up' | 'refresh-cw' | 'file-text'
export type ActivityTone = 'green' | 'red' | 'yellow' | 'blue' | 'purple' | 'muted'
export interface ActivityEvent {
  id: string; icon: ActivityIcon; tone: ActivityTone
  title: string; subtitle: string; timestamp: string
}
export interface ActivityResponse { events: ActivityEvent[] }

export interface AlertsSummary { total: number; critical: number; warning: number; info: number }
export interface CampaignsSummary { active: number; paused: number; draft: number; detail: string }
```

- [ ] **Step 2: Commit**: `git add infra/cloudflare/worker/src/types/api.ts && git commit -m "feat(worker): shared API types"`

### Task 3.2: Add `routes/performance.ts` (summary + series)

**Files:**
- Create: `infra/cloudflare/worker/src/routes/performance.ts`
- Create: `infra/cloudflare/worker/test/performance.test.ts`

- [ ] **Step 1: Test**

```ts
// infra/cloudflare/worker/test/performance.test.ts
import { describe, it, expect } from 'vitest'
import { performance as perfRoute } from '../src/routes/performance'

describe('performance routes', () => {
  it('GET /summary returns 6 horizons for all asset bucket', async () => {
    const res = await perfRoute.request('/summary?asset=all')
    expect(res.status).toBe(200)
    const body = await res.json() as any
    expect(body.asset).toBe('all')
    expect(typeof body.m).toBe('number')
    expect(typeof body.base).toBe('number')
  })
  it('defaults asset=all when query missing', async () => {
    const res = await perfRoute.request('/summary')
    const body = await res.json() as any
    expect(body.asset).toBe('all')
  })
  it('GET /series returns 3 arrays with same length', async () => {
    const res = await perfRoute.request('/series?asset=options&range=1M')
    const body = await res.json() as any
    expect(body.portfolio.length).toBe(body.sp500.length)
    expect(body.portfolio.length).toBe(body.swda.length)
    expect(body.range).toBe('1M')
  })
  it('rejects invalid asset', async () => {
    const res = await perfRoute.request('/summary?asset=bogus')
    expect(res.status).toBe(400)
  })
})
```

- [ ] **Step 2: Run — FAIL**: `cd infra/cloudflare/worker && bun test performance`

- [ ] **Step 3: Implement**

```ts
// infra/cloudflare/worker/src/routes/performance.ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { AssetBucket, PerfRange, SummaryData, PerfSeries } from '../types/api'

const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']
const RANGES: PerfRange[] = ['1W', '1M', '3M', 'YTD', '1Y', 'ALL']

const SUMMARY: Record<AssetBucket, SummaryData> = {
  all:        { asset: 'all',        m: 14.30, ytd: 15.04, y2: 49.88, y5: 100.13, y10: 98.59,  ann: 13.07, base: 125430 },
  systematic: { asset: 'systematic', m:  8.20, ytd:  9.64, y2: 31.40, y5:  74.80, y10: 82.10, ann: 11.22, base:  72500 },
  options:    { asset: 'options',    m: 22.80, ytd: 24.10, y2: 68.10, y5: 128.40, y10: 118.30, ann: 15.31, base:  38900 },
  other:      { asset: 'other',      m:  3.10, ytd:  4.28, y2: 12.60, y5:  28.90, y10: 42.40, ann:  5.64, base:  14030 },
}

function parseAsset(raw: string | undefined): AssetBucket | null {
  if (!raw) return 'all'
  return ASSETS.includes(raw as AssetBucket) ? (raw as AssetBucket) : null
}

function parseRange(raw: string | undefined): PerfRange | null {
  if (!raw) return '1M'
  return RANGES.includes(raw as PerfRange) ? (raw as PerfRange) : null
}

// Deterministic synthetic series per asset; caller crops by range
function generateSeries(asset: AssetBucket): PerfSeries['portfolio'] {
  const N = 60
  const growth: Record<AssetBucket, number> = { all: 0.65, systematic: 0.30, options: 1.30, other: 0.10 }
  const base = 100
  const gPerStep = growth[asset] / N
  return Array.from({ length: N }, (_, i) => +(base * (1 + gPerStep * i)).toFixed(2))
}

const SP500 = Array.from({ length: 60 }, (_, i) => +(100 + i * 0.3).toFixed(2))
const SWDA = Array.from({ length: 60 }, (_, i) => +(100 + i * 0.29).toFixed(2))

const CROP: Record<PerfRange, number> = { '1W': 7, '1M': 20, '3M': 42, YTD: 50, '1Y': 60, ALL: 60 }

export const performance = new Hono<{ Bindings: Env }>()

performance.get('/summary', c => {
  const asset = parseAsset(c.req.query('asset') ?? undefined)
  if (!asset) return c.json({ error: 'invalid asset' }, 400)
  return c.json(SUMMARY[asset])
})

performance.get('/series', c => {
  const asset = parseAsset(c.req.query('asset') ?? undefined)
  const range = parseRange(c.req.query('range') ?? undefined)
  if (!asset || !range) return c.json({ error: 'invalid query' }, 400)

  const portfolioFull = generateSeries(asset)
  const crop = CROP[range]
  const N = portfolioFull.length
  const portfolio = portfolioFull.slice(N - crop)
  const sp500 = SP500.slice(N - crop)
  const swda = SWDA.slice(N - crop)
  const endDate = new Date('2026-04-20')
  const startDate = new Date(endDate); startDate.setDate(startDate.getDate() - crop)
  const payload: PerfSeries = {
    asset, range, portfolio, sp500, swda,
    startDate: startDate.toISOString(), endDate: endDate.toISOString(),
  }
  return c.json(payload)
})
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add infra/cloudflare/worker/src/routes/performance.ts infra/cloudflare/worker/test/performance.test.ts && git commit -m "feat(worker): performance/summary and performance/series endpoints"`

### Task 3.3: Add `routes/drawdowns.ts`

**Files:** Create route + test.

- [ ] **Step 1: Test**

```ts
// test/drawdowns.test.ts
import { describe, it, expect } from 'vitest'
import { drawdowns } from '../src/routes/drawdowns'
describe('drawdowns', () => {
  it('returns series + worst list', async () => {
    const res = await drawdowns.request('/?asset=all&range=10Y')
    expect(res.status).toBe(200)
    const body = await res.json() as any
    expect(body.portfolioSeries.length).toBeGreaterThan(0)
    expect(body.sp500Series.length).toBe(body.portfolioSeries.length)
    expect(Array.isArray(body.worst)).toBe(true)
    expect(body.worst[0]).toHaveProperty('depthPct')
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```ts
// routes/drawdowns.ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { AssetBucket, DrawdownRange, DrawdownsResponse, WorstDrawdown } from '../types/api'

const PORT = [0,-1.2,-0.8,-2.1,-4.5,-3.2,-1.8,-0.6,0,0,-0.5,-1.2,-2.1,-1.4,-0.8,0,-0.3,-1.1,-3.2,-5.8,-8.9,-12.4,-15.8,-18.2,-19.9,-18.5,-16.2,-13.1,-10.4,-7.2,-4.8,-2.9,-1.5,-0.6,0,0,-0.4,-1.2,-2.8,-3.1,-2.4,-1.6,-0.8,0,-0.3,-1.1,-0.7,-0.2,0,0,-0.4,-0.9,-1.6,-2.4,-3.1,-3.8,-4.4,-5.1,-5.9,-6.6,-7.2,-6.8,-6.2,-5.4,-4.6,-3.8,-3.0,-2.3,-1.6,-1.0,-0.5,-0.2,0,0,-0.3,-0.5,-0.8,-0.4,-0.1,0]
const SP = [0,-0.8,-0.4,-1.1,-2.3,-1.5,-0.8,-0.2,0,0,-0.6,-1.8,-3.2,-2.1,-1.3,0,-0.5,-1.8,-5.4,-9.8,-14.2,-18.6,-21.3,-23.8,-25.1,-22.4,-18.8,-14.9,-11.2,-7.8,-5.2,-3.1,-1.8,-0.8,0,0,-0.6,-1.9,-4.2,-4.8,-3.6,-2.4,-1.2,0,-0.5,-1.8,-1.1,-0.3,0,0,-0.7,-1.6,-2.8,-4.2,-5.6,-6.8,-8.1,-9.4,-10.8,-12.1,-13.2,-12.4,-11.2,-9.6,-8.1,-6.8,-5.4,-4.1,-2.8,-1.7,-0.8,-0.3,0,0,-0.5,-0.9,-1.4,-0.7,-0.2,0]

const CROP: Record<DrawdownRange, number> = { Max: 80, '10Y': 80, '5Y': 60, '1Y': 12, YTD: 4, '6M': 6 }
const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']
const RANGES: DrawdownRange[] = ['Max', '10Y', '5Y', '1Y', 'YTD', '6M']

const scaleFor = (a: AssetBucket) => a === 'systematic' ? 0.7 : a === 'options' ? 1.4 : a === 'other' ? 0.3 : 1

const WORST: WorstDrawdown[] = [
  { depthPct: -7.92, start: 'Nov 2023', end: 'Jan 2025', months: 14 },
  { depthPct: -6.68, start: 'Oct 2025', end: 'Dec 2025', months: 2 },
  { depthPct: -3.92, start: 'Jan 2021', end: 'Mar 2021', months: 2 },
  { depthPct: -0.65, start: 'Mar 2026', end: 'Apr 2026', months: 1 },
]

export const drawdowns = new Hono<{ Bindings: Env }>()

drawdowns.get('/', c => {
  const asset = (c.req.query('asset') ?? 'all') as AssetBucket
  const range = (c.req.query('range') ?? '10Y') as DrawdownRange
  if (!ASSETS.includes(asset) || !RANGES.includes(range)) return c.json({ error: 'invalid query' }, 400)
  const crop = CROP[range]
  const s = scaleFor(asset)
  const payload: DrawdownsResponse = {
    asset, range,
    portfolioSeries: PORT.slice(-crop).map(v => +(v * s).toFixed(2)),
    sp500Series: SP.slice(-crop),
    worst: WORST.map(w => ({ ...w, depthPct: +(w.depthPct * s).toFixed(2) })),
  }
  return c.json(payload)
})
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add infra/cloudflare/worker/src/routes/drawdowns.ts infra/cloudflare/worker/test/drawdowns.test.ts && git commit -m "feat(worker): /drawdowns endpoint"`

### Task 3.4: Add `routes/monthly-returns.ts`

**Files:** Create route + test.

- [ ] **Step 1: Test** — similar pattern: assert years map non-empty, each year has 12 entries, totals map present.

```ts
// test/monthly-returns.test.ts
import { describe, it, expect } from 'vitest'
import { monthlyReturns } from '../src/routes/monthly-returns'
describe('monthly-returns', () => {
  it('returns full matrix with 12 entries per year', async () => {
    const res = await monthlyReturns.request('/?asset=all')
    const body = await res.json() as any
    expect(Object.keys(body.years).length).toBeGreaterThan(0)
    for (const year of Object.keys(body.years)) {
      expect(body.years[year].length).toBe(12)
    }
    expect(typeof body.totals[Object.keys(body.years)[0]]).toBe('number')
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```ts
// routes/monthly-returns.ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { AssetBucket, MonthlyReturnsResponse } from '../types/api'

const MATRIX: Record<string, (number | null)[]> = {
  '2026': [0.77, 0.53, -0.65, 14.30, null, null, null, null, null, null, null, null],
  '2025': [3.15, 1.57, 2.49, 1.54, 4.98, 0.78, 2.45, 2.09, 0.76, -6.68, 6.12, 2.07],
  '2024': [0.12, 0.76, 0.30, 0.25, 2.08, 0.87, 0.05, 0.19, 2.56, -0.55, 0.37, 0.34],
  '2023': [1.68, 2.65, 0.44, 0.55, 0.56, 0.79, 1.02, 0.68, 0.37, 0.49, -5.55, -2.51],
  '2022': [2.22, 0.60, 0.62, 0.94, 1.05, 2.43, 1.86, 1.70, 1.53, 0.30, 1.00, 1.23],
  '2021': [-3.92, 1.05, 4.32, 7.16, 0.46, 0.10, 3.49, 3.94, 0.40, 0.97, 0.63, 1.38],
  '2020': [null, null, -8.57, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00],
}
const TOTALS: Record<string, number> = { '2026': 15.04, '2025': 22.88, '2024': 7.55, '2023': 0.92, '2022': 16.60, '2021': 21.42, '2020': -8.57 }

const scaleFor = (a: AssetBucket) => a === 'systematic' ? 0.6 : a === 'options' ? 1.5 : a === 'other' ? 0.25 : 1

const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']

export const monthlyReturns = new Hono<{ Bindings: Env }>()

monthlyReturns.get('/', c => {
  const asset = (c.req.query('asset') ?? 'all') as AssetBucket
  if (!ASSETS.includes(asset)) return c.json({ error: 'invalid asset' }, 400)
  const s = scaleFor(asset)
  const years: MonthlyReturnsResponse['years'] = {}
  const totals: MonthlyReturnsResponse['totals'] = {}
  for (const [year, arr] of Object.entries(MATRIX)) {
    years[year] = arr.map(v => v === null ? null : +(v * s).toFixed(2))
    totals[year] = +(TOTALS[year] * s).toFixed(2)
  }
  return c.json({ asset, years, totals })
})
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add infra/cloudflare/worker/src/routes/monthly-returns.ts infra/cloudflare/worker/test/monthly-returns.test.ts && git commit -m "feat(worker): /monthly-returns endpoint"`

### Task 3.5: Add `routes/risk.ts`, `routes/system-metrics.ts`, `routes/breakdown.ts`, `routes/activity.ts`, `routes/campaigns-summary.ts`

**Files:** Create 5 route files + 5 test files.

- [ ] **Step 1: Tests (one per route, each ~20 lines)** — each test hits the route and asserts top-level shape exactly as types define.

- [ ] **Step 2: Run all — FAIL**

- [ ] **Step 3: Implement** (each route file ~30-50 lines, deterministic mock returning the shape). Example for `risk.ts`:

```ts
// routes/risk.ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { RiskMetrics } from '../types/api'
export const risk = new Hono<{ Bindings: Env }>()
risk.get('/metrics', c => {
  const payload: RiskMetrics = { vix: 15.84, vix1d: 11.20, delta: 42.3, theta: -58.20, vega: 124.5, ivRankSpy: 0.34, buyingPower: 48200, marginUsedPct: 61 }
  return c.json(payload)
})
```

```ts
// routes/system-metrics.ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { SystemMetricsSample } from '../types/api'
export const systemMetrics = new Hono<{ Bindings: Env }>()
systemMetrics.get('/metrics', c => {
  const payload: SystemMetricsSample = {
    cpu: [22,28,24,30,34,38,35,40,44,48,42,38,36,34,32,30,34,38,36,34],
    ram: [54,55,56,57,58,58,59,59,60,61,60,59,58,58,57,57,58,58,58,58],
    network: [12,18,22,30,45,58,65,72,78,82,76,68,60,54,48,42,48,54,62,71],
    diskUsedPct: 79, diskFreeGb: 42, diskTotalGb: 200,
    asOf: new Date().toISOString(),
  }
  return c.json(payload)
})
```

```ts
// routes/breakdown.ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { PositionsBreakdownResponse } from '../types/api'
export const breakdown = new Hono<{ Bindings: Env }>()
breakdown.get('/', c => {
  const payload: PositionsBreakdownResponse = {
    byStrategy: [
      { label: 'Iron Condor', value: 18400, color: '#2f81f7' },
      { label: 'Put Spread', value: 12200, color: '#a371f7' },
      { label: 'Call Spread', value: 6800, color: '#3fb950' },
      { label: 'Long Call', value: 3400, color: '#d29922' },
      { label: 'Short Strangle', value: 2100, color: '#f85149' },
    ],
    byAsset: [
      { label: 'Options', value: 28900, color: '#a371f7' },
      { label: 'Systematic', value: 10200, color: '#2f81f7' },
      { label: 'Other', value: 3800, color: '#3fb950' },
    ],
  }
  return c.json(payload)
})
```

```ts
// routes/activity.ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { ActivityResponse, ActivityEvent } from '../types/api'
const EVENTS: ActivityEvent[] = [
  { id: 'a1', icon: 'check-circle-2', tone: 'green', title: 'Order filled', subtitle: 'SPY 450C × 3 @ $12.40', timestamp: new Date(Date.now() - 2*60e3).toISOString() },
  { id: 'a2', icon: 'alert-triangle', tone: 'yellow', title: 'Delta breach warning', subtitle: 'SPY Iron Condor · short call 0.37', timestamp: new Date(Date.now() - 6*60e3).toISOString() },
  { id: 'a3', icon: 'play', tone: 'blue', title: 'Campaign resumed', subtitle: 'IWM Volatility Harvest', timestamp: new Date(Date.now() - 24*60e3).toISOString() },
  { id: 'a4', icon: 'x-circle', tone: 'red', title: 'Order rejected', subtitle: 'QQQ 395C — insufficient margin', timestamp: new Date(Date.now() - 38*60e3).toISOString() },
  { id: 'a5', icon: 'repeat', tone: 'purple', title: 'Position rolled', subtitle: 'SPY 450P/445P → next week', timestamp: new Date(Date.now() - 72*60e3).toISOString() },
  { id: 'a6', icon: 'trending-up', tone: 'green', title: 'Take-profit hit', subtitle: 'IWM 195C closed @ +$94.50', timestamp: new Date(Date.now() - 124*60e3).toISOString() },
  { id: 'a7', icon: 'refresh-cw', tone: 'blue', title: 'IBKR reconnected', subtitle: 'after 4.2s drop', timestamp: new Date(Date.now() - 201*60e3).toISOString() },
  { id: 'a8', icon: 'file-text', tone: 'muted', title: 'Daily report exported', subtitle: 'pnl_2026-04-19.csv', timestamp: new Date(Date.now() - 24*60*60e3).toISOString() },
]
export const activity = new Hono<{ Bindings: Env }>()
activity.get('/recent', c => {
  const limit = Number(c.req.query('limit') ?? 8)
  const payload: ActivityResponse = { events: EVENTS.slice(0, Math.max(1, Math.min(limit, 50))) }
  return c.json(payload)
})
```

```ts
// routes/campaigns-summary.ts
import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { CampaignsSummary } from '../types/api'
export const campaignsSummary = new Hono<{ Bindings: Env }>()
campaignsSummary.get('/summary', c => {
  const payload: CampaignsSummary = { active: 2, paused: 1, draft: 1, detail: '1 paused · 1 draft' }
  return c.json(payload)
})
```

- [ ] **Step 4: Run all — PASS**: `cd infra/cloudflare/worker && bun test`
- [ ] **Step 5: Commit**: `git add infra/cloudflare/worker/src/routes infra/cloudflare/worker/test && git commit -m "feat(worker): risk, system-metrics, breakdown, activity, campaigns-summary endpoints"`

### Task 3.6: Extend `alerts.ts` with `/summary-24h`

**Files:** Modify `infra/cloudflare/worker/src/routes/alerts.ts`; add test.

- [ ] **Step 1: Test**

```ts
// test/alerts-summary.test.ts
import { describe, it, expect } from 'vitest'
import { alerts } from '../src/routes/alerts'
describe('alerts summary-24h', () => {
  it('returns counts by severity', async () => {
    const res = await alerts.request('/summary-24h')
    expect(res.status).toBe(200)
    const body = await res.json() as any
    expect(typeof body.total).toBe('number')
    expect(typeof body.critical).toBe('number')
    expect(body.total).toBeGreaterThanOrEqual(body.critical)
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Add route handler** (append to existing `alerts.ts`):

```ts
// infra/cloudflare/worker/src/routes/alerts.ts — ADD this route definition
import type { AlertsSummary } from '../types/api'

alerts.get('/summary-24h', async (c) => {
  // TODO in Phase 6: replace with real D1 query grouped by severity and timestamp > now - 24h
  const payload: AlertsSummary = { total: 14, critical: 2, warning: 5, info: 7 }
  return c.json(payload)
})
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add infra/cloudflare/worker/src/routes/alerts.ts infra/cloudflare/worker/test/alerts-summary.test.ts && git commit -m "feat(worker): alerts/summary-24h endpoint"`

### Task 3.7: Extend `positions.ts` with `campaign` join

**Files:** Modify `infra/cloudflare/worker/src/routes/positions.ts`; update test.

- [ ] **Step 1: Update existing positions test** to assert `campaign` field is `string | null`.
- [ ] **Step 2: Modify SQL in `routes/positions.ts`**: LEFT JOIN `campaigns` on `positions.campaign_id = campaigns.id` and include `campaigns.name AS campaign`.

```sql
SELECT p.*, c.name AS campaign, c.id AS campaignId
FROM positions p
LEFT JOIN campaigns c ON c.id = p.campaign_id
WHERE p.status = 'OPEN'
ORDER BY p.opened_at DESC
```

- [ ] **Step 3: Run tests — PASS**
- [ ] **Step 4: Commit**: `git add infra/cloudflare/worker/src/routes/positions.ts infra/cloudflare/worker/test && git commit -m "feat(worker): add campaign to positions/active"`

### Task 3.8: Mount all new routes in `index.ts`

**Files:** Modify `infra/cloudflare/worker/src/index.ts`.

- [ ] **Step 1: Add imports and routes after the existing `app.route` calls**

```ts
import { performance } from './routes/performance'
import { drawdowns } from './routes/drawdowns'
import { monthlyReturns } from './routes/monthly-returns'
import { risk } from './routes/risk'
import { systemMetrics } from './routes/system-metrics'
import { breakdown } from './routes/breakdown'
import { activity } from './routes/activity'
import { campaignsSummary } from './routes/campaigns-summary'

app.route('/api/performance', performance)
app.route('/api/drawdowns', drawdowns)
app.route('/api/monthly-returns', monthlyReturns)
app.route('/api/risk', risk)
app.route('/api/system', systemMetrics)
app.route('/api/positions/breakdown', breakdown)
app.route('/api/activity', activity)
app.route('/api/campaigns', campaignsSummary)
```

- [ ] **Step 2: Extend the root `/` `endpoints` array with the new paths**
- [ ] **Step 3: Full worker test**: `cd infra/cloudflare/worker && bun test`
- [ ] **Step 4: Local smoke**: `bun run dev` and `curl http://localhost:8787/api/performance/summary?asset=all`; `Ctrl+C`.
- [ ] **Step 5: Commit**: `git add infra/cloudflare/worker/src/index.ts && git commit -m "feat(worker): mount new dashboard API routes"`

---

(continues in `2026-04-20-dashboard-redesign-part3.md` for Phase 4 & 5.)
