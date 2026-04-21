# Dashboard Redesign — Design Document

**Date:** 2026-04-20
**Status:** Draft — awaiting user review
**Author:** Claude (on behalf of Lorenzo Padovani, Padosoft)
**Scope:** Full rewrite of the trading-system dashboard frontend (React/Vite/TypeScript) to adopt the "Trading System Design System" kit (Bloomberg × GitHub aesthetic), with new widgets wired to real Cloudflare Worker APIs.

---

## 1. Executive Summary

The current dashboard uses a glassmorphism aesthetic (blur, gradients, animated background, glow shadows). The target design system delivered in `docs/design-kit/extracted/trading-system-design-system/` follows the opposite philosophy: **flat dark, border-heavy, quiet** — a Bloomberg-terminal-crossed-with-GitHub look.

The design kit ships a complete JSX implementation of Overview, Positions, System Health, IVTS, Logs, Settings, Alerts and Campaigns pages, plus shared widgets (Summary, Drawdowns, MonthlyPerformance, VIX metrics, Alerts-24h mini, SystemPerformance mini, Donut breakdown). The kit is **React 18 CDN + Babel standalone + inline styles**; we port it **1:1** to the project's React 19 + TypeScript + Tailwind 4 + Recharts stack, wiring mock widgets to real Worker endpoints.

**Outcome:** every widget specified in the user's brief is already designed in the kit; our job is to translate from JSX/inline-style to TSX/Tailwind, rewrite `index.css` to the kit's token system, extend the Cloudflare Worker with new API endpoints, and replace mock series with React Query hooks pointed at the real API.

---

## 2. Architecture Decisions (confirmed with user)

| # | Decision | Value |
|---|---|---|
| D1 | Frontend framework | Keep React 19 + Vite 8 + TS strict (no rewrite to vanilla TS / other framework) |
| D2 | Port strategy | **Approach A** — Hybrid Tailwind 4 + CSS vars. Inline `style={{}}` in kit → Tailwind classes when simple, CSS vars elsewhere |
| D3 | Backend scope | Extend the Cloudflare Worker with new endpoints for all new widgets (VIX, performance, drawdowns, monthly, system metrics, breakdowns, activity) |
| D4 | Strategy Wizard | Keep existing JSX/logic intact; only harmonize its CSS variables with the kit's `.wizard-root` sub-theme (amber palette already defined) |
| D5 | Theme | Implement both dark (default) and light, with a Sun/Moon toggle in the Header; persist selection in localStorage |
| D6 | Icons | Lucide-react exclusively (already a dep) — no emoji, no custom SVG decoration except the logo |
| D7 | Charts | Recharts for multi-series chart + drawdowns (already a dep); inline `<svg>` for Donut and Monthly heatmap grid (lighter, matches kit output exactly) |
| D8 | Animation | `motion` (Framer Motion successor, already a dep) for staggered fade-up entrance — 280ms ease-out, matches kit |
| D9 | Data fetching | React Query + `ky` (already deps). New hooks per endpoint, following pattern of `usePositions`/`useAlerts`/`useCampaigns`/`useSystemStatus` |

---

## 3. Target File Structure

