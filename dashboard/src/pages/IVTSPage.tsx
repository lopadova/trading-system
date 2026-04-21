// IVTSPage — Implied Volatility Term Structure monitor.
// Ported from docs/design-kit/extracted/.../IVTSPage.jsx (inline-styles → TSX +
// Tailwind). SVG chart is kept verbatim since it's small; everything else is
// Tailwind with CSS-variable tokens. Mock data mirrors the kit.

import { useState } from 'react'
import { motion } from 'motion/react'
import { Activity, Timer, List, AlertCircle } from 'lucide-react'
import { Card } from '../components/ui/Card'
import { StatCard } from '../components/ui/StatCard'
import { Badge, type BadgeTone } from '../components/ui/Badge'
import { FilterOverlay } from '../components/ui/FilterOverlay'

// -----------------------------------------------------------------------------
// Data model
// -----------------------------------------------------------------------------

interface TermStructure {
  today: number[]
  week: number[]
  month: number[]
}

type Symbol = 'SPY' | 'QQQ' | 'IWM'

// Synthetic, deterministic term-structure curves — match the design kit
const CURVES: Record<Symbol, TermStructure> = {
  SPY: {
    today: [22.1, 21.2, 20.4, 19.8, 19.5, 19.3, 19.2, 19.1, 19.0],
    week: [24.8, 23.5, 22.6, 21.9, 21.4, 21.0, 20.8, 20.6, 20.4],
    month: [28.1, 26.4, 25.0, 24.2, 23.5, 22.9, 22.5, 22.2, 21.9],
  },
  QQQ: {
    today: [29.2, 27.8, 26.4, 25.1, 24.2, 23.5, 23.0, 22.6, 22.2],
    week: [32.5, 30.8, 29.1, 27.5, 26.3, 25.4, 24.8, 24.3, 23.9],
    month: [38.1, 35.2, 32.4, 30.1, 28.5, 27.3, 26.4, 25.7, 25.1],
  },
  IWM: {
    today: [34.8, 32.4, 30.5, 29.0, 27.8, 26.9, 26.3, 25.8, 25.4],
    week: [38.2, 35.3, 32.9, 31.0, 29.5, 28.4, 27.5, 26.8, 26.3],
    month: [42.5, 38.9, 36.0, 33.7, 31.9, 30.4, 29.3, 28.4, 27.8],
  },
}

const DTE = [1, 7, 14, 21, 30, 45, 60, 90, 120] as const

interface Underlying {
  sym: string
  spot: string
  iv30: number
  rank: number
  change: number
  sig: 'contango' | 'flat' | 'backwardation'
}

const UNDERLYINGS: Underlying[] = [
  { sym: 'SPY', spot: '456.12', iv30: 19.5, rank: 34, change: -0.8, sig: 'contango' },
  { sym: 'QQQ', spot: '398.40', iv30: 24.2, rank: 58, change: 1.3, sig: 'contango' },
  { sym: 'IWM', spot: '203.85', iv30: 27.8, rank: 72, change: 2.1, sig: 'contango' },
  { sym: 'DIA', spot: '362.21', iv30: 16.4, rank: 22, change: -0.3, sig: 'flat' },
  { sym: 'XLE', spot: '89.14', iv30: 31.5, rank: 84, change: 3.8, sig: 'backwardation' },
  { sym: 'XSP', spot: '456.15', iv30: 19.4, rank: 33, change: -0.7, sig: 'contango' },
]

// Staggered fade-in-up (matches HomePage)
function stagger(i: number) {
  return {
    initial: { opacity: 0, y: 8 },
    animate: { opacity: 1, y: 0 },
    transition: {
      duration: 0.28,
      delay: i * 0.04,
      ease: [0.4, 0, 0.2, 1] as const,
    },
  }
}

// -----------------------------------------------------------------------------
// IVTSChart — 3-curve IV term structure plot (today / 1w / 1m ago)
// -----------------------------------------------------------------------------

