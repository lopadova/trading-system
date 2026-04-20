# Dashboard Redesign — Implementation Plan (Part 3: Phase 4)

> Continuation of `2026-04-20-dashboard-redesign-part2.md`. Same execution rules.

---

## Phase 4 — Dashboard widgets + hooks

**Checkpoint:** Overview page matches kit layout pixel-close and fetches live data from the Worker.

### Task 4.1: Create shared dashboard types

**Files:**
- Create: `dashboard/src/types/performance.ts`, `drawdown.ts`, `risk.ts`, `system.ts`, `activity.ts`, `breakdown.ts`
- Modify: `dashboard/src/types/position.ts`

- [ ] **Step 1: Write all type files** (mirror worker `src/types/api.ts` from Task 3.1).

```ts
// dashboard/src/types/performance.ts
export type AssetBucket = 'all' | 'systematic' | 'options' | 'other'
export type PerfRange = '1W' | '1M' | '3M' | 'YTD' | '1Y' | 'ALL'

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

// dashboard/src/types/drawdown.ts
import type { AssetBucket } from './performance'
export type DrawdownRange = 'Max' | '10Y' | '5Y' | '1Y' | 'YTD' | '6M'
export interface WorstDrawdown { depthPct: number; start: string; end: string; months: number }
export interface DrawdownsData {
  asset: AssetBucket; range: DrawdownRange
  portfolioSeries: number[]; sp500Series: number[]
  worst: WorstDrawdown[]
}

// dashboard/src/types/risk.ts
export interface RiskMetrics {
  vix: number | null; vix1d: number | null
  delta: number; theta: number; vega: number
  ivRankSpy: number | null; buyingPower: number; marginUsedPct: number
}

// dashboard/src/types/system.ts
export interface SystemMetricsSample {
  cpu: number[]; ram: number[]; network: number[]
  diskUsedPct: number; diskFreeGb: number; diskTotalGb: number
  asOf: string
}

// dashboard/src/types/activity.ts
export type ActivityIcon = 'check-circle-2' | 'alert-triangle' | 'play' | 'x-circle' | 'repeat' | 'trending-up' | 'refresh-cw' | 'file-text'
export type ActivityTone = 'green' | 'red' | 'yellow' | 'blue' | 'purple' | 'muted'
export interface ActivityEvent {
  id: string; icon: ActivityIcon; tone: ActivityTone
  title: string; subtitle: string; timestamp: string
}
export interface ActivityResponse { events: ActivityEvent[] }

// dashboard/src/types/breakdown.ts
export interface ExposureSegment { label: string; value: number; color: string }
export interface PositionsBreakdownData {
  byStrategy: ExposureSegment[]; byAsset: ExposureSegment[]
}

// dashboard/src/types/alert.ts — add:
export interface AlertsSummary { total: number; critical: number; warning: number; info: number }

// dashboard/src/types/campaign.ts — add:
export interface CampaignsSummary { active: number; paused: number; draft: number; detail: string }

// dashboard/src/types/position.ts — EXTEND existing Position interface:
// Add: campaign: string | null; campaignId: string | null
```

- [ ] **Step 2: Typecheck**: `cd dashboard && npm run typecheck`
- [ ] **Step 3: Commit**: `git add dashboard/src/types && git commit -m "feat(dashboard): shared API types for new widgets"`

### Task 4.2: Create `chart-utils.ts` helpers (TDD)

**Files:**
- Create: `dashboard/src/lib/chart-utils.test.ts`
- Create: `dashboard/src/lib/chart-utils.ts`

- [ ] **Step 1: Test**

```ts
// chart-utils.test.ts
import { describe, it, expect } from 'vitest'
import { normalizeYAxis, generateDateLabels } from './chart-utils'

describe('normalizeYAxis', () => {
  it('returns min/max with padding', () => {
    const r = normalizeYAxis([100, 110, 120])
    expect(r.min).toBeLessThan(100)
    expect(r.max).toBeGreaterThan(120)
  })
  it('handles empty arrays', () => {
    const r = normalizeYAxis([])
    expect(r.min).toBeLessThan(r.max)
  })
})
describe('generateDateLabels', () => {
  it('returns the requested number of labels', () => {
    const labels = generateDateLabels(new Date('2026-04-20'), 30, 5)
    expect(labels.length).toBe(5)
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```ts
// chart-utils.ts
export interface YAxisRange { min: number; max: number }

export function normalizeYAxis(values: number[], padRatio = 0.08): YAxisRange {
  if (values.length === 0) return { min: 0, max: 100 }
  const max = Math.max(...values)
  const min = Math.min(...values)
  const pad = (max - min) * padRatio || 2
  return { min: min - pad, max: max + pad }
}

export interface DateLabel { idx: number; label: string }

export function generateDateLabels(endDate: Date, span: number, steps: number): DateLabel[] {
  const labels: DateLabel[] = []
  for (let i = 0; i < steps; i++) {
    const idx = Math.round((i / (steps - 1)) * (span - 1))
    const daysAgo = span - 1 - idx
    const d = new Date(endDate); d.setDate(d.getDate() - daysAgo)
    labels.push({ idx, label: d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) })
  }
  return labels
}