```
dashboard/
├── src/
│   ├── index.css                          ← REWRITE (adopt kit tokens)
│   ├── App.tsx                            ← REWRITE (TanStack Router migration optional)
│   ├── main.tsx                           ← unchanged
│   ├── types/
│   │   ├── performance.ts                 ← NEW — SummaryData, PerfSeriesPoint, etc.
│   │   ├── drawdown.ts                    ← NEW — WorstDrawdown, DrawdownPoint
│   │   ├── risk.ts                        ← NEW — VixData, RiskMetrics
│   │   ├── system.ts                      ← NEW — SystemMetricsSample
│   │   ├── activity.ts                    ← NEW — ActivityEvent
│   │   ├── breakdown.ts                   ← NEW — ExposureSegment
│   │   ├── position.ts                    ← EXTEND — add `campaign: string | null`
│   │   ├── alert.ts                       ← unchanged
│   │   └── campaign.ts                    ← unchanged
│   ├── hooks/
│   │   ├── usePerformanceSummary.ts       ← NEW
│   │   ├── usePerformanceSeries.ts        ← NEW
│   │   ├── useDrawdowns.ts                ← NEW
│   │   ├── useMonthlyReturns.ts           ← NEW
│   │   ├── useRiskMetrics.ts              ← NEW (VIX, VIX1D, portfolio greeks)
│   │   ├── useSystemMetrics.ts            ← REWRITE — CPU/RAM/Network/HDD series + asOf
│   │   ├── useRecentActivity.ts           ← NEW
│   │   ├── useAlertsSummary.ts            ← NEW (24h counts by severity)
│   │   ├── usePositionsBreakdown.ts       ← NEW (by strategy, by asset)
│   │   ├── useCampaignsSummary.ts         ← NEW (active/paused/draft)
│   │   ├── usePositions.ts                ← unchanged (already integrated)
│   │   ├── useAlerts.ts                   ← unchanged
│   │   └── useCampaigns.ts                ← unchanged
│   ├── stores/
│   │   ├── assetFilterStore.ts            ← NEW (Zustand: asset = all|systematic|options|other)
│   │   ├── themeStore.ts                  ← NEW (dark|light, localStorage-backed)
│   │   └── (existing stores unchanged)
│   ├── components/
│   │   ├── ui/                            ← UPDATE / REWRITE primitives
│   │   │   ├── Card.tsx                   ← rewrite: bg-2 / border-1 / radius-lg
│   │   │   ├── Badge.tsx                  ← rewrite: 6 tones (green/red/yellow/blue/purple/muted) + pulse
│   │   │   ├── Button.tsx                 ← rewrite: primary/secondary/ghost/danger × sm/md/lg + icon slot
│   │   │   ├── StatCard.tsx               ← NEW
│   │   │   ├── Spinner.tsx                ← NEW
│   │   │   ├── Skeleton.tsx               ← NEW (shimmer animation)
│   │   │   ├── FilterOverlay.tsx          ← NEW
│   │   │   ├── SegmentedControl.tsx       ← NEW (ranges / tabs / unit toggle)
│   │   │   ├── Toggle.tsx                 ← unchanged
│   │   │   ├── Select.tsx                 ← unchanged
│   │   │   ├── Input.tsx                  ← unchanged
│   │   │   └── Toast.tsx                  ← unchanged
│   │   ├── layout/
│   │   │   ├── Sidebar.tsx                ← rewrite: kit layout (240px, Paper-Trading footer, IBKR dot)
│   │   │   ├── Header.tsx                 ← rewrite: operational badge + bell + theme toggle + avatar
│   │   │   └── Layout.tsx                 ← rewrite: flex shell
│   │   ├── dashboard/                     ← NEW — Overview-specific widgets
│   │   │   ├── AssetFilter.tsx
│   │   │   ├── SummaryCard.tsx
│   │   │   ├── MultiSeriesChart.tsx       ← Recharts-backed
│   │   │   ├── DrawdownsSection.tsx       ← Recharts for chart, table sidekick
│   │   │   ├── MonthlyPerfSection.tsx     ← inline SVG-less grid (CSS grid)
│   │   │   ├── RiskMetricsCard.tsx        ← includes VIX, VIX1D
│   │   │   ├── AlertsMiniCard.tsx
│   │   │   ├── SystemPerfMini.tsx
│   │   │   ├── RecentActivity.tsx
│   │   │   └── ActivePositionsTable.tsx   ← with Campaign column
│   │   ├── positions/
│   │   │   ├── PositionsBreakdown.tsx     ← NEW — 2× Donut (strategy + asset)
│   │   │   ├── PositionsKpiStrip.tsx      ← NEW — 5 StatCards
│   │   │   ├── PositionsFilterBar.tsx     ← NEW — search + status chips + type select + view toggle
│   │   │   ├── PositionsTable.tsx         ← rewrite: kit styling
│   │   │   ├── PositionCard.tsx           ← rewrite: kit styling
│   │   │   ├── PositionFilters.tsx        ← REPLACE with PositionsFilterBar
│   │   │   └── PositionsSummary.tsx       ← REPLACE with PositionsKpiStrip
│   │   ├── alerts/                        ← minor restyle to kit
│   │   ├── campaigns/                     ← minor restyle to kit
│   │   └── strategy-wizard/               ← CSS-var harmonization only (no JSX changes)
│   ├── pages/
│   │   ├── HomePage.tsx                   ← FULL REWRITE (aliases to OverviewPage layout)
│   │   ├── PositionsPage.tsx              ← FULL REWRITE
│   │   ├── AlertsPage.tsx                 ← light restyle
│   │   ├── CampaignsPage.tsx              ← light restyle
│   │   ├── SettingsPage.tsx               ← light restyle
│   │   └── (strategies/* unchanged)
│   ├── lib/
│   │   ├── api.ts                         ← unchanged (ky client)
│   │   ├── queryClient.ts                 ← unchanged
│   │   └── chart-utils.ts                 ← NEW — range crop, y-axis scaling, date labels
│   └── utils/
│       ├── format.ts                      ← NEW — currency/percent with InvariantCulture equivalent
│       ├── cn.ts                          ← unchanged (tailwind-merge + clsx)
│       └── theme.ts                       ← UPDATE (read/write data-theme)
└── tailwind.config (via @theme in index.css)

infra/cloudflare/worker/
├── src/
│   ├── index.ts                           ← UPDATE (mount new routes)
│   └── routes/
│       ├── performance.ts                 ← NEW
│       ├── drawdowns.ts                   ← NEW
│       ├── monthly-returns.ts             ← NEW
│       ├── risk.ts                        ← NEW (VIX)
│       ├── system-metrics.ts              ← NEW
│       ├── breakdown.ts                   ← NEW (positions breakdown)
│       ├── activity.ts                    ← NEW (recent activity)
│       ├── positions.ts                   ← EXTEND (add campaign join)
│       ├── alerts.ts                      ← EXTEND (add summary endpoint)
│       └── campaigns.ts                   ← NEW (summary endpoint)
```

