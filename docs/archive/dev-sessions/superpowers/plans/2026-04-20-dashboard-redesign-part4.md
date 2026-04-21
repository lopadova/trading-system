# Dashboard Redesign — Implementation Plan (Part 4: Phase 5 + finalization)

> Continuation of `2026-04-20-dashboard-redesign-part3.md`.

---

## Phase 5 — Positions page + cleanup

**Checkpoint:** PositionsPage matches kit layout with donut charts; all old glassmorphism dead; acceptance criteria green.

### Task 5.1: Build `Donut` primitive

**Files:**
- Create: `dashboard/src/components/positions/Donut.test.tsx`
- Create: `dashboard/src/components/positions/Donut.tsx`

- [ ] **Step 1: Test**

```tsx
import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { Donut } from './Donut'

describe('Donut', () => {
  it('renders an SVG with the expected segment count', () => {
    render(<Donut segments={[{ label: 'A', value: 10, color: '#f00' }, { label: 'B', value: 20, color: '#0f0' }]} centerLabel="€30" centerSub="total" data-testid="d" />)
    const svg = screen.getByTestId('d').querySelector('svg')
    expect(svg?.querySelectorAll('circle').length).toBe(3) // bg + 2 segments
    expect(screen.getByText('€30')).toBeInTheDocument()
    expect(screen.getByText('total')).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Run — FAIL**
- [ ] **Step 3: Implement**

```tsx
// Donut.tsx
import type { HTMLAttributes } from 'react'
import type { ExposureSegment } from '../../types/breakdown'

export interface DonutProps extends HTMLAttributes<HTMLDivElement> {
  segments: ExposureSegment[]
  size?: number
  thickness?: number
  centerLabel: string
  centerSub: string
}