export function generateMonthLabels(endDate: Date, span: number, steps: number): DateLabel[] {
  const labels: DateLabel[] = []
  for (let i = 0; i < steps; i++) {
    const idx = Math.round((i / (steps - 1)) * (span - 1))
    const monthsAgo = span - 1 - idx
    const d = new Date(endDate); d.setMonth(d.getMonth() - monthsAgo)
    labels.push({ idx, label: d.toLocaleDateString('en-US', { month: 'short', year: '2-digit' }) })
  }
  return labels
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/lib/chart-utils* && git commit -m "feat(dashboard): chart-utils helpers"`

### Task 4.3: Create `utils/format.ts` (culture-invariant currency/percent)

**Files:**
- Create: `dashboard/src/utils/format.test.ts`
- Create: `dashboard/src/utils/format.ts`

- [ ] **Step 1: Test**

```ts
// format.test.ts
import { describe, it, expect } from 'vitest'
import { formatCurrency, formatPercent, formatDelta } from './format'

describe('formatCurrency', () => {
  it('formats USD with 2 decimals', () => {
    expect(formatCurrency(125430.5, 'USD')).toBe('$125,430.50')
  })
  it('formats EUR with integer magnitude', () => {
    expect(formatCurrency(38900, 'EUR', 0)).toBe('€38,900')
  })
  it('always uses US-style thousands separator', () => {
    expect(formatCurrency(1234567.89, 'USD')).toBe('$1,234,567.89')
  })
})
describe('formatPercent', () => {
  it('signs positive values', () => {
    expect(formatPercent(14.3)).toBe('+14.30%')
  })
  it('signs negative values', () => {
    expect(formatPercent(-7.92)).toBe('-7.92%')
  })
})
describe('formatDelta', () => {
  it('renders arrow + amount + percent for positive', () => {
    expect(formatDelta(2340.8, 1.9, 'USD')).toBe('↑ +$2,340.80 (+1.90%)')
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```ts
// format.ts
const LOCALE = 'en-US'

export function formatCurrency(amount: number, currency: 'USD' | 'EUR', decimals = 2): string {
  const symbol = currency === 'USD' ? '$' : '€'
  const sign = amount < 0 ? '-' : ''
  const abs = Math.abs(amount).toLocaleString(LOCALE, { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
  return `${sign}${symbol}${abs}`
}

export function formatPercent(value: number, decimals = 2): string {
  const sign = value >= 0 ? '+' : '-'
  return `${sign}${Math.abs(value).toFixed(decimals)}%`
}

export function formatDelta(amount: number, pct: number, currency: 'USD' | 'EUR'): string {
  const up = amount >= 0
  const arrow = up ? '↑' : '↓'
  const money = `${up ? '+' : '-'}${formatCurrency(Math.abs(amount), currency).replace(/^-/, '')}`
  return `${arrow} ${money} (${formatPercent(pct)})`
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/utils/format* && git commit -m "feat(dashboard): format helpers"`

### Task 4.4: Create the 10 React Query hooks

**Files:** Create 10 hook files under `dashboard/src/hooks/` + a shared test demonstrating hook shape.

- [ ] **Step 1: Shared test file**

```ts
// dashboard/src/hooks/dashboard-hooks.test.tsx
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { usePerformanceSummary } from './usePerformanceSummary'

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn(async () => new Response(JSON.stringify({
    asset: 'all', m: 14.3, ytd: 15.04, y2: 49.88, y5: 100.13, y10: 98.59, ann: 13.07, base: 125430
  }), { headers: { 'content-type': 'application/json' } })) as any
})
afterEach(() => { vi.restoreAllMocks() })

describe('usePerformanceSummary', () => {
  it('fetches and returns summary data', async () => {
    const { result } = renderHook(() => usePerformanceSummary('all'), { wrapper })
    await waitFor(() => expect(result.current.data).toBeDefined())
    expect(result.current.data?.m).toBe(14.3)
  })
})
```

- [ ] **Step 2: Implement hooks**

```ts
// hooks/usePerformanceSummary.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { SummaryData, AssetBucket } from '../types/performance'

export function usePerformanceSummary(asset: AssetBucket) {
  return useQuery<SummaryData>({
    queryKey: ['performance', 'summary', asset],
    queryFn: () => api.get(`performance/summary?asset=${asset}`).json<SummaryData>(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}

// hooks/usePerformanceSeries.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { PerfSeries, AssetBucket, PerfRange } from '../types/performance'
export function usePerformanceSeries(asset: AssetBucket, range: PerfRange) {
  return useQuery<PerfSeries>({
    queryKey: ['performance', 'series', asset, range],
    queryFn: () => api.get(`performance/series?asset=${asset}&range=${range}`).json<PerfSeries>(),
    staleTime: 30_000,
  })
}

// hooks/useDrawdowns.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { DrawdownsData, DrawdownRange } from '../types/drawdown'
import type { AssetBucket } from '../types/performance'
export function useDrawdowns(asset: AssetBucket, range: DrawdownRange) {
  return useQuery<DrawdownsData>({
    queryKey: ['drawdowns', asset, range],
    queryFn: () => api.get(`drawdowns?asset=${asset}&range=${range}`).json<DrawdownsData>(),
    staleTime: 60_000,
  })
}

// hooks/useMonthlyReturns.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { AssetBucket } from '../types/performance'
interface MonthlyReturnsData { asset: AssetBucket; years: Record<string, (number | null)[]>; totals: Record<string, number> }
export function useMonthlyReturns(asset: AssetBucket) {
  return useQuery<MonthlyReturnsData>({
    queryKey: ['monthly-returns', asset],
    queryFn: () => api.get(`monthly-returns?asset=${asset}`).json<MonthlyReturnsData>(),
    staleTime: 120_000,
  })
}

// hooks/useRiskMetrics.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { RiskMetrics } from '../types/risk'
export function useRiskMetrics() {
  return useQuery<RiskMetrics>({
    queryKey: ['risk', 'metrics'],
    queryFn: () => api.get('risk/metrics').json<RiskMetrics>(),
    staleTime: 15_000,
    refetchInterval: 15_000,
  })
}

// hooks/useSystemMetrics.ts (REWRITE existing useSystemStatus, keeping filename for minimal diff:
// Add new hook to useSystemStatus.ts and re-export or replace consumers)
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { SystemMetricsSample } from '../types/system'
export function useSystemMetricsSample() {
  return useQuery<SystemMetricsSample>({
    queryKey: ['system', 'metrics', 'sample'],
    queryFn: () => api.get('system/metrics').json<SystemMetricsSample>(),
    staleTime: 15_000,
    refetchInterval: 15_000,
  })
}

// hooks/useRecentActivity.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { ActivityResponse } from '../types/activity'
export function useRecentActivity(limit = 8) {
  return useQuery<ActivityResponse>({
    queryKey: ['activity', 'recent', limit],
    queryFn: () => api.get(`activity/recent?limit=${limit}`).json<ActivityResponse>(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}

// hooks/useAlertsSummary.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { AlertsSummary } from '../types/alert'
export function useAlertsSummary() {
  return useQuery<AlertsSummary>({
    queryKey: ['alerts', 'summary-24h'],
    queryFn: () => api.get('alerts/summary-24h').json<AlertsSummary>(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}

// hooks/useCampaignsSummary.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { CampaignsSummary } from '../types/campaign'
export function useCampaignsSummary() {
  return useQuery<CampaignsSummary>({
    queryKey: ['campaigns', 'summary'],
    queryFn: () => api.get('campaigns/summary').json<CampaignsSummary>(),
    staleTime: 30_000,
  })
}

// hooks/usePositionsBreakdown.ts
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { PositionsBreakdownData } from '../types/breakdown'
export function usePositionsBreakdown() {
  return useQuery<PositionsBreakdownData>({
    queryKey: ['positions', 'breakdown'],
    queryFn: () => api.get('positions/breakdown').json<PositionsBreakdownData>(),
    staleTime: 60_000,
  })
}
```

- [ ] **Step 3: Run — PASS**: `cd dashboard && npm test -- dashboard-hooks`
- [ ] **Step 4: Commit**: `git add dashboard/src/hooks dashboard/src/types && git commit -m "feat(dashboard): 10 React Query hooks for new endpoints"`

### Task 4.5: Build `AssetFilter` widget

**Files:**
- Create: `dashboard/src/components/dashboard/AssetFilter.test.tsx`
- Create: `dashboard/src/components/dashboard/AssetFilter.tsx`

- [ ] **Step 1: Test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, beforeEach } from 'vitest'
import { AssetFilter } from './AssetFilter'
import { useAssetFilterStore } from '../../stores/assetFilterStore'
describe('AssetFilter', () => {
  beforeEach(() => useAssetFilterStore.setState({ asset: 'all' }))
  it('renders 4 chips', () => {
    render(<AssetFilter />)
    expect(screen.getByRole('button', { name: /all/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /systematic/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /options/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /other/i })).toBeInTheDocument()
  })
  it('sets asset on click', () => {
    render(<AssetFilter />)
    fireEvent.click(screen.getByRole('button', { name: /options/i }))
    expect(useAssetFilterStore.getState().asset).toBe('options')
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```tsx
// AssetFilter.tsx
import { cn } from '../../utils/cn'
import { useAssetFilterStore, type AssetBucket } from '../../stores/assetFilterStore'

const ASSETS: { id: AssetBucket; label: string; color: string }[] = [
  { id: 'all',        label: 'All assets',  color: '#e6edf3' },
  { id: 'systematic', label: 'Systematic',  color: '#2f81f7' },
  { id: 'options',    label: 'Options',     color: '#a371f7' },
  { id: 'other',      label: 'Other',       color: '#3fb950' },
]

export function AssetFilter() {
  const asset = useAssetFilterStore(s => s.asset)
  const setAsset = useAssetFilterStore(s => s.setAsset)
  return (
    <div className="inline-flex gap-1 p-1 bg-surface border border-border rounded-lg">
      {ASSETS.map(a => {
        const on = a.id === asset
        return (
          <button
            key={a.id}
            type="button"
            aria-pressed={on}
            onClick={() => setAsset(a.id)}
            className={cn(
              'px-3.5 py-1.5 rounded-md text-[12.5px] font-medium flex items-center gap-1.5 transition-colors',
              on ? 'bg-[var(--bg-3)] text-[var(--fg-1)]' : 'text-muted hover:text-[var(--fg-1)]'
            )}
          >
            <span className={cn('w-1.5 h-1.5 rounded-full', a.id === 'all' ? 'border border-muted' : '')} style={a.id !== 'all' ? { background: a.color } : undefined} />
            {a.label}
          </button>
        )
      })}
    </div>
  )
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/components/dashboard/AssetFilter* && git commit -m "feat(dashboard): AssetFilter widget"`

### Task 4.6: Build `SummaryCard` widget

**Files:** Create `dashboard/src/components/dashboard/SummaryCard.tsx` + test.

- [ ] **Step 1: Test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { SummaryCard } from './SummaryCard'

function Wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['performance', 'summary', 'all'], {
    asset: 'all', m: 14.3, ytd: 15.04, y2: 49.88, y5: 100.13, y10: 98.59, ann: 13.07, base: 125430
  })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

beforeEach(() => { globalThis.fetch = vi.fn() as any })

describe('SummaryCard', () => {
  it('renders 6 horizons as %', async () => {
    render(<Wrapper><SummaryCard asset="all" /></Wrapper>)
    expect(await screen.findByText('+14.30%')).toBeInTheDocument()
    expect(screen.getByText('+15.04%')).toBeInTheDocument()
    expect(screen.getByText('+49.88%')).toBeInTheDocument()
    expect(screen.getByText('+100.13%')).toBeInTheDocument()
    expect(screen.getByText('+98.59%')).toBeInTheDocument()
    expect(screen.getByText('+13.07%')).toBeInTheDocument()
  })
  it('toggles to € unit', async () => {
    render(<Wrapper><SummaryCard asset="all" /></Wrapper>)
    fireEvent.click(screen.getByRole('button', { name: '€' }))
    expect(await screen.findByText(/€17,937/)).toBeInTheDocument() // 14.3% of 125430
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```tsx
// SummaryCard.tsx
import { useState } from 'react'
import { Card } from '../ui/Card'
import { SegmentedControl } from '../ui/SegmentedControl'
import { Skeleton } from '../ui/Skeleton'
import { usePerformanceSummary } from '../../hooks/usePerformanceSummary'
import { formatPercent, formatCurrency } from '../../utils/format'
import type { AssetBucket } from '../../types/performance'

type Unit = 'pct' | 'eur'
interface Row { k: string; v: number }

export function SummaryCard({ asset }: { asset: AssetBucket }) {
  const [unit, setUnit] = useState<Unit>('pct')
  const { data, isLoading } = usePerformanceSummary(asset)
  if (isLoading || !data) return <Card><Skeleton h={180} /></Card>

  const rows: Row[] = [
    { k: 'This Month', v: data.m },
    { k: 'Year To Date', v: data.ytd },
    { k: '2 Years', v: data.y2 },
    { k: '5 Years', v: data.y5 },
    { k: '10 Years', v: data.y10 },
    { k: 'Annualized', v: data.ann },
  ]

  const render = (v: number) => unit === 'pct'
    ? formatPercent(v, 2)
    : `${v >= 0 ? '+' : '-'}${formatCurrency(Math.abs(data.base * v / 100), 'EUR', 0).replace(/^-/, '')}`

  return (
    <Card>
      <div className="flex items-start justify-between mb-4">
        <div>
          <h3 className="text-[15px] font-semibold m-0">Performance Summary</h3>
          <div className="text-[12px] text-muted mt-0.5">Returns across time horizons</div>
        </div>
        <SegmentedControl<Unit>
          value={unit}
          onChange={setUnit}
          options={[{ value: 'pct', label: '%' }, { value: 'eur', label: '€' }]}
          size="sm"
        />
      </div>
      <div className="grid grid-cols-3 gap-0.5">
        {rows.map((r, i) => {
          const up = r.v >= 0
          const rightBorder = (i % 3 !== 2) ? 'border-r' : ''
          const bottomBorder = (i < 3) ? 'border-b' : ''
          return (
            <div key={r.k} className={`py-3.5 px-2 text-center ${rightBorder} ${bottomBorder} border-border`}>
              <div
                className={`font-mono text-[22px] font-semibold tabular-nums tracking-tight ${up ? 'text-up' : 'text-down'}`}
              >
                {render(r.v)}
              </div>
              <div className="overline mt-1">{r.k}</div>
            </div>
          )
        })}
      </div>
    </Card>
  )
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/components/dashboard/SummaryCard* && git commit -m "feat(dashboard): SummaryCard with %/€ toggle"`

### Task 4.7: Build `MultiSeriesChart` with Recharts

**Files:** Create `dashboard/src/components/dashboard/MultiSeriesChart.tsx` + test.

- [ ] **Step 1: Test** — uses Recharts mock via happy-dom; asserts series toggle buttons and range control call setters.

```tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MultiSeriesChart } from './MultiSeriesChart'

function wrap(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['performance', 'series', 'all', '1M'], {
    asset: 'all', range: '1M',
    portfolio: Array.from({ length: 20 }, (_, i) => 100 + i),
    sp500: Array.from({ length: 20 }, (_, i) => 100 + i * 0.8),
    swda: Array.from({ length: 20 }, (_, i) => 100 + i * 0.7),
    startDate: '2026-03-31T00:00:00.000Z', endDate: '2026-04-20T00:00:00.000Z',
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => { globalThis.fetch = vi.fn() as any })

describe('MultiSeriesChart', () => {
  it('renders 3 toggle chips and all range buttons', () => {
    render(wrap(<MultiSeriesChart asset="all" />))
    expect(screen.getByRole('button', { name: /Portfolio/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /S&P 500/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /SWDA/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'ALL' })).toBeInTheDocument()
  })
  it('toggles a series off', () => {
    render(wrap(<MultiSeriesChart asset="all" />))
    const btn = screen.getByRole('button', { name: /SWDA/ })
    fireEvent.click(btn)
    expect(btn.getAttribute('aria-pressed')).toBe('false')
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```tsx
// MultiSeriesChart.tsx
import { useState } from 'react'
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, Area, CartesianGrid } from 'recharts'
import { SegmentedControl } from '../ui/SegmentedControl'
import { FilterOverlay } from '../ui/FilterOverlay'
import { usePerformanceSeries } from '../../hooks/usePerformanceSeries'
import type { AssetBucket, PerfRange } from '../../types/performance'
import { cn } from '../../utils/cn'

const RANGES: PerfRange[] = ['1W', '1M', '3M', 'YTD', '1Y', 'ALL']

const SERIES = [
  { key: 'portfolio' as const, label: 'Portfolio', color: '#2f81f7', strokeWidth: 2, dash: undefined },
  { key: 'sp500'     as const, label: 'S&P 500',   color: '#a371f7', strokeWidth: 1.5, dash: '4 3' },
  { key: 'swda'      as const, label: 'SWDA',      color: '#3fb950', strokeWidth: 1.5, dash: '1 3' },
]

type SeriesKey = typeof SERIES[number]['key']

export function MultiSeriesChart({ asset }: { asset: AssetBucket }) {
  const [range, setRange] = useState<PerfRange>('1M')
  const [shown, setShown] = useState<Record<SeriesKey, boolean>>({ portfolio: true, sp500: true, swda: true })
  const { data, isFetching } = usePerformanceSeries(asset, range)

  const chartData = data
    ? data.portfolio.map((v, i) => ({ i, portfolio: v, sp500: data.sp500[i], swda: data.swda[i] }))
    : []

  return (
    <div className="relative">
      <div className="flex justify-between items-start mb-3">
        <div>
          <h3 className="text-[15px] font-semibold m-0">Account Performance</h3>
          <div className="text-[12px] text-muted mt-0.5">Normalized to 100 · vs S&amp;P 500 and SWDA</div>
        </div>
        <SegmentedControl<PerfRange>
          value={range}
          onChange={setRange}
          options={RANGES.map(r => ({ value: r, label: r }))}
          size="sm"
        />
      </div>
      <div className="flex gap-2.5 mb-2.5 flex-wrap">
        {SERIES.map(s => {
          const on = shown[s.key]
          return (
            <button
              key={s.key}
              type="button"
              aria-pressed={on}
              onClick={() => setShown(p => ({ ...p, [s.key]: !p[s.key] }))}
              className={cn(
                'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-pill border border-border text-[11.5px]',
                on ? 'bg-[var(--bg-3)] text-[var(--fg-1)]' : 'bg-transparent text-muted opacity-60'
              )}
            >
              <span className="w-2.5 h-0.5 rounded" style={{ background: s.color }} />
              {s.label}
            </button>
          )
        })}
      </div>
      <div className="h-[260px] relative">
        <FilterOverlay visible={isFetching && chartData.length === 0} label="Loading chart…" />
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={chartData} margin={{ top: 10, right: 12, bottom: 10, left: 0 }}>
            <defs>
              <linearGradient id="msPerfGrad" x1="0" x2="0" y1="0" y2="1">
                <stop offset="0%" stopColor="#2f81f7" stopOpacity={0.3} />
                <stop offset="100%" stopColor="#2f81f7" stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid stroke="var(--border-2)" strokeDasharray="0" vertical={false} />
            <XAxis dataKey="i" stroke="var(--fg-2)" fontSize={10} tickLine={false} axisLine={{ stroke: 'var(--border-1)' }} />
            <YAxis stroke="var(--fg-2)" fontSize={10} tickLine={false} axisLine={{ stroke: 'var(--border-1)' }} domain={['auto', 'auto']} />
            <Tooltip contentStyle={{ background: 'var(--bg-2)', border: '1px solid var(--border-1)', borderRadius: 8, fontSize: 12 }} labelStyle={{ color: 'var(--fg-2)' }} />
            {shown.portfolio && (
              <Area type="monotone" dataKey="portfolio" stroke="none" fill="url(#msPerfGrad)" isAnimationActive={false} />
            )}
            {SERIES.map(s => shown[s.key] && (
              <Line
                key={s.key}
                type="monotone"
                dataKey={s.key}
                stroke={s.color}
                strokeWidth={s.strokeWidth}
                strokeDasharray={s.dash}
                dot={false}
                isAnimationActive={false}
              />
            ))}
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/components/dashboard/MultiSeriesChart* && git commit -m "feat(dashboard): MultiSeriesChart (Portfolio + S&P + SWDA)"`

### Task 4.8: Build `DrawdownsSection` widget

**Files:** Create `dashboard/src/components/dashboard/DrawdownsSection.tsx` + test.

- [ ] **Step 1: Test** — render with QueryClient seeded data; assert chart container renders + Worst Drawdowns table renders 4 rows.

- [ ] **Step 2: Implement**

```tsx
// DrawdownsSection.tsx
import { useState } from 'react'
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts'
import { Card } from '../ui/Card'
import { Skeleton } from '../ui/Skeleton'
import { SegmentedControl } from '../ui/SegmentedControl'
import { useDrawdowns } from '../../hooks/useDrawdowns'
import type { AssetBucket } from '../../types/performance'
import type { DrawdownRange } from '../../types/drawdown'
import { formatPercent } from '../../utils/format'

const RANGES: DrawdownRange[] = ['Max', '10Y', '5Y', '1Y', 'YTD', '6M']

export function DrawdownsSection({ asset }: { asset: AssetBucket }) {
  const [range, setRange] = useState<DrawdownRange>('10Y')
  const { data, isLoading } = useDrawdowns(asset, range)

  return (
    <div className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-4">
      <Card>
        <div className="flex justify-between items-center mb-2.5">
          <h3 className="text-[15px] font-semibold m-0">Drawdowns</h3>
          <SegmentedControl<DrawdownRange>
            value={range}
            onChange={setRange}
            options={RANGES.map(r => ({ value: r, label: r }))}
            size="sm"
          />
        </div>
        <div className="flex gap-4 mb-2 text-[11.5px] text-muted">
          <span className="inline-flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-[var(--green)]" />Portfolio</span>
          <span className="inline-flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-[var(--blue)]" />S&amp;P 500</span>
        </div>
        <div className="h-[220px]">
          {isLoading || !data ? <Skeleton h={220} /> : (
            <ResponsiveContainer width="100%" height="100%">
              <LineChart
                data={data.portfolioSeries.map((v, i) => ({ i, portfolio: v, sp500: data.sp500Series[i] }))}
                margin={{ top: 14, right: 12, bottom: 10, left: 0 }}
              >
                <CartesianGrid stroke="var(--border-2)" vertical={false} />
                <XAxis dataKey="i" stroke="var(--fg-2)" fontSize={10} tickLine={false} axisLine={{ stroke: 'var(--border-1)' }} />
                <YAxis stroke="var(--fg-2)" fontSize={10} tickLine={false} axisLine={{ stroke: 'var(--border-1)' }} tickFormatter={(v: number) => `${v.toFixed(0)}%`} />
                <Tooltip contentStyle={{ background: 'var(--bg-2)', border: '1px solid var(--border-1)', borderRadius: 8 }} />
                <Line type="monotone" dataKey="sp500" stroke="#2f81f7" strokeWidth={1.5} dot={false} isAnimationActive={false} />
                <Line type="monotone" dataKey="portfolio" stroke="#3fb950" strokeWidth={1.8} dot={false} isAnimationActive={false} />
              </LineChart>
            </ResponsiveContainer>
          )}
        </div>
      </Card>
      <Card padding={0}>
        <div className="px-4 py-3.5 border-b border-border">
          <h3 className="text-[14px] font-semibold m-0">Worst Drawdowns</h3>
        </div>
        <table className="w-full border-collapse">
          <thead>
            <tr className="bg-[var(--bg-1)]">
              {['Depth','Start','End','Months'].map(h => (
                <th key={h} className="px-3 py-2 text-left text-[10.5px] font-medium overline border-b border-border">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data?.worst.map((d, i) => (
              <tr key={i} className="border-b border-border last:border-0">
                <td className="px-3 py-2.5 font-mono text-down font-medium text-[12.5px]">{formatPercent(d.depthPct)}</td>
                <td className="px-3 py-2.5 font-mono text-[12.5px]">{d.start}</td>
                <td className="px-3 py-2.5 font-mono text-[12.5px]">{d.end}</td>
                <td className="px-3 py-2.5 font-mono text-muted text-[12.5px]">{d.months}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  )
}
```

- [ ] **Step 3: Run — PASS**
- [ ] **Step 4: Commit**: `git add dashboard/src/components/dashboard/DrawdownsSection* && git commit -m "feat(dashboard): DrawdownsSection (chart + worst list)"`

### Task 4.9: Build `MonthlyPerfSection` widget

**Files:** Create `dashboard/src/components/dashboard/MonthlyPerfSection.tsx` + test.

- [ ] **Step 1: Test** — seed data; assert 12 month header cells, year rows render, locked tabs disabled.

- [ ] **Step 2: Implement**

```tsx
// MonthlyPerfSection.tsx
import { useState } from 'react'
import { Card } from '../ui/Card'
import { Skeleton } from '../ui/Skeleton'
import { SegmentedControl } from '../ui/SegmentedControl'
import { useMonthlyReturns } from '../../hooks/useMonthlyReturns'
import type { AssetBucket } from '../../types/performance'
import { Lock } from 'lucide-react'

const MONTHS = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec']

type Tab = 'monthly' | 'compounded' | 'cumulative' | 'drawdowns' | 'beta'

function cellBg(v: number | null | undefined): string {
  if (v === null || v === undefined) return 'rgba(125,133,144,0.22)'
  if (v === 0) return 'rgba(125,133,144,0.14)'
  const abs = Math.min(Math.abs(v) / 8, 1)
  return v > 0 ? `rgba(63,185,80,${0.18 + abs * 0.55})` : `rgba(248,81,73,${0.18 + abs * 0.55})`
}

function cellFg(v: number | null | undefined): string {
  if (v === null || v === undefined) return 'var(--fg-3)'
  if (v === 0) return 'var(--fg-2)'
  return '#ffffff'
}

export function MonthlyPerfSection({ asset }: { asset: AssetBucket }) {
  const [tab, setTab] = useState<Tab>('monthly')
  const { data, isLoading } = useMonthlyReturns(asset)

  const years = data ? Object.keys(data.years).map(Number).sort((a, b) => b - a) : []

  return (
    <Card padding={0}>
      <div className="px-5 pt-4 pb-3.5 text-center border-b border-border">
        <h3 className="font-display text-[20px] font-bold m-0 mb-3.5">Monthly Performance</h3>
        <SegmentedControl<Tab>
          value={tab}
          onChange={setTab}
          options={[
            { value: 'monthly', label: 'Monthly Returns' },
            { value: 'compounded', label: 'Compounded Returns', locked: true },
            { value: 'cumulative', label: 'Cumulative Returns', locked: true },
            { value: 'drawdowns', label: 'Drawdowns', locked: true },
            { value: 'beta', label: 'Beta (12M)', locked: true },
          ]}
          size="sm"
        />
      </div>
      {tab !== 'monthly' ? (
        <div className="py-14 px-5 text-center text-muted text-[12.5px]">
          <Lock size={22} className="text-subtle inline-block" />
          <div className="mt-2">Premium view — locked</div>
        </div>
      ) : isLoading || !data ? (
        <div className="p-5"><Skeleton h={220} /></div>
      ) : (
        <div className="p-5 overflow-x-auto">
          <table className="w-full border-separate font-mono tabular-nums" style={{ borderSpacing: 2 }}>
            <thead>
              <tr>
                <th style={{ width: 46 }} />
                {MONTHS.map(m => (
                  <th key={m} className="text-[10.5px] text-muted font-medium uppercase tracking-wider pb-1.5 text-center">{m}</th>
                ))}
                <th className="text-[10.5px] text-muted font-semibold uppercase tracking-wider pb-1.5 text-center pl-2">Total</th>
              </tr>
            </thead>
            <tbody>
              {years.map(yr => (
                <tr key={yr}>
                  <td className="text-[11px] text-muted font-medium pr-2 text-right">{yr}</td>
                  {data.years[String(yr)].map((v, i) => (
                    <td key={i}
                      style={{ background: cellBg(v), color: cellFg(v) }}
                      className="text-[11px] font-semibold text-center py-2 rounded-sm min-w-[54px]">
                      {v === null || v === undefined ? '' : `${v.toFixed(2)}%`}
                    </td>
                  ))}
                  <td
                    style={{ background: cellBg(data.totals[String(yr)]), color: cellFg(data.totals[String(yr)]) }}
                    className="text-[11px] font-bold text-center py-2 px-1.5 rounded-sm">
                    {data.totals[String(yr)] !== undefined ? `${data.totals[String(yr)].toFixed(2)}%` : ''}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  )
}
```

- [ ] **Step 3: Run — PASS**
- [ ] **Step 4: Commit**: `git add dashboard/src/components/dashboard/MonthlyPerfSection* && git commit -m "feat(dashboard): MonthlyPerfSection heatmap grid"`

### Task 4.10: Build `RiskMetricsCard`, `AlertsMiniCard`, `SystemPerfMini`, `RecentActivity`, `ActivePositionsTable`

**Files:** 5 widget files + smoke tests each.

- [ ] **Step 1: Smoke tests** for each (render without crash with seeded query client).

- [ ] **Step 2: Implement**

```tsx
// RiskMetricsCard.tsx
import { Card } from '../ui/Card'
import { Skeleton } from '../ui/Skeleton'
import { useRiskMetrics } from '../../hooks/useRiskMetrics'
import { formatCurrency, formatPercent } from '../../utils/format'

export function RiskMetricsCard() {
  const { data, isLoading } = useRiskMetrics()
  if (isLoading || !data) return <Card><Skeleton h={260} /></Card>

  type Row = { k: string; v: string; tone: 'green' | 'red' | 'yellow' | 'muted' }
  const rows: Row[] = [
    { k: 'Portfolio Delta', v: formatPercent(data.delta / 1), tone: data.delta >= 0 ? 'green' : 'red' },
    { k: 'Portfolio Theta', v: data.theta.toFixed(2), tone: data.theta >= 0 ? 'green' : 'red' },
    { k: 'Portfolio Vega',  v: formatPercent(data.vega), tone: 'green' },
    { k: 'VIX Index',       v: data.vix !== null ? data.vix.toFixed(2) : '—', tone: 'muted' },
    { k: 'VIX1D',           v: data.vix1d !== null ? data.vix1d.toFixed(2) : '—', tone: 'muted' },
    { k: 'IV Rank (SPY)',   v: data.ivRankSpy !== null ? `${(data.ivRankSpy * 100).toFixed(0)}%` : '—', tone: 'muted' },
    { k: 'Buying Power',    v: formatCurrency(data.buyingPower, 'USD', 0), tone: 'muted' },
    { k: 'Margin Used',     v: `${data.marginUsedPct}%`, tone: data.marginUsedPct > 70 ? 'red' : data.marginUsedPct > 55 ? 'yellow' : 'muted' },
  ]

  return (
    <Card>
      <h3 className="m-0 text-[15px] font-semibold mb-3">Risk Metrics</h3>
      {rows.map(r => (
        <div key={r.k} className="flex justify-between items-center py-[7px] border-b border-border last:border-0 text-[12.5px]">
          <span className="text-muted">{r.k}</span>
          <span className={`font-mono tabular-nums font-medium ${r.tone === 'green' ? 'text-up' : r.tone === 'red' ? 'text-down' : r.tone === 'yellow' ? 'text-[var(--yellow)]' : ''}`}>{r.v}</span>
        </div>
      ))}
    </Card>
  )
}
```

```tsx
// AlertsMiniCard.tsx
import { Card } from '../ui/Card'
import { Badge } from '../ui/Badge'
import { Skeleton } from '../ui/Skeleton'
import { Bell } from 'lucide-react'
import { useAlertsSummary } from '../../hooks/useAlertsSummary'

export function AlertsMiniCard({ onClick }: { onClick?: () => void }) {
  const { data, isLoading } = useAlertsSummary()
  return (
    <Card className="cursor-pointer" onClick={onClick}>
      <div className="flex justify-between items-center mb-3">
        <div className="text-[12px] text-muted font-medium">Alerts · last 24h</div>
        <Bell size={16} className="text-subtle" />
      </div>
      {isLoading || !data ? <Skeleton h={64} /> : (
        <>
          <div className="text-[26px] font-semibold text-[var(--fg-1)] mb-2 tabular-nums tracking-tight">{data.total}</div>
          <div className="flex gap-1.5 flex-wrap">
            <Badge tone="red" size="sm">{data.critical} critical</Badge>
            <Badge tone="yellow" size="sm">{data.warning} warning</Badge>
            <Badge tone="blue" size="sm">{data.info} info</Badge>
          </div>
        </>
      )}
    </Card>
  )
}
```

```tsx
// SystemPerfMini.tsx
import { Card } from '../ui/Card'
import { Badge } from '../ui/Badge'
import { Skeleton } from '../ui/Skeleton'
import { useSystemMetricsSample } from '../../hooks/useSystemMetrics'

function MiniBars({ vals, color, label }: { vals: number[]; color: string; label: string }) {
  const last = vals.length > 0 ? vals[vals.length - 1] : 0
  return (
    <div>
      <div className="flex justify-between text-[11px] mb-1">
        <span className="text-muted">{label}</span>
        <span className="font-mono font-medium">{last.toFixed(0)}%</span>
      </div>
      <div className="flex gap-[1.5px] items-end h-[26px]">
        {vals.map((v, i) => (
          <div key={i} className="flex-1 rounded-[1.5px]" style={{ background: color, height: `${Math.max(2, v)}%`, opacity: 0.3 + (i / vals.length) * 0.7 }} />
        ))}
      </div>
    </div>
  )
}

export function SystemPerfMini() {
  const { data, isLoading } = useSystemMetricsSample()
  if (isLoading || !data) return <Card><Skeleton h={170} /></Card>

  const asOf = new Date(data.asOf).toISOString().replace('T', ' ').slice(0, 19)
  const diskFreePct = 100 - data.diskUsedPct
  return (
    <Card>
      <div className="flex justify-between items-start mb-3">
        <div>
          <h3 className="m-0 text-[14px] font-semibold">System Performance</h3>
          <div className="text-[11px] text-muted mt-0.5 font-mono">as of {asOf} UTC</div>
        </div>
        <Badge tone="green" pulse size="sm">LIVE</Badge>
      </div>
      <div className="grid grid-cols-2 gap-3.5 gap-y-3">
        <MiniBars vals={data.cpu} color="#2f81f7" label="CPU" />
        <MiniBars vals={data.ram} color="#a371f7" label="RAM" />
        <MiniBars vals={data.network} color="#d29922" label="Network" />
        <div>
          <div className="flex justify-between text-[11px] mb-1">
            <span className="text-muted">Disk free</span>
            <span className="font-mono font-medium text-up">{data.diskFreeGb} GB · {diskFreePct}%</span>
          </div>
          <div className="h-2 bg-[var(--bg-1)] rounded overflow-hidden">
            <div className="h-full" style={{ width: `${data.diskUsedPct}%`, background: 'linear-gradient(to right,#3fb950 0%,#3fb950 70%,#d29922 85%,#f85149 100%)' }} />
          </div>
          <div className="text-[10px] text-subtle mt-1 font-mono">{data.diskUsedPct}% used of {data.diskTotalGb} GB</div>
        </div>
      </div>
    </Card>
  )
}
```

```tsx
// RecentActivity.tsx
import { Card } from '../ui/Card'
import { Button } from '../ui/Button'
import { Skeleton } from '../ui/Skeleton'
import { useRecentActivity } from '../../hooks/useRecentActivity'
import { ExternalLink, CheckCircle2, AlertTriangle, Play, XCircle, Repeat, TrendingUp, RefreshCw, FileText } from 'lucide-react'
import type { ActivityIcon, ActivityTone } from '../../types/activity'
import { formatDistanceToNow } from 'date-fns'

const ICONS: Record<ActivityIcon, typeof CheckCircle2> = {
  'check-circle-2': CheckCircle2, 'alert-triangle': AlertTriangle, 'play': Play, 'x-circle': XCircle,
  'repeat': Repeat, 'trending-up': TrendingUp, 'refresh-cw': RefreshCw, 'file-text': FileText,
}

const toneColor: Record<ActivityTone, string> = {
  green: 'var(--green)', red: 'var(--red)', yellow: 'var(--yellow)', blue: 'var(--blue)', purple: 'var(--purple)', muted: 'var(--fg-2)',
}

export function RecentActivity() {
  const { data, isLoading } = useRecentActivity(8)
  return (
    <Card padding={0}>
      <div className="px-5 py-4 border-b border-border flex justify-between items-center">
        <div>
          <h3 className="m-0 text-[15px] font-semibold">Recent Activity</h3>
          <div className="text-[12px] text-muted mt-0.5">Orders, signals and system events · last 24h</div>
        </div>
        <Button variant="secondary" size="sm" icon={ExternalLink}>View all</Button>
      </div>
      {isLoading || !data ? <div className="p-5"><Skeleton h={260} /></div> : (
        <div>
          {data.events.map((e, i) => {
            const IconCmp = ICONS[e.icon]
            return (
              <div key={e.id} className={`px-5 py-2.5 flex items-center gap-3 transition-colors ${i < data.events.length - 1 ? 'border-b border-border' : ''} hover:bg-[color:var(--fg-1)]/[0.02]`}>
                <div className="w-7 h-7 rounded-md shrink-0 flex items-center justify-center bg-[var(--bg-1)] border border-border">
                  <IconCmp size={14} color={toneColor[e.tone]} />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[13px] font-medium text-[var(--fg-1)]">{e.title}</div>
                  <div className="text-[11.5px] text-muted truncate font-mono">{e.subtitle}</div>
                </div>
                <span className="text-[11px] text-muted whitespace-nowrap font-mono">{formatDistanceToNow(new Date(e.timestamp), { addSuffix: true })}</span>
              </div>
            )
          })}
        </div>
      )}
    </Card>
  )
}
```

```tsx
// ActivePositionsTable.tsx
import { Card } from '../ui/Card'
import { Badge } from '../ui/Badge'
import { Button } from '../ui/Button'
import { Skeleton } from '../ui/Skeleton'
import { RefreshCw, Plus } from 'lucide-react'
import { usePositions } from '../../hooks/usePositions'
import { formatCurrency, formatPercent } from '../../utils/format'

interface Props { onNewCampaign?: () => void }

export function ActivePositionsTable({ onNewCampaign }: Props) {
  const { data, isLoading, refetch, isFetching } = usePositions({ status: 'OPEN' })
  const positions = data?.positions ?? []

  return (
    <Card padding={0}>
      <div className="px-5 py-4 border-b border-border flex justify-between items-center">
        <div>
          <h3 className="m-0 text-[15px] font-semibold">Active Positions</h3>
          <div className="text-[12px] text-muted mt-0.5">{positions.length} open</div>
        </div>
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" icon={RefreshCw} loading={isFetching} onClick={() => refetch()}>Refresh</Button>
          <Button variant="primary" size="sm" icon={Plus} onClick={onNewCampaign}>New Campaign</Button>
        </div>
      </div>
      {isLoading ? <div className="p-5"><Skeleton h={140} /></div> : (
        <table className="w-full border-collapse">
          <thead>
            <tr className="bg-[var(--bg-1)]">
              {['Symbol','Campaign','Qty','Mark','P&L','%','DTE'].map((h, i) => (
                <th key={h} className={`px-3 py-2.5 text-[11px] font-medium text-muted uppercase tracking-wider border-b border-border ${i < 2 ? 'text-left' : 'text-right'}`}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {positions.map(p => {
              const up = (p.unrealizedPnl ?? 0) >= 0
              return (
                <tr key={p.id} className="border-b border-border last:border-0">
                  <td className="px-3 py-2.5">
                    <div className="flex items-center gap-2">
                      <span className="font-mono font-semibold">{p.contractSymbol ?? p.symbol}</span>
                      {p.strategyType && <Badge tone="purple" size="sm">{p.strategyType}</Badge>}
                    </div>
                  </td>
                  <td className="px-3 py-2.5 text-[12px]">
                    {p.campaign ? <span className="text-accent font-medium">{p.campaign}</span> : <span className="text-subtle">—</span>}
                  </td>
                  <td className="px-3 py-2.5 text-right font-mono tabular-nums text-muted">{p.quantity}</td>
                  <td className="px-3 py-2.5 text-right font-mono tabular-nums">{formatCurrency(p.marketPrice ?? 0, 'USD')}</td>
                  <td className={`px-3 py-2.5 text-right font-mono tabular-nums font-medium ${up ? 'text-up' : 'text-down'}`}>
                    {formatCurrency(p.unrealizedPnl ?? 0, 'USD')}
                  </td>
                  <td className={`px-3 py-2.5 text-right text-[12px] ${up ? 'text-up' : 'text-down'}`}>
                    {up ? '▲' : '▼'} {Math.abs(p.unrealizedPnlPct ?? 0).toFixed(2)}%
                  </td>
                  <td className="px-3 py-2.5 text-right font-mono text-muted">{p.daysToExpiration ?? 0}d</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
    </Card>
  )
}
```

- [ ] **Step 3: Run** `cd dashboard && npm test -- components/dashboard`
- [ ] **Step 4: Commit**: `git add dashboard/src/components/dashboard && git commit -m "feat(dashboard): RiskMetrics, AlertsMini, SystemPerfMini, RecentActivity, ActivePositionsTable"`

### Task 4.11: Build the new `HomePage.tsx`

**Files:** Modify `dashboard/src/pages/HomePage.tsx`.

- [ ] **Step 1: Replace contents**

```tsx
// dashboard/src/pages/HomePage.tsx
import { motion } from 'motion/react'
import { DollarSign, TrendingUp, FolderKanban, RefreshCw } from 'lucide-react'
import { AssetFilter } from '../components/dashboard/AssetFilter'
import { StatCard } from '../components/ui/StatCard'
import { SummaryCard } from '../components/dashboard/SummaryCard'
import { MultiSeriesChart } from '../components/dashboard/MultiSeriesChart'
import { DrawdownsSection } from '../components/dashboard/DrawdownsSection'
import { MonthlyPerfSection } from '../components/dashboard/MonthlyPerfSection'
import { RiskMetricsCard } from '../components/dashboard/RiskMetricsCard'
import { AlertsMiniCard } from '../components/dashboard/AlertsMiniCard'
import { SystemPerfMini } from '../components/dashboard/SystemPerfMini'
import { RecentActivity } from '../components/dashboard/RecentActivity'
import { ActivePositionsTable } from '../components/dashboard/ActivePositionsTable'
import { Card } from '../components/ui/Card'
import { useAssetFilterStore } from '../stores/assetFilterStore'
import { usePerformanceSummary } from '../hooks/usePerformanceSummary'
import { useCampaignsSummary } from '../hooks/useCampaignsSummary'
import { formatCurrency, formatDelta } from '../utils/format'

function stagger(i: number) {
  return { initial: { opacity: 0, y: 8 }, animate: { opacity: 1, y: 0 }, transition: { duration: 0.28, delay: i * 0.04, ease: [0.4, 0, 0.2, 1] as const } }
}

export function HomePage() {
  const asset = useAssetFilterStore(s => s.asset)
  const { data: summary } = usePerformanceSummary(asset)
  const { data: camps } = useCampaignsSummary()

  const accountValue = summary ? formatCurrency(summary.base, 'USD') : '—'
  const accountDelta = summary ? formatDelta(summary.base * summary.m / 100, summary.m, 'USD') : ''

  return (
    <div className="p-8 flex flex-col gap-5">
      <motion.div {...stagger(0)} className="flex justify-between items-center gap-3 flex-wrap">
        <AssetFilter />
        <div className="flex items-center gap-2 text-[11.5px] text-muted font-mono">
          <RefreshCw size={12} />
          auto-refresh · 30s
        </div>
      </motion.div>

      <motion.div {...stagger(1)} className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard label="Account Value" value={accountValue} delta={accountDelta} deltaTone="green" icon={DollarSign} />
        <StatCard label="Open P&L" value="+$1,248.00" delta="3 positions · today" deltaTone="green" icon={TrendingUp} />
        <StatCard label="Active Campaigns" value={String(camps?.active ?? '—')} delta={camps?.detail} icon={FolderKanban} />
        <AlertsMiniCard />
      </motion.div>

      <motion.div {...stagger(2)} className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-4">
        <Card className="min-h-[330px]"><MultiSeriesChart asset={asset} /></Card>
        <div className="flex flex-col gap-4">
          <RiskMetricsCard />
          <SystemPerfMini />
        </div>
      </motion.div>

      <motion.div {...stagger(3)}><SummaryCard asset={asset} /></motion.div>
      <motion.div {...stagger(4)}><DrawdownsSection asset={asset} /></motion.div>
      <motion.div {...stagger(5)}><MonthlyPerfSection asset={asset} /></motion.div>
      <motion.div {...stagger(6)}><ActivePositionsTable /></motion.div>
      <motion.div {...stagger(7)}><RecentActivity /></motion.div>
    </div>
  )
}
```

- [ ] **Step 2: Typecheck + test**: `cd dashboard && npm run typecheck && npm test`
- [ ] **Step 3: Commit**: `git add dashboard/src/pages/HomePage.tsx && git commit -m "feat(dashboard): new HomePage matching kit Overview layout"`

### Task 4.12: Phase 4 smoke test

- [ ] **Step 1: Start worker**: `cd infra/cloudflare/worker && bun run dev &` (background)
- [ ] **Step 2: Start dashboard**: `cd dashboard && npm run dev`
- [ ] **Step 3: Browser**: open localhost, asset filter triggers overlays, chart toggles Portfolio/S&P/SWDA, range ALL/1W/etc, theme toggle works, Positions page next in Phase 5.
- [ ] **Step 4: Kill bg processes**: `kill %1` or Ctrl+C.
- [ ] **Step 5: No commit** — nothing changed.

---

(continues in `2026-04-20-dashboard-redesign-part4.md` for Phase 5.)