Legend: **NEW** = create new file · **REWRITE** = replace contents · **EXTEND** = add to existing · **UPDATE** = small edits · **unchanged** = no touch.

---

## 4. Design System Foundation

### 4.1 `src/index.css` — complete rewrite

Replace the current glassmorphism CSS with a port of `colors_and_type.css` from the kit. Key changes:

**Token model:** CSS custom properties on `:root` (dark default) and `:root[data-theme='light']` (override block). Wizard sub-theme continues to override inside `.wizard-root` containers.

**Removed:** `body::before` animated gradient, `body::after` grid overlay, `.card-clean` glassmorphism, `.metric-value` gradient-text, `.positive-glow`/`.negative-glow` text-shadow, `.pulse-dot` keyframes (replaced with kit version).

**Added (from `colors_and_type.css`):**
- Full dark palette (`--bg-1: #0d1117`, `--bg-2: #161b22`, `--bg-3: #21262d`, borders, semantic greens/reds/yellows/blues/purples)
- Full light palette (`[data-theme='light']` block)
- Wizard amber palette (`.wizard-root` block)
- Typography scale (11/12/14/16/18/20/24/30/36)
- Spacing scale (4/8/12/16/24/32/48/64)
- Radius scale (4/6/8/12/pill)
- Shadow scale (sm/md/lg + ring)
- Motion tokens (fast/normal/slow + ease-out)
- Animations (`@keyframes pulseDot`, `spin`, `shimmer`, `fadeIn`, `fadeUp`)
- Scrollbar styling (8px, `--border-1` thumb)
- Semantic classes (`.num`, `.tabular`, `.text-up`, `.text-down`, `.text-muted`, `.overline`)
- Google Fonts import (Inter, DM Sans, Space Mono, JetBrains Mono)

### 4.2 Tailwind 4 `@theme` configuration

Expose the CSS vars as Tailwind utilities so JSX can use `bg-card` / `border-default` / `text-muted` without writing `style={{}}`:

```css
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
  --font-sans: var(--font-sans);
  --font-display: var(--font-display);
  --font-mono: var(--font-mono);
  --radius-card: 8px;
  --radius-pill: 999px;
  /* ... */
}
```

Result: `<div class="bg-surface border border-default rounded-card p-4">` produces a kit-compliant card without inline style. The existing Tailwind semantic aliases (`text-muted`, `text-danger`, `bg-accent`, `border-border`) in current code remain valid; they just rebind to the new palette.

### 4.3 Font loading

Kit imports 4 font families from Google Fonts; our current `index.css` imports only 2. We add Inter (300/400/500/600/700) and Space Mono (400/700), keep DM Sans (400/500/600/700) and JetBrains Mono (400/500/600). Body default shifts to Inter (denser operator console), headings to DM Sans, numbers/tickers to JetBrains Mono.

---

## 5. Component Library

### 5.1 Primitives (`components/ui/`)

Each primitive has a **typed props contract**, small surface area, and styles expressed in Tailwind + CSS vars. All primitives support forwardRef where it matters for composition (Radix compatibility).