export function Donut({ segments, size = 180, thickness = 28, centerLabel, centerSub, ...rest }: DonutProps) {
  const total = segments.reduce((s, x) => s + x.value, 0) || 1
  const cx = size / 2, cy = size / 2
  const r = (size - thickness) / 2
  const c = 2 * Math.PI * r
  let offset = 0

  return (
    <div className="relative shrink-0" style={{ width: size, height: size }} {...rest}>
      <svg width={size} height={size} style={{ transform: 'rotate(-90deg)' }}>
        <circle cx={cx} cy={cy} r={r} fill="none" stroke="var(--bg-1)" strokeWidth={thickness} />
        {segments.map((s, i) => {
          const frac = s.value / total
          const dash = frac * c
          const gap = c - dash
          const el = (
            <circle key={i} cx={cx} cy={cy} r={r} fill="none" stroke={s.color} strokeWidth={thickness} strokeDasharray={`${dash} ${gap}`} strokeDashoffset={-offset} />
          )
          offset += dash
          return el
        })}
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <div className="font-mono text-[18px] font-semibold tabular-nums text-[var(--fg-1)]">{centerLabel}</div>
        <div className="text-[10.5px] text-muted mt-0.5 uppercase tracking-wider">{centerSub}</div>
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Run — PASS**
- [ ] **Step 5: Commit**: `git add dashboard/src/components/positions/Donut* && git commit -m "feat(dashboard): Donut primitive"`

### Task 5.2: Build `PositionsBreakdown` widget

**Files:**
- Create: `dashboard/src/components/positions/PositionsBreakdown.tsx`

- [ ] **Step 1: Implement**

```tsx
// PositionsBreakdown.tsx
import { Card } from '../ui/Card'
import { Skeleton } from '../ui/Skeleton'
import { Donut } from './Donut'
import { usePositionsBreakdown } from '../../hooks/usePositionsBreakdown'
import type { ExposureSegment } from '../../types/breakdown'

function Legend({ segments, total }: { segments: ExposureSegment[]; total: number }) {
  return (
    <div className="flex-1 flex flex-col gap-2">
      {segments.map(s => {
        const pct = (s.value / total) * 100
        return (
          <div key={s.label} className="flex items-center gap-2.5 text-[12.5px]">
            <span className="w-2.5 h-2.5 rounded-sm shrink-0" style={{ background: s.color }} />
            <span className="flex-1 text-[var(--fg-1)]">{s.label}</span>
            <span className="font-mono text-muted tabular-nums">{pct.toFixed(1)}%</span>
            <span className="font-mono font-medium tabular-nums min-w-[70px] text-right">€{s.value.toLocaleString('en-US')}</span>
          </div>
        )
      })}
    </div>
  )
}

export function PositionsBreakdown() {
  const { data, isLoading } = usePositionsBreakdown()
  if (isLoading || !data) return <div className="grid grid-cols-1 lg:grid-cols-2 gap-4"><Card><Skeleton h={200} /></Card><Card><Skeleton h={200} /></Card></div>

  const sumS = data.byStrategy.reduce((s, x) => s + x.value, 0)
  const sumA = data.byAsset.reduce((s, x) => s + x.value, 0)

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
      <Card>
        <h3 className="m-0 text-[14px] font-semibold mb-3.5">Exposure by Strategy</h3>
        <div className="flex items-center gap-5">
          <Donut segments={data.byStrategy} centerLabel={`€${(sumS / 1000).toFixed(1)}k`} centerSub="total" />
          <Legend segments={data.byStrategy} total={sumS} />
        </div>
      </Card>
      <Card>
        <h3 className="m-0 text-[14px] font-semibold mb-3.5">Exposure by Asset</h3>
        <div className="flex items-center gap-5">
          <Donut segments={data.byAsset} centerLabel={`€${(sumA / 1000).toFixed(1)}k`} centerSub="total" />
          <Legend segments={data.byAsset} total={sumA} />
        </div>
      </Card>
    </div>
  )
}
```

- [ ] **Step 2: Typecheck + test**: `cd dashboard && npm run typecheck`
- [ ] **Step 3: Commit**: `git add dashboard/src/components/positions/PositionsBreakdown* && git commit -m "feat(dashboard): PositionsBreakdown (strategy + asset donuts)"`

### Task 5.3: Build `PositionsKpiStrip`

**Files:** Create `dashboard/src/components/positions/PositionsKpiStrip.tsx`.

- [ ] **Step 1: Implement**

```tsx
// PositionsKpiStrip.tsx
import { StatCard } from '../ui/StatCard'
import { TrendingUp, CheckCircle2, Layers, Calendar, DollarSign } from 'lucide-react'
import { usePositions } from '../hooks/usePositions'
import { useRiskMetrics } from '../../hooks/useRiskMetrics'
import { formatCurrency } from '../../utils/format'

export function PositionsKpiStrip() {
  const { data: positions } = usePositions({ status: 'OPEN' })
  const { data: risk } = useRiskMetrics()
  const open = positions?.positions ?? []
  const openPl = open.reduce((s, p) => s + (p.unrealizedPnl ?? 0), 0)
  const avgDte = open.length ? Math.round(open.reduce((s, p) => s + (p.daysToExpiration ?? 0), 0) / open.length) : 0

  return (
    <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
      <StatCard label="Open P&L" value={formatCurrency(openPl, 'USD')} delta="today" deltaTone={openPl >= 0 ? 'green' : 'red'} icon={TrendingUp} />
      <StatCard label="Realized · 7d" value="—" delta="rolling 7 days" deltaTone="muted" icon={CheckCircle2} />
      <StatCard label="Open Positions" value={String(open.length)} delta={`avg DTE ${avgDte}d`} icon={Layers} />
      <StatCard label="Avg DTE" value={`${avgDte}d`} delta="across open book" icon={Calendar} />
      <StatCard label="Buying Power" value={risk ? formatCurrency(risk.buyingPower, 'USD', 0) : '—'} delta={risk ? `margin ${risk.marginUsedPct}%` : ''} deltaTone={risk && risk.marginUsedPct > 70 ? 'red' : 'yellow'} icon={DollarSign} />
    </div>
  )
}
```

Fix the import `../hooks/usePositions` → `../../hooks/usePositions`. Verify.

- [ ] **Step 2: Commit**: `git add dashboard/src/components/positions/PositionsKpiStrip* && git commit -m "feat(dashboard): PositionsKpiStrip (5 StatCards)"`

### Task 5.4: Build `PositionsFilterBar`

**Files:** Create `dashboard/src/components/positions/PositionsFilterBar.tsx`.

- [ ] **Step 1: Implement**

```tsx
// PositionsFilterBar.tsx
import { useState } from 'react'
import { Search, RefreshCw, List, LayoutGrid } from 'lucide-react'
import { Button } from '../ui/Button'
import { SegmentedControl } from '../ui/SegmentedControl'
import { cn } from '../../utils/cn'

export type PositionStatus = 'All' | 'Open' | 'Closed' | 'Pending'
export type ViewMode = 'table' | 'cards'
const STATUSES: PositionStatus[] = ['All', 'Open', 'Closed', 'Pending']
const TYPES = ['All', 'Iron Condor', 'Put Spread', 'Call Spread', 'Long Call', 'Short Strangle']

interface Props {
  status: PositionStatus
  setStatus: (s: PositionStatus) => void
  typeFilter: string
  setTypeFilter: (t: string) => void
  query: string
  setQuery: (q: string) => void
  view: ViewMode
  setView: (v: ViewMode) => void
  onRefresh: () => void
  isFetching: boolean
}

export function PositionsFilterBar({ status, setStatus, typeFilter, setTypeFilter, query, setQuery, view, setView, onRefresh, isFetching }: Props) {
  return (
    <div className="px-4 py-3 flex gap-2.5 items-center flex-wrap border-b border-border">
      <div className="relative flex-1 min-w-[220px] max-w-[320px]">
        <Search size={13} className="absolute left-2.5 top-2.5 text-muted" />
        <input
          value={query}
          onChange={e => setQuery(e.target.value)}
          placeholder="Search symbol…"
          className="w-full py-[7px] pl-[30px] pr-2.5 bg-[var(--bg-1)] border border-border rounded-md text-[12.5px] focus-visible:outline-none focus-visible:border-[var(--border-focus)]"
        />
      </div>
      <SegmentedControl<PositionStatus>
        value={status}
        onChange={setStatus}
        options={STATUSES.map(s => ({ value: s, label: s }))}
        size="sm"
      />
      <select
        value={typeFilter}
        onChange={e => setTypeFilter(e.target.value)}
        className="px-2.5 py-1.5 bg-[var(--bg-1)] border border-border rounded-md text-[12.5px] focus-visible:outline-none focus-visible:border-[var(--border-focus)]"
      >
        {TYPES.map(t => <option key={t} value={t}>{t === 'All' ? 'All types' : t}</option>)}
      </select>
      <div className="ml-auto flex gap-2 items-center">
        <Button variant="secondary" size="sm" icon={RefreshCw} loading={isFetching} onClick={onRefresh}>Refresh</Button>
        <div className="flex gap-0.5 p-[3px] bg-[var(--bg-1)] border border-border rounded-md">
          <button type="button" aria-label="Table view" aria-pressed={view === 'table'} onClick={() => setView('table')}
            className={cn('px-2 py-1 rounded flex items-center', view === 'table' ? 'bg-[var(--blue)] text-white' : 'text-muted')}>
            <List size={14} />
          </button>
          <button type="button" aria-label="Card view" aria-pressed={view === 'cards'} onClick={() => setView('cards')}
            className={cn('px-2 py-1 rounded flex items-center', view === 'cards' ? 'bg-[var(--blue)] text-white' : 'text-muted')}>
            <LayoutGrid size={14} />
          </button>
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Commit**: `git add dashboard/src/components/positions/PositionsFilterBar* && git commit -m "feat(dashboard): PositionsFilterBar"`

### Task 5.5: Rewrite `PositionsTable` to kit styling

**Files:** Modify `dashboard/src/components/positions/PositionsTable.tsx`.

- [ ] **Step 1: Replace contents**

```tsx
// PositionsTable.tsx
import { Badge } from '../ui/Badge'
import type { Position } from '../../types/position'
import { formatCurrency } from '../../utils/format'

interface Props { positions: Position[] }

export function PositionsTable({ positions }: Props) {
  if (positions.length === 0) {
    return <div className="text-center py-12 text-muted">No positions match</div>
  }
  return (
    <table className="w-full border-collapse">
      <thead>
        <tr className="bg-[var(--bg-1)]">
          {['Symbol','Status','Qty','Cost','Mark','P&L','%','Δ','Θ','DTE'].map((h, i) => (
            <th key={h} className={`px-3 py-2.5 text-[11px] font-medium text-muted uppercase tracking-wider border-b border-border ${i < 2 ? 'text-left' : 'text-right'}`}>{h}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {positions.map(p => {
          const up = (p.unrealizedPnl ?? 0) >= 0
          const statusTone = p.status === 'OPEN' ? 'green' : p.status === 'CLOSED' ? 'muted' : 'yellow'
          return (
            <tr key={p.id} className="border-b border-border last:border-0">
              <td className="px-3 py-2.5">
                <div className="flex items-center gap-2">
                  <span className="font-mono font-semibold">{p.contractSymbol ?? p.symbol}</span>
                  {p.strategyType && <Badge tone="purple" size="sm">{p.strategyType}</Badge>}
                </div>
              </td>
              <td className="px-3 py-2.5"><Badge tone={statusTone} size="sm">{p.status}</Badge></td>
              <td className="px-3 py-2.5 text-right font-mono tabular-nums text-muted">{p.quantity}</td>
              <td className="px-3 py-2.5 text-right font-mono tabular-nums text-muted">{formatCurrency(p.avgCost ?? 0, 'USD')}</td>
              <td className="px-3 py-2.5 text-right font-mono tabular-nums">{formatCurrency(p.marketPrice ?? 0, 'USD')}</td>
              <td className={`px-3 py-2.5 text-right font-mono tabular-nums font-medium ${up ? 'text-up' : 'text-down'}`}>{formatCurrency(p.unrealizedPnl ?? 0, 'USD')}</td>
              <td className={`px-3 py-2.5 text-right text-[12px] ${up ? 'text-up' : 'text-down'}`}>{up ? '▲' : '▼'} {Math.abs(p.unrealizedPnlPct ?? 0).toFixed(2)}%</td>
              <td className="px-3 py-2.5 text-right font-mono text-muted">{p.delta?.toFixed(2) ?? '+0.00'}</td>
              <td className="px-3 py-2.5 text-right font-mono text-muted">{p.theta?.toFixed(2) ?? '+0.00'}</td>
              <td className="px-3 py-2.5 text-right font-mono text-muted">{p.daysToExpiration ?? 0}d</td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
```

- [ ] **Step 2: Commit**: `git add dashboard/src/components/positions/PositionsTable.tsx && git commit -m "refactor(dashboard): kit-styled PositionsTable"`

### Task 5.6: Rewrite `PositionCard` to kit styling

**Files:** Modify `dashboard/src/components/positions/PositionCard.tsx`.

- [ ] **Step 1: Replace contents**

```tsx
// PositionCard.tsx
import { Card } from '../ui/Card'
import { Badge } from '../ui/Badge'
import type { Position } from '../../types/position'
import { formatCurrency } from '../../utils/format'

export function PositionCard({ position }: { position: Position }) {
  const up = (position.unrealizedPnl ?? 0) >= 0
  const statusTone = position.status === 'OPEN' ? 'green' : position.status === 'CLOSED' ? 'muted' : 'yellow'
  return (
    <Card padding={16}>
      <div className="flex justify-between items-start mb-2.5">
        <div>
          <div className="font-mono font-semibold text-[13px]">{position.contractSymbol ?? position.symbol}</div>
          <div className="flex gap-1 mt-1.5">
            {position.strategyType && <Badge tone="purple" size="sm">{position.strategyType}</Badge>}
            <Badge tone={statusTone} size="sm">{position.status}</Badge>
          </div>
        </div>
        <div className="text-right">
          <div className={`font-mono text-[16px] font-semibold tabular-nums ${up ? 'text-up' : 'text-down'}`}>
            {formatCurrency(Math.abs(position.unrealizedPnl ?? 0), 'USD', 0).replace(/^/, up ? '+' : '-')}
          </div>
          <div className={`text-[11px] ${up ? 'text-up' : 'text-down'}`}>{up ? '▲' : '▼'} {Math.abs(position.unrealizedPnlPct ?? 0).toFixed(2)}%</div>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-2 text-[11.5px] pt-2.5 border-t border-border">
        <div><span className="text-muted">Qty</span> <span className="font-mono">{position.quantity}</span></div>
        <div><span className="text-muted">DTE</span> <span className="font-mono">{position.daysToExpiration ?? 0}d</span></div>
        <div><span className="text-muted">Δ</span> <span className="font-mono">{position.delta?.toFixed(2) ?? '0.00'}</span></div>
        <div><span className="text-muted">Θ</span> <span className="font-mono">{position.theta?.toFixed(2) ?? '0.00'}</span></div>
      </div>
    </Card>
  )
}
```

- [ ] **Step 2: Commit**: `git add dashboard/src/components/positions/PositionCard.tsx && git commit -m "refactor(dashboard): kit-styled PositionCard"`

### Task 5.7: Rewrite `PositionsPage.tsx`

**Files:** Modify `dashboard/src/pages/PositionsPage.tsx`.

- [ ] **Step 1: Replace contents**

```tsx
// dashboard/src/pages/PositionsPage.tsx
import { useState } from 'react'
import { motion } from 'motion/react'
import { Card } from '../components/ui/Card'
import { FilterOverlay } from '../components/ui/FilterOverlay'
import { Skeleton } from '../components/ui/Skeleton'
import { PositionsBreakdown } from '../components/positions/PositionsBreakdown'
import { PositionsKpiStrip } from '../components/positions/PositionsKpiStrip'
import { PositionsFilterBar, type PositionStatus, type ViewMode } from '../components/positions/PositionsFilterBar'
import { PositionsTable } from '../components/positions/PositionsTable'
import { PositionCard } from '../components/positions/PositionCard'
import { usePositions } from '../hooks/usePositions'

function stagger(i: number) {
  return { initial: { opacity: 0, y: 8 }, animate: { opacity: 1, y: 0 }, transition: { duration: 0.28, delay: i * 0.04, ease: [0.4, 0, 0.2, 1] as const } }
}

export function PositionsPage() {
  const [status, setStatus] = useState<PositionStatus>('All')
  const [typeFilter, setTypeFilter] = useState('All')
  const [query, setQuery] = useState('')
  const [view, setView] = useState<ViewMode>('table')

  const filter = status === 'All' ? undefined : status.toUpperCase()
  const { data, isLoading, isFetching, refetch } = usePositions({ status: filter })
  const positions = (data?.positions ?? []).filter(p =>
    (typeFilter === 'All' || p.strategyType === typeFilter) &&
    (query === '' || (p.contractSymbol ?? p.symbol ?? '').toLowerCase().includes(query.toLowerCase()))
  )

  return (
    <div className="p-8 flex flex-col gap-4">
      <motion.div {...stagger(0)}><PositionsBreakdown /></motion.div>
      <motion.div {...stagger(1)}><PositionsKpiStrip /></motion.div>
      <motion.div {...stagger(2)}>
        <Card padding={0}>
          <PositionsFilterBar
            status={status} setStatus={setStatus}
            typeFilter={typeFilter} setTypeFilter={setTypeFilter}
            query={query} setQuery={setQuery}
            view={view} setView={setView}
            onRefresh={() => refetch()}
            isFetching={isFetching}
          />
          <div className="relative min-h-[240px]">
            <FilterOverlay visible={isFetching && (data?.positions.length ?? 0) === 0} label="Loading positions…" />
            {isLoading ? <div className="p-5"><Skeleton h={240} /></div> : view === 'table' ? (
              <PositionsTable positions={positions} />
            ) : (
              <div className="p-4 grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3.5">
                {positions.map(p => <PositionCard key={p.id} position={p} />)}
              </div>
            )}
          </div>
          <div className="px-4 py-2 border-t border-border text-[11px] text-muted text-center">
            {data ? `Last updated: ${new Date(data.timestamp).toLocaleString()} · Auto-refresh every 30s` : ''}
          </div>
        </Card>
      </motion.div>
    </div>
  )
}
```

- [ ] **Step 2: Typecheck + test**: `cd dashboard && npm run typecheck && npm test`
- [ ] **Step 3: Commit**: `git add dashboard/src/pages/PositionsPage.tsx && git commit -m "feat(dashboard): new PositionsPage with breakdown + filter bar + view toggle"`

### Task 5.8: Restyle `AlertsPage`, `CampaignsPage`, `SettingsPage`

**Files:** Modify those 3 pages.

- [ ] **Step 1: For each page**: wrap top-level in `<div className="p-8 flex flex-col gap-5">`. Replace any `card-clean`/`metric-value`/`badge badge-*` with `<Card>` / `<Badge>`. Replace custom color classes with kit tokens (`text-muted`, `text-up`, `text-down`).

- [ ] **Step 2: Run tests**: `cd dashboard && npm test`
- [ ] **Step 3: Commit**: `git add dashboard/src/pages/AlertsPage.tsx dashboard/src/pages/CampaignsPage.tsx dashboard/src/pages/SettingsPage.tsx && git commit -m "refactor(dashboard): restyle Alerts/Campaigns/Settings to kit tokens"`

### Task 5.9: Delete deprecated `useSystemStatus` + old `PositionsSummary`/`PositionFilters`

**Files:**
- Delete: `dashboard/src/components/positions/PositionsSummary.tsx` (replaced by PositionsKpiStrip)
- Delete: `dashboard/src/components/positions/PositionFilters.tsx` (replaced by PositionsFilterBar)
- Modify: `dashboard/src/hooks/useSystemStatus.ts` — keep only the old hook if referenced elsewhere, or delete if unused.

- [ ] **Step 1: Check for references**

Run: `cd dashboard && grep -rn "PositionsSummary\|PositionFilters\b" src`

- [ ] **Step 2: If only PositionsPage referenced them (and it no longer does), delete**

Run: `rm dashboard/src/components/positions/PositionsSummary.tsx dashboard/src/components/positions/PositionFilters.tsx`

- [ ] **Step 3: Run lint + typecheck**: `cd dashboard && npm run lint && npm run typecheck`

- [ ] **Step 4: Commit**: `git add -A dashboard/src/components/positions && git commit -m "chore(dashboard): remove replaced PositionsSummary/PositionFilters"`

### Task 5.10: Final sweep — `grep` for stale classes and dead mocks

- [ ] **Step 1: Search for glass/gradient remnants**

Run: `cd dashboard && grep -rn "card-clean\|metric-value\|positive-glow\|negative-glow\|backdrop-blur\|badge-green\|badge-red\|badge-yellow\|badge-blue" src`
Expected: **zero matches**.

- [ ] **Step 2: Search for hardcoded colors that should be vars**

Run: `grep -rn "#3b82f6\|#8b5cf6\|rgba(59, 130" dashboard/src`
Expected: zero matches except in `vitest` mocks.

- [ ] **Step 3: Run the whole suite**

Run: `cd dashboard && npm run typecheck && npm run lint && npm test && npm run build`
All green.

Run: `cd infra/cloudflare/worker && bun test`
All green.

- [ ] **Step 4: If anything flagged, fix inline, then re-run step 3**

- [ ] **Step 5: If changes made, commit**: `git add -A && git commit -m "chore(dashboard): purge glassmorphism remnants"`

### Task 5.11: Acceptance-criteria sweep

Manually verify each item from spec §15 (`docs/superpowers/specs/2026-04-20-dashboard-redesign-design.md`):

- [ ] **Overview page**
  - Asset filter chips control every asset-scoped widget
  - 4 KPIs including Alerts-24h card
  - Chart with Portfolio/S&P500/SWDA toggles + 1W/1M/3M/YTD/1Y/**ALL** range
  - Risk Metrics includes VIX + VIX1D
  - SystemPerfMini present with CPU/RAM/Network/Disk + asOf
  - Summary card with 6 horizons + **% / €** toggle (values match kit example)
  - Drawdowns section (chart + Worst Drawdowns table)
  - Monthly Performance heatmap (year × month) with locked tabs
  - Active Positions table has **Campaign** column
  - Recent Activity below positions
  - 420ms overlay on asset filter change

- [ ] **Positions page**
  - 2 Donuts (Strategy + Asset)
  - 5 KPI cards
  - Filter bar: search + status chips + type select + Table/Card toggle

- [ ] **Design system**
  - `index.css` — no glass classes
  - Dark and light theme switch via header button
  - Strategy Wizard still amber; wizard tests pass

- [ ] **Backend**
  - All new endpoints return JSON; CORS passes (tested via dashboard fetch)
  - `positions/active` includes `campaign`

- [ ] **Quality**
  - `dotnet test` — untouched, passes
  - `cd dashboard && npm test && npm run typecheck && npm run build` — passes
  - `cd infra/cloudflare/worker && bun test` — passes

### Task 5.12: Knowledge base update

**Files:**
- Modify: `knowledge/lessons-learned.md`
- Modify: `knowledge/errors-registry.md` (if any new ERR-XXX discovered)
- Modify: `knowledge/skill-changelog.md`

- [ ] **Step 1: Append at least 1 lesson learned**

Example entry:

```
- LESSON-NNN: Dashboard — Design tokens via CSS vars + Tailwind @theme bridge keeps kit fidelity without locking into inline styles
  Context: Dashboard redesign 2026-04-20
  Discovery: Using CSS custom properties as single source of truth (defined on :root) and exposing them as Tailwind utility classes via @theme lets every component consume `bg-surface`, `border-border`, `text-muted`, while dark/light theme switching is a one-line data-theme toggle — no style prop rebind needed.
  Impact: All widgets theme-switch instantly; future palette tweaks only touch index.css.
  Reference: docs/superpowers/specs/2026-04-20-dashboard-redesign-design.md §4
```

- [ ] **Step 2: If new errors discovered (e.g. VIX endpoint missing subscription), append ERR-XXX**

- [ ] **Step 3: Bump affected skill versions** (if skill-dotnet, skill-cloudflare, skill-react-dashboard edited during execution)

- [ ] **Step 4: Commit**: `git add knowledge && git commit -m "docs: lessons learned from dashboard redesign"`

### Task 5.13: Sync KB → Rules

- [ ] **Step 1: Run sync script**

Run (bash or PS depending on shell):
- `./scripts/sync-kb-to-rules.sh`  — if the script exists and shell is bash-capable
- Otherwise: `./scripts/Sync-KBToRules.ps1`

- [ ] **Step 2: Inspect the diff** in `.claude/rules/`

Run: `git diff .claude/rules/`

- [ ] **Step 3: Commit**: `git add .claude/rules && git commit -m "chore: sync KB -> rules after dashboard redesign"`

### Task 5.14: Final state verification + dev smoke

- [ ] **Step 1: Start backend worker**

Run: `cd infra/cloudflare/worker && bun run dev`  (background or separate shell)

- [ ] **Step 2: Start dashboard**

Run: `cd dashboard && npm run dev`

- [ ] **Step 3: Browser verification**

Open `http://localhost:5173` (or printed port):
- Navigate Overview → verify every widget renders real data
- Flip Asset filter → overlays fade in; values update
- Change chart range → chart updates
- Toggle Portfolio/S&P/SWDA → series appear/disappear
- Click Sun/Moon → theme flips
- Navigate Positions → donut charts, filter bar, table/card toggle all work
- Navigate Strategy Wizard → amber theme still active, wizard still functional

- [ ] **Step 4: Stop**: Ctrl+C both.

- [ ] **Step 5: No final commit** — everything already committed.

---

## Post-Implementation: Self-review

After the last task completes:

1. **Spec coverage** — walk every acceptance-criteria checkbox in spec §15 and confirm ✔.
2. **No glass** — confirm no residual glassmorphism styles or classes.
3. **All tests green** — dashboard, worker, .NET services untouched.
4. **Commit log** — each task should have 1-2 targeted commits; verify with `git log --oneline HEAD~<n>..HEAD`.
5. **Knowledge base** — at least one lesson + any new ERR entries present.
6. **No bumped wizard regressions** — wizard amber intact, wizard tests green.

If any item fails, create a follow-up task and fix before declaring done.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-04-20-dashboard-redesign{,-part2,-part3,-part4}.md`.** Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task + two-stage review between tasks. Best for a plan of this size (~60 tasks across 5 phases). Uses skill `superpowers:subagent-driven-development`.
2. **Inline Execution** — all tasks in the current session with batch checkpoints. Uses skill `superpowers:executing-plans`.

**Which approach do you want?**