interface IVTSChartProps {
  symbol: Symbol
  spot: string
}

function IVTSChart({ symbol, spot }: IVTSChartProps) {
  // SVG geometry constants taken from the kit — preserve layout identically
  const curve = CURVES[symbol]
  const all = [...curve.today, ...curve.week, ...curve.month]
  const maxY = Math.ceil(Math.max(...all) / 5) * 5
  const minY = Math.floor(Math.min(...all) / 5) * 5
  const W = 640
  const H = 260
  const PAD_L = 44
  const PAD_R = 14
  const PAD_T = 14
  const PAD_B = 28
  const plot = { w: W - PAD_L - PAD_R, h: H - PAD_T - PAD_B }

  const x = (i: number): number => PAD_L + (i / (DTE.length - 1)) * plot.w
  const y = (v: number): number =>
    PAD_T + plot.h - ((v - minY) / (maxY - minY)) * plot.h

  // Build the "M x,y L x,y …" path for a series
  const line = (arr: number[]): string =>
    arr
      .map((v, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(1)},${y(v).toFixed(1)}`)
      .join(' ')

  // Closed-path variant used for the gradient-fill area under the Today curve
  const area = (arr: number[]): string =>
    `${line(arr)} L${x(arr.length - 1).toFixed(1)},${PAD_T + plot.h} L${PAD_L},${PAD_T + plot.h} Z`

  // Contango/flat/backwardation regime badge is derived from the slope of the
  // short-end → long-end of the Today curve
  const todayStart = curve.today[0] ?? 0
  const todayEnd = curve.today[curve.today.length - 1] ?? 0
  const slope = todayEnd - todayStart
  const regime: { lbl: string; tone: BadgeTone } =
    slope > 2
      ? { lbl: 'CONTANGO', tone: 'blue' }
      : slope < -2
        ? { lbl: 'BACKWARDATION', tone: 'red' }
        : { lbl: 'FLAT', tone: 'muted' }

  const yTicks = 5
  const gridLines = Array.from({ length: yTicks + 1 }, (_, i) => {
    const v = minY + (i / yTicks) * (maxY - minY)
    return { v, yy: y(v) }
  })

  return (
    <div>
      <div className="flex justify-between items-end mb-2.5">
        <div className="flex gap-4 items-center">
          <div>
            <div className="text-[11px] text-muted uppercase tracking-wider">
              spot
            </div>
            <div className="font-mono text-[20px] font-semibold text-[var(--fg-1)] tabular-nums">
              ${spot}
            </div>
          </div>
          <Badge tone={regime.tone}>{regime.lbl}</Badge>
        </div>
        <div className="flex gap-3.5 text-[11.5px] text-muted">
          <span className="flex items-center gap-1.5">
            <span className="w-3.5 h-0.5 bg-[var(--blue)]" />
            Today
          </span>
          <span className="flex items-center gap-1.5">
            <span className="w-3.5 border-t border-dashed border-[var(--purple)]" />
            1 week ago
          </span>
          <span className="flex items-center gap-1.5">
            <span className="w-3.5 border-t border-dotted border-subtle" />
            1 month ago
          </span>
        </div>
      </div>
      <svg viewBox={`0 0 ${W} ${H}`} className="w-full block">
        <defs>
          <linearGradient id="ivtsGrad" x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" stopColor="#2f81f7" stopOpacity="0.3" />
            <stop offset="100%" stopColor="#2f81f7" stopOpacity="0" />
          </linearGradient>
        </defs>
        {/* horizontal grid lines + y-axis tick labels */}
        {gridLines.map((g, i) => (
          <g key={i}>
            <line
              x1={PAD_L}
              x2={W - PAD_R}
              y1={g.yy}
              y2={g.yy}
              stroke="var(--border-2)"
              strokeWidth="1"
            />
            <text
              x={PAD_L - 8}
              y={g.yy + 3}
              textAnchor="end"
              fill="var(--fg-2)"
              fontSize="10"
              fontFamily="var(--font-mono)"
            >
              {g.v.toFixed(0)}%
            </text>
          </g>
        ))}
        {/* x-axis DTE labels */}
        {DTE.map((d, i) => (
          <text
            key={d}
            x={x(i)}
            y={H - 10}
            textAnchor="middle"
            fill="var(--fg-2)"
            fontSize="10"
            fontFamily="var(--font-mono)"
          >
            {d}d
          </text>
        ))}
        {/* month-ago dotted curve */}
        <path
          d={line(curve.month)}
          fill="none"
          stroke="var(--fg-2)"
          strokeWidth="1.5"
          strokeDasharray="1 3"
          opacity=".8"
        />
        {/* week-ago dashed curve */}
        <path
          d={line(curve.week)}
          fill="none"
          stroke="var(--purple)"
          strokeWidth="1.5"
          strokeDasharray="4 3"
        />
        {/* today filled area + line + markers */}
        <path d={area(curve.today)} fill="url(#ivtsGrad)" />
        <path
          d={line(curve.today)}
          fill="none"
          stroke="var(--blue)"
          strokeWidth="2"
        />
        {curve.today.map((v, i) => (
          <circle
            key={i}
            cx={x(i)}
            cy={y(v)}
            r="2.5"
            fill="var(--bg-1)"
            stroke="var(--blue)"
            strokeWidth="1.5"
          />
        ))}
      </svg>
    </div>
  )
}

// -----------------------------------------------------------------------------
// IVTSPage — main composition
// -----------------------------------------------------------------------------

type ChartTab = 'term' | 'skew'

export function IVTSPage() {
  const [selected, setSelected] = useState<Symbol>('SPY')
  const [loading, setLoading] = useState(false)
  const [tab, setTab] = useState<ChartTab>('term')

  // Guarded because the synth chart only has data for SPY/QQQ/IWM. Symbols
  // outside that set still appear in the list so users can see the roster.
  const pick = (sym: string) => {
    if (sym === selected) return
    if (!(sym in CURVES)) return
    setLoading(true)
    setTimeout(() => {
      setSelected(sym as Symbol)
      setLoading(false)
    }, 420)
  }

  const pickTab = (t: ChartTab) => {
    if (t === tab) return
    setLoading(true)
    setTimeout(() => {
      setTab(t)
      setLoading(false)
    }, 360)
  }

  // UNDERLYINGS contains SPY, guaranteed — but be defensive.
  const current =
    UNDERLYINGS.find(u => u.sym === selected) ?? UNDERLYINGS[0]!

  return (
    <div className="p-8 flex flex-col gap-4">
      {/* Row 0 — KPI strip */}
      <motion.div
        {...stagger(0)}
        className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4"
      >
        <StatCard
          label="VIX Index"
          value="15.84"
          icon={Activity}
          delta="↓ 0.42 (−2.58%)"
          deltaTone="green"
        />
        <StatCard
          label="VIX1D · front"
          value="11.20"
          icon={Timer}
          delta="↑ 0.08 (+0.72%)"
          deltaTone="red"
        />
        <StatCard
          label="Symbols Monitored"
          value="6"
          icon={List}
          delta="4 contango · 1 flat · 1 backwardation"
          deltaTone="muted"
        />
        <StatCard
          label="IV Rank Alerts"
          value="2"
          icon={AlertCircle}
          delta="XLE · IWM above 70%"
          deltaTone="yellow"
        />
      </motion.div>

      {/* Row 1 — main chart + symbol list */}
      <motion.div
        {...stagger(1)}
        className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-4"
      >
        <Card className="relative min-h-[380px]">
          <FilterOverlay
            visible={loading}
            label={`Loading ${selected} · ${tab === 'term' ? 'term structure' : 'skew'}…`}
          />
          <div className="flex justify-between items-start mb-2.5">
            <div>
              <h3 className="m-0 text-[16px] font-semibold flex items-center gap-2.5">
                <span className="font-mono">{selected}</span>
                <span className="text-[12px] text-muted font-normal">
                  · {tab === 'term' ? 'Term Structure' : 'Volatility Skew'}
                </span>
              </h3>
              <div className="text-[12px] text-muted mt-0.5">
                Implied vol across {tab === 'term' ? 'expirations' : 'strikes'} ·
                delayed 15 min
              </div>
            </div>
            <div className="flex gap-1 p-[3px] bg-[var(--bg-1)] border border-border rounded-md">
              {(['term', 'skew'] as const).map(k => {
                const label = k === 'term' ? 'Term' : 'Skew'
                const on = tab === k
                return (
                  <button
                    key={k}
                    type="button"
                    onClick={() => pickTab(k)}
                    className={`px-3 py-1 rounded text-[12px] border-none ${
                      on
                        ? 'bg-[var(--bg-3)] text-[var(--fg-1)] font-medium'
                        : 'bg-transparent text-muted'
                    }`}
                  >
                    {label}
                  </button>
                )
              })}
            </div>
          </div>
          {/* Only the term-structure chart has real data — the skew tab is a
              premium/coming-soon placeholder. */}
          {tab === 'term' ? (
            <IVTSChart symbol={selected} spot={current.spot} />
          ) : (
            <div className="py-16 text-center text-muted text-[12.5px]">
              Volatility skew view — coming soon
            </div>
          )}
        </Card>

        <Card padding={0} className="relative min-h-[380px]">
          <FilterOverlay visible={loading} label={`Loading ${selected}…`} />
          <div className="px-4 py-3.5 border-b border-border">
            <h3 className="m-0 text-[14px] font-semibold">Underlyings</h3>
            <div className="text-[11.5px] text-muted mt-0.5">
              Click to load term structure
            </div>
          </div>
          <div>
            {UNDERLYINGS.map(u => {
              const on = u.sym === selected
              const up = u.change >= 0
              // Underlyings not in CURVES are non-interactive (show as disabled)
              const clickable = u.sym in CURVES
              return (
                <div
                  key={u.sym}
                  onClick={() => clickable && pick(u.sym)}
                  className={`px-4 py-[11px] border-b border-border flex justify-between items-center transition-colors ${
                    clickable ? 'cursor-pointer' : 'cursor-not-allowed opacity-60'
                  } ${
                    on
                      ? 'bg-[var(--tint-blue)] border-l-2 border-l-[var(--blue)]'
                      : 'border-l-2 border-l-transparent hover:bg-[color:var(--fg-2)]/10'
                  }`}
                >
                  <div>
                    <div
                      className={`font-mono font-semibold text-[13px] ${
                        on ? 'text-[var(--blue)]' : 'text-[var(--fg-1)]'
                      }`}
                    >
                      {u.sym}
                    </div>
                    <div className="text-[11px] text-muted font-mono mt-0.5">
                      ${u.spot} · IV30 {u.iv30}%
                    </div>
                  </div>
                  <div className="text-right">
                    <div
                      className={`font-mono text-[12px] tabular-nums font-medium ${
                        up ? 'text-up' : 'text-down'
                      }`}
                    >
                      {up ? '+' : ''}
                      {u.change.toFixed(1)}%
                    </div>
                    <div className="mt-[3px] text-[10px] text-muted flex items-center gap-1 justify-end">
                      rank
                      <span
                        className="px-1.5 rounded-sm font-semibold font-mono"
                        style={{
                          background:
                            u.rank >= 70
                              ? 'var(--tint-yellow)'
                              : u.rank >= 40
                                ? 'var(--tint-blue)'
                                : 'var(--tint-muted)',
                          color:
                            u.rank >= 70
                              ? 'var(--yellow)'
                              : u.rank >= 40
                                ? 'var(--blue)'
                                : 'var(--fg-2)',
                        }}
                      >
                        {u.rank}
                      </span>
                    </div>
                  </div>
                </div>
              )
            })}
          </div>
        </Card>
      </motion.div>
    </div>
  )
}