| Component | Props (key) | Source |
|---|---|---|
| `Card` | `padding?: 0 \| 16 \| 20 \| 24`, `className`, `children` | kit components.jsx |
| `Badge` | `tone: 'green'\|'red'\|'yellow'\|'blue'\|'purple'\|'muted'`, `pulse?: boolean`, `size?: 'sm'\|'md'` | kit components.jsx |
| `Button` | `variant: 'primary'\|'secondary'\|'ghost'\|'danger'`, `size: 'sm'\|'md'\|'lg'`, `icon?: LucideIcon`, `loading?: boolean`, standard button HTML attrs | kit components.jsx |
| `StatCard` | `label: string`, `value: string`, `delta?: string`, `deltaTone?: 'green'\|'red'\|'yellow'\|'muted'`, `icon?: LucideIcon`, `status?: string \| {tone, label}`, `onClick?` | kit components.jsx |
| `Spinner` | `size?: number`, `color?: string` | kit components.jsx |
| `Skeleton` | `w?: string \| number`, `h?: number`, `radius?: number` | kit components.jsx |
| `FilterOverlay` | `label?: string`, `visible: boolean` | kit components.jsx |
| `SegmentedControl<T>` | `value: T`, `onChange: (v: T) => void`, `options: {value: T, label: string, locked?: boolean}[]`, `size?: 'sm'\|'md'` | distilled from kit (range picker / tabs / unit toggle pattern) |

### 5.2 Layout (`components/layout/`)

- **Sidebar** — 240px, items from kit: Overview / System Health / Positions / Campaigns / IVTS Monitor / Alerts / Logs / Settings. Plus Strategy Wizard collapsible section (kept from current sidebar). Footer: "Paper Trading / IBKR · Connected" with pulsing green dot.
- **Header** — 64px, left = title/subtitle from route, right = OPERATIONAL badge (pulse) + Bell button + Theme toggle (Sun/Moon) + Avatar.
- **Layout** — flex shell, sidebar + main (header + scrollable PageShell).

### 5.3 Dashboard widgets (`components/dashboard/`)

All ported from `PerfWidgets.jsx` / `OverviewPage.jsx` / `components.jsx`:

- **AssetFilter** — 4 chips (All / Systematic / Options / Other), controlled via `assetFilterStore`.
- **SummaryCard** — 3×2 grid, 6 horizons (This Month / YTD / 2Y / 5Y / 10Y / Annualized), % vs € toggle (SegmentedControl).
- **MultiSeriesChart** — 3 overlaid lines (Portfolio solid, S&P 500 dashed, SWDA dotted) with legend chips to toggle each, range SegmentedControl (1W/1M/3M/YTD/1Y/ALL), gradient area fill under Portfolio. Built on **Recharts `ComposedChart` + `Line` + `Area`**.
- **DrawdownsSection** — left: chart (Portfolio green, S&P 500 blue), range control (Max/10Y/5Y/1Y/YTD/6M). Right: "Worst Drawdowns" table (Depth / Start / End / Months).
- **MonthlyPerfSection** — tabbed card; "Monthly Returns" shows a CSS Grid heatmap of Year × Month cells colored by return magnitude (green gradient for positive, red for negative, gray for null). Other tabs (Compounded / Cumulative / Drawdowns / Beta) render "premium — locked" placeholder with lock icon.
- **RiskMetricsCard** — 8-row list: Portfolio Delta/Theta/Vega, **VIX Index**, **VIX1D**, IV Rank SPY, Buying Power, Margin Used. Tone per row.
- **AlertsMiniCard** — "Alerts · last 24h" count with severity badges (N critical / N warning / N info), clickable → navigates to /alerts.
- **SystemPerfMini** — 2×2 grid: CPU/RAM/Network as mini histogram of last 20 samples + Disk Free as gradient progress bar. Header shows "as of YYYY-MM-DD HH:MM:SS UTC" + LIVE badge.
- **RecentActivity** — list of events (icon + title + subtitle + relative time), "View all" button → /logs.
- **ActivePositionsTable** — 7 cols: Symbol / **Campaign** / Qty / Mark / P&L / % / DTE. New Campaign col shows campaign name in accent blue or "—" if none.

### 5.4 Positions widgets (`components/positions/`)

- **PositionsBreakdown** — 2 Cards side by side: "Exposure by Strategy" (Iron Condor, Put/Call Spread, Long Call, Short Strangle) + "Exposure by Asset" (Options, Systematic, Other). Each: Donut chart (inline SVG, 180px, 28px thickness, center = total in €) + legend with %+€ values.
- **PositionsKpiStrip** — 5 StatCards: Open P&L / Realized 7d / Open Positions / Avg DTE / Buying Power.
- **PositionsFilterBar** — Search input + Status chips (All/Open/Closed/Pending) + Type select + Refresh button + Table/Cards view toggle.
- **PositionsTable** — 10 cols: Symbol / Status / Qty / Cost / Mark / P&L / % / Δ / Θ / DTE (as kit).
- **PositionCard** — grid-view card variant.

---

## 6. Page Layouts

### 6.1 `pages/HomePage.tsx` (Overview)

```
┌─────────────────────────────────────────────────────────────┐
│ AssetFilter chips                   auto-refresh · 30s (⟳) │
├─────────────────────────────────────────────────────────────┤
│ StatCard × 4: Account Value · Open P&L · Active Campaigns   │
│                                              · AlertsMini   │
├─────────────────────────────────────┬───────────────────────┤
│ Account Performance (MultiSeries)   │ RiskMetricsCard       │
│  [1W 1M 3M YTD 1Y ALL]              │ (VIX, VIX1D, etc)     │
│  [Portfolio S&P500 SWDA toggles]    │                       │
│                                     ├───────────────────────┤
│                                     │ SystemPerfMini        │
├─────────────────────────────────────┴───────────────────────┤
│ SummaryCard (6 horizons, % / € toggle)                      │
├─────────────────────────────────────────────────────────────┤
│ DrawdownsSection (chart + Worst Drawdowns table)            │
├─────────────────────────────────────────────────────────────┤
│ MonthlyPerfSection (tabs + heatmap grid)                    │
├─────────────────────────────────────────────────────────────┤
│ Active Positions (table with Campaign column)               │
├─────────────────────────────────────────────────────────────┤
│ Recent Activity (list)                                      │
└─────────────────────────────────────────────────────────────┘
```

Filter behavior: changing the `AssetFilter` triggers a 420ms overlay on every asset-scoped widget (KPI row, chart, Summary, Drawdowns, Monthly). Range changes on the chart re-fetch only the series endpoint.

### 6.2 `pages/PositionsPage.tsx`

```
┌─────────────────────────────────────────────────────────────┐
│ PositionsBreakdown (Donut × 2: Strategy + Asset)            │
├─────────────────────────────────────────────────────────────┤
│ PositionsKpiStrip (5 StatCards)                             │
├─────────────────────────────────────────────────────────────┤
│ PositionsFilterBar                                          │
├─────────────────────────────────────────────────────────────┤
│ PositionsTable or PositionsGrid                             │
└─────────────────────────────────────────────────────────────┘
```

### 6.3 Other pages

- **AlertsPage / CampaignsPage / SettingsPage** — restyle existing pages to the kit's visual language (Card, Badge, Button primitives). No data model changes.
- **Strategy pages** — unchanged, only CSS var harmonization.

---

## 7. Backend API Extensions (Cloudflare Worker)

All endpoints are auth'd via the existing `X-Api-Key` header + rate limit middleware. Response shape is JSON with a top-level object (for extensibility). Mounted in `src/index.ts` using Hono's `app.route()`.

### 7.1 New endpoints

| Method | Path | Purpose | Response shape |
|---|---|---|---|
| GET | `/api/performance/summary?asset={all\|systematic\|options\|other}` | 6-horizon returns + base notional | `{ asset, m, ytd, y2, y5, y10, ann, base }` |
| GET | `/api/performance/series?asset={...}&range={1W\|1M\|3M\|YTD\|1Y\|ALL}` | Overlaid portfolio + S&P 500 + SWDA series | `{ asset, range, portfolio: number[], sp500: number[], swda: number[], startDate: ISO, endDate: ISO }` |
| GET | `/api/drawdowns?asset={...}&range={Max\|10Y\|5Y\|1Y\|YTD\|6M}` | Drawdown series + worst N drawdowns | `{ asset, range, portfolioSeries: number[], sp500Series: number[], worst: WorstDrawdown[] }` |
| GET | `/api/monthly-returns?asset={...}` | Full year×month matrix + yearly totals | `{ asset, years: { [year: number]: number[12] \| null[] }, totals: { [year: number]: number } }` |
| GET | `/api/risk/metrics` | VIX, VIX1D, portfolio greeks, IV rank, margin | `{ vix: number, vix1d: number, delta: number, theta: number, vega: number, ivRankSpy: number, buyingPower: number, marginUsedPct: number }` |
| GET | `/api/system/metrics` | Latest sampled CPU/RAM/Network + HDD total/free + asOf timestamp | `{ cpu: number[], ram: number[], network: number[], diskUsedPct: number, diskFreeGb: number, diskTotalGb: number, asOf: ISO }` |
| GET | `/api/positions/breakdown` | Aggregates of open positions in EUR | `{ byStrategy: ExposureSegment[], byAsset: ExposureSegment[] }` |
| GET | `/api/activity/recent?limit=8` | Recent orders/events/system activity | `{ events: ActivityEvent[] }` |
| GET | `/api/alerts/summary-24h` | Alert counts last 24h by severity | `{ total: number, critical: number, warning: number, info: number }` |
| GET | `/api/campaigns/summary` | Running/paused/draft counts | `{ active: number, paused: number, draft: number, detail: string }` |

### 7.2 Modified endpoints

- `GET /api/positions/active` — add `campaign: string | null` field to each Position row (JOIN to campaigns table on position's `campaign_id`).

### 7.3 Data sources

- **VIX / VIX1D** — poll IBKR contract market data (the IBKR market data subscription supports CBOE VIX index; VIX1D is a separate contract). Cache 30s in Worker KV or D1. If market-data subscription missing, return `null` and hide the two rows in the frontend.
- **Performance series** — computed from `positions_history` or a future `account_equity_daily` D1 table. Store daily closes; compute normalized-to-100 series on the fly from `startDate` to `endDate`.
- **S&P 500 / SWDA benchmarks** — hit IBKR (SPX / IWDA contract) or a free fallback (Stooq CSV / Yahoo v8 chart). Cache daily closes in D1 table `benchmarks_daily`.
- **Drawdowns** — derived from equity series: rolling peak, then (current − peak) / peak as %; find worst-N by depth with start/end dates.
- **Monthly returns** — compounded monthly returns from daily equity, with per-year totals from compounding.
- **System metrics** — already comes from `services_heartbeats` table (MachineMetrics collector); extend to include CPU/RAM/Network history window + disk. The `.NET MachineMetricsCollector` will need extension for disk.
- **Recent activity** — merged view from `orders`, `alerts`, `campaigns_events`, `heartbeats`; ordered by ts desc, limit 8.

### 7.4 Caching strategy

- Series endpoints: cache-control `max-age=30` (30s). VIX/system: `max-age=15`.
- React Query on client side: `staleTime: 30_000`, `refetchInterval: 30_000` to match.

---

## 8. Data Contracts (TypeScript types)

Example signatures (full types in `src/types/*.ts`):

```ts
// types/performance.ts
export interface SummaryData {
  asset: AssetBucket
  m: number; ytd: number; y2: number; y5: number; y10: number; ann: number
  base: number  // notional in EUR for % → EUR conversion
}

export type AssetBucket = 'all' | 'systematic' | 'options' | 'other'
export type PerfRange = '1W' | '1M' | '3M' | 'YTD' | '1Y' | 'ALL'

export interface PerfSeries {
  asset: AssetBucket
  range: PerfRange
  portfolio: number[]
  sp500: number[]
  swda: number[]
  startDate: string   // ISO
  endDate: string
}

// types/drawdown.ts
export interface WorstDrawdown {
  depthPct: number    // negative
  start: string       // "Nov 2023"
  end: string
  months: number
}

// types/risk.ts
export interface RiskMetrics {
  vix: number | null
  vix1d: number | null
  delta: number; theta: number; vega: number
  ivRankSpy: number | null
  buyingPower: number
  marginUsedPct: number
}

// types/system.ts
export interface SystemMetricsSample {
  cpu: number[]       // last N samples, 0-100
  ram: number[]
  network: number[]
  diskUsedPct: number
  diskFreeGb: number
  diskTotalGb: number
  asOf: string        // ISO UTC
}

// types/activity.ts
export type ActivityIcon =
  | 'check-circle-2' | 'alert-triangle' | 'play' | 'x-circle'
  | 'repeat' | 'trending-up' | 'refresh-cw' | 'file-text'
export type ActivityTone = 'green' | 'red' | 'yellow' | 'blue' | 'purple' | 'muted'

export interface ActivityEvent {
  id: string
  icon: ActivityIcon
  tone: ActivityTone
  title: string          // "Order filled"
  subtitle: string       // "SPY 450C × 3 @ $12.40"
  timestamp: string      // ISO — frontend converts via formatDistanceToNow
}

// types/breakdown.ts
export interface ExposureSegment {
  label: string          // "Iron Condor", "Options", etc.
  value: number          // EUR
  color: string          // hex from kit palette
}

// types/position.ts — EXTEND
export interface Position {
  // ...existing fields...
  campaign: string | null
  campaignId: string | null
}
```

---

## 9. Data Layer / Hooks Strategy

Each new hook follows the same pattern:

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
```

- **Single source of truth** — no mock data in frontend. Worker returns mock/real seamlessly based on DB presence; frontend doesn't care.
- **Loading states** — every widget renders a `Skeleton` (shimmer) while data is `undefined`; error state renders a tiny retry Card.
- **Suspense boundaries** — optional; default to in-widget skeletons for progressive rendering.
- **Asset filter** — `useAssetFilterStore().asset` is the reactive key every asset-scoped hook subscribes to. Changing the store invalidates all keyed queries automatically.

---

## 10. Light / Dark Theme

- **Toggle in Header:** Sun / Moon icon button (lucide). Click cycles `dark ↔ light`; `system` mode is NOT in v1 (YAGNI).
- **State:** `themeStore` (Zustand) holds `'dark' | 'light'`, writes to `localStorage.ts-theme`, also sets `document.documentElement.dataset.theme`.
- **Init:** `main.tsx` reads `localStorage.ts-theme` before first paint to avoid FOUC.
- **Recharts:** chart colors reference CSS vars (`stroke: 'var(--blue)'`, etc.), so chart hues flip automatically.
- **Logo:** kit's logo is a purple lightning glyph that reads on both themes — no per-theme switch needed.

---

## 11. Strategy Wizard Harmonization

The current Wizard uses its own `src/styles/wizard.css` with an amber palette. The kit defines the **same amber palette** as `.wizard-root` CSS var overrides in `colors_and_type.css`. Plan:

1. Keep all wizard JSX/logic/tests untouched.
2. Delete or gut `src/styles/wizard.css`; move its specifics into `.wizard-root` block of new `index.css`.
3. Wrap the Wizard root container in `<div className="wizard-root">` so the overrides apply.
4. Regression-test the wizard pages to confirm amber accents still appear correctly.

Expected diff: ~30 lines removed from `wizard.css`, same colors now owned centrally.

---

## 12. Migration Strategy

This is a breaking rewrite of the UI layer. To keep the tree in a buildable state throughout, execute in **5 serialized phases**:

1. **Phase 1 — Design system foundation (no UI changes visible)**
   - Rewrite `src/index.css` with kit tokens (dark palette only at first)
   - Add Tailwind `@theme` mapping
   - Rewrite `components/ui/Card.tsx` + `Badge.tsx` + `Button.tsx`
   - Wizard gets `.wizard-root` wrapper
   - **Checkpoint:** dashboard still runs, but all pages now look "flat" (no glass). Tests still pass.

2. **Phase 2 — Primitives + layout**
   - Build `StatCard`, `Spinner`, `Skeleton`, `FilterOverlay`, `SegmentedControl`
   - Rewrite `Sidebar`, `Header`, `Layout`
   - Theme toggle + `themeStore` + light palette
   - **Checkpoint:** all pages re-chromed; content still the old content.

3. **Phase 3 — Worker API extensions (parallel to phase 4)**
   - New routes: performance, drawdowns, monthly, risk, system, breakdown, activity
   - `positions/active` extended with campaign
   - All endpoints tested with vitest (existing worker test pattern)
   - **Checkpoint:** `curl` each endpoint returns valid JSON shape.

4. **Phase 4 — Dashboard widgets + new hooks**
   - `assetFilterStore`, `useAssetFilter*` hooks (10 new hooks)
   - Build `components/dashboard/*` (10 widgets)
   - Rewrite `HomePage.tsx` to kit Overview layout
   - **Checkpoint:** Overview page matches kit pixel-close (modulo real data).

5. **Phase 5 — Positions + cleanup**
   - Build `components/positions/PositionsBreakdown`, `PositionsKpiStrip`, `PositionsFilterBar`
   - Rewrite `PositionsPage.tsx`, `PositionsTable`, `PositionCard`
   - Light restyle of Alerts / Campaigns / Settings pages to kit vocabulary
   - Delete dead code: old `wizard.css`, unused classes, old animated-bg CSS
   - **Checkpoint:** full acceptance criteria (§15) met.

---

## 13. Testing Strategy

- **Unit (dashboard):** new primitives (Card/Badge/Button/StatCard/Spinner/SegmentedControl) — snapshot + basic interaction tests, **vitest + `npm test` (NOT `bun test`)** per project CLAUDE.md rule.
- **Unit (worker):** new routes — vitest integration tests mirroring the existing `bot-*.test.ts` pattern.
- **Component (dashboard):** MultiSeriesChart/DrawdownsSection/MonthlyPerfSection — render with mock React Query provider, assert chart SVG nodes + legend toggles + range segment.
- **E2E (optional v2):** Playwright smoke — open Overview, click each range, confirm chart re-renders. Out of scope for v1 unless you want it.
- **Visual regression:** not in scope. If wanted: Chromatic or Playwright visual snapshots at the end of Phase 5.

---

## 14. Out of Scope / Future Work

- Bulk refactor of Strategy Wizard visuals (kit explicitly excludes it).
- Server-sent events / WebSocket streaming (current approach: HTTP polling at 30s via React Query `refetchInterval`). Logs stream can be upgraded to WS in a later cycle — log tail works fine with polling for now.
- Locked "premium" tabs of Monthly Performance (Compounded / Cumulative / Drawdowns / Beta) — stubbed with lock icon, populate in a future ticket.
- Real IV Rank SPY computation — requires historical IV data; return `null` for now and hide row.
- Real-time IBKR connection status pulse in header / sidebar — already exists in kit; wire to existing heartbeats endpoint.
- Mobile responsiveness beyond tablet (kit is desktop-first — 1366px baseline).

---

## 15. Acceptance Criteria

**Overview page**
- [ ] Asset filter chips (All / Systematic / Options / Other) control every asset-scoped widget below.
- [ ] 4 KPIs: Account Value · Open P&L · Active Campaigns · **Alerts last 24h** (with critical/warning/info badges).
- [ ] Account Performance chart renders 3 series (Portfolio solid, S&P 500 dashed, SWDA dotted) with toggles.
- [ ] Range chips include **ALL** in addition to 1W/1M/3M/YTD/1Y.
- [ ] Risk Metrics card includes **VIX Index** and **VIX1D** rows.
- [ ] **System Performance mini** card shows CPU/RAM/Network histograms + Disk Free + asOf datetime.
- [ ] **Performance Summary** card with 6 horizons and **% / €** toggle (values example: +14.30%, +15.04%, +49.88%, +100.13%, +98.59%, +13.07%).
- [ ] **Drawdowns** section: chart (Portfolio vs S&P 500, color-diff) + Worst Drawdowns table.
- [ ] **Monthly Performance** section: tabs (Monthly Returns visible; Compounded/Cumulative/Drawdowns/Beta locked) + heatmap grid (year rows × month cols + Total col).
- [ ] Active Positions table has a **Campaign** column between Symbol and Qty.
- [ ] **Recent Activity** list appears below Active Positions.
- [ ] All widgets show a 420ms filter overlay when the asset filter changes.

**Positions page**
- [ ] 2 Donut charts at top: **Exposure by Strategy** + **Exposure by Asset**.
- [ ] 5 KPI StatCards strip.
- [ ] Filter bar: search + status chips + type select + Table/Card toggle.

**Design system**
- [ ] `src/index.css` contains no glassmorphism (`backdrop-filter`, gradient bg, text-gradient, `::before` animated bg).
- [ ] Dark and Light themes both render correctly via `data-theme` toggle in header.
- [ ] Strategy Wizard still shows amber sub-theme and all wizard tests still pass.

**Backend**
- [ ] All new Worker endpoints return valid JSON, pass CORS, respect rate limit.
- [ ] `GET /api/positions/active` now includes `campaign` field.
- [ ] No breaking changes to existing endpoints.

**Quality**
- [ ] `dotnet test` unchanged and passing (no backend service changes).
- [ ] `cd dashboard && npm test` passes; `cd dashboard && npm run typecheck` passes; `npm run build` produces bundle.
- [ ] `cd infra/cloudflare/worker && bun test` passes.
- [ ] No TODO-bloccante in modified code.
- [ ] `knowledge/errors-registry.md` updated with any discovery; `knowledge/lessons-learned.md` gains at least one entry.

---

## 16. Open Questions

None blocking — ready for user review. Any follow-up clarifications (e.g., "show `null` VIX as em-dash vs hide row" exact behavior) resolved during Phase 4 implementation.
