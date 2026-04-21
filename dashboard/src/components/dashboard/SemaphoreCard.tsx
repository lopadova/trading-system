// SemaphoreCard — Options Trading Semaphore composite indicator.
//
// Top of the card: a 270° speedometer gauge with a smooth green→yellow→orange→red
// gradient arc, hairline tick marks, and a needle pointing at the current risk
// score (0 = safe, 100 = dangerous). Below the gauge: a dense operator-console
// readout with market regime, SPX / VIX live quotes, and a row per sub-indicator
// with its own tone chip. Aesthetic matches the rest of the dashboard kit
// (Bloomberg × GitHub dark).

import { motion } from 'motion/react'
import { TrendingUp, TrendingDown, Clock } from 'lucide-react'
import { Card } from '../ui/Card'
import { Badge, type BadgeTone } from '../ui/Badge'
import { Skeleton } from '../ui/Skeleton'
import { useSemaphore } from '../../hooks/useSemaphore'
import { formatPercent } from '../../utils/format'
import type {
  SemaphoreData,
  SemaphoreIndicator,
  SemaphoreStatus,
} from '../../types/risk'

// -----------------------------------------------------------------------------
// Gauge geometry
//
// The gauge is a semicircular arc that sweeps 270° around a centered origin.
// We leave the bottom 90° open (from 135° to 405° == 45°). Score 0 maps to the
// LEFT end of the arc (angle 135°) and 100 maps to the RIGHT end (angle 45°),
// sweeping clockwise through the top. All angles are measured clockwise from
// the positive X-axis (3 o'clock) so they match SVG conventions once we flip
// the Y sign at render time.
// -----------------------------------------------------------------------------

// SVG coordinate system — 240 x 160 viewBox with origin at center of gauge.
const GAUGE_SIZE = 240
const GAUGE_CX = GAUGE_SIZE / 2
const GAUGE_CY = 130 // nudge down so the label area below has room
const GAUGE_R_OUTER = 98
const GAUGE_R_INNER = 80
const GAUGE_R_TICKS_OUTER = 102
const GAUGE_R_TICKS_INNER = 92
const GAUGE_R_NEEDLE = 86

// Start / end angles in degrees (SVG convention: 0° = 3 o'clock, clockwise).
// We want 0-score at bottom-left (angle 135°) and 100-score at bottom-right
// (angle 45°), sweeping clockwise through the top through 270° of arc.
const START_ANGLE_DEG = 135
const SWEEP_DEG = 270

function degToRad(deg: number): number {
  return (deg * Math.PI) / 180
}

function polar(radius: number, angleDeg: number): { x: number; y: number } {
  const rad = degToRad(angleDeg)
  return {
    x: GAUGE_CX + radius * Math.cos(rad),
    y: GAUGE_CY + radius * Math.sin(rad),
  }
}

// Map a 0-100 score to an angle on the gauge.
function scoreToAngle(score: number): number {
  const clamped = Math.max(0, Math.min(100, score))
  return START_ANGLE_DEG + (clamped / 100) * SWEEP_DEG
}

// Describe an SVG arc path from startAngle to endAngle at the given radius.
function arcPath(radius: number, startAngleDeg: number, endAngleDeg: number): string {
  const start = polar(radius, startAngleDeg)
  const end = polar(radius, endAngleDeg)
  const largeArc = endAngleDeg - startAngleDeg > 180 ? 1 : 0
  return `M ${start.x} ${start.y} A ${radius} ${radius} 0 ${largeArc} 1 ${end.x} ${end.y}`
}

// Describe a thick band (arc segment) between two angles as a closed path.
function arcBandPath(
  rOuter: number,
  rInner: number,
  startAngleDeg: number,
  endAngleDeg: number
): string {
  const outerStart = polar(rOuter, startAngleDeg)
  const outerEnd = polar(rOuter, endAngleDeg)
  const innerStart = polar(rInner, endAngleDeg)
  const innerEnd = polar(rInner, startAngleDeg)
  const largeArc = endAngleDeg - startAngleDeg > 180 ? 1 : 0
  return [
    `M ${outerStart.x} ${outerStart.y}`,
    `A ${rOuter} ${rOuter} 0 ${largeArc} 1 ${outerEnd.x} ${outerEnd.y}`,
    `L ${innerStart.x} ${innerStart.y}`,
    `A ${rInner} ${rInner} 0 ${largeArc} 0 ${innerEnd.x} ${innerEnd.y}`,
    'Z',
  ].join(' ')
}

// -----------------------------------------------------------------------------
// Status helpers
// -----------------------------------------------------------------------------

function toneFromStatus(s: SemaphoreStatus): BadgeTone {
  // Our kit's 'yellow' tone is used for "orange" (amber caution).
  if (s === 'orange') return 'yellow'
  if (s === 'red') return 'red'
  return 'green'
}

function statusLabel(s: SemaphoreStatus): string {
  if (s === 'green') return 'OPERATIVE'
  if (s === 'orange') return 'CAUTION'
  return 'HALT'
}

function colorVarForStatus(s: SemaphoreStatus): string {
  if (s === 'red') return 'var(--red)'
  if (s === 'orange') return 'var(--yellow)'
  return 'var(--green)'
}

// -----------------------------------------------------------------------------
// Gauge component
// -----------------------------------------------------------------------------

interface GaugeProps {
  score: number
  status: SemaphoreStatus
}

function Gauge({ score, status }: GaugeProps) {
  // Segment boundaries in score units — green 0..40, yellow 40..60,
  // orange 60..80, red 80..100. Rendered as 4 colored bands that overlap at
  // the boundaries with small gaps so they read as a smooth gradient in dark.
  const segments: { from: number; to: number; color: string }[] = [
    { from: 0, to: 40, color: 'var(--green)' },
    { from: 40, to: 60, color: 'var(--yellow)' },
    { from: 60, to: 80, color: '#e8811b' }, // orange between yellow and red
    { from: 80, to: 100, color: 'var(--red)' },
  ]

  const needleAngle = scoreToAngle(score)
  const needleTip = polar(GAUGE_R_NEEDLE, needleAngle)

  // Tick marks every 10 units — 11 ticks from 0 to 100. Labels at 0/20/.../100.
  const ticks = Array.from({ length: 11 }, (_, i) => i * 10)

  const statusColor = colorVarForStatus(status)

  return (
    <svg
      viewBox={`0 0 ${GAUGE_SIZE} ${GAUGE_SIZE * 0.82}`}
      width="100%"
      height="100%"
      role="img"
      aria-label={`Risk score ${score} of 100, status ${status}`}
      style={{ maxWidth: 260 }}
    >
      {/* Background track — subtle thin arc */}
      <path
        d={arcPath(GAUGE_R_OUTER - (GAUGE_R_OUTER - GAUGE_R_INNER) / 2, START_ANGLE_DEG, START_ANGLE_DEG + SWEEP_DEG)}
        stroke="var(--bg-3)"
        strokeWidth={GAUGE_R_OUTER - GAUGE_R_INNER}
        strokeLinecap="butt"
        fill="none"
      />

      {/* Colored segments */}
      {segments.map(seg => {
        const a1 = scoreToAngle(seg.from)
        const a2 = scoreToAngle(seg.to)
        return (
          <path
            key={seg.from}
            d={arcBandPath(GAUGE_R_OUTER, GAUGE_R_INNER, a1, a2)}
            fill={seg.color}
            opacity={0.88}
          />
        )
      })}

      {/* Tick marks */}
      {ticks.map(t => {
        const a = scoreToAngle(t)
        const p1 = polar(GAUGE_R_TICKS_INNER, a)
        const p2 = polar(GAUGE_R_TICKS_OUTER, a)
        const major = t % 20 === 0
        return (
          <line
            key={t}
            x1={p1.x}
            y1={p1.y}
            x2={p2.x}
            y2={p2.y}
            stroke={major ? 'var(--fg-2)' : 'var(--fg-3)'}
            strokeWidth={major ? 1.5 : 1}
          />
        )
      })}

      {/* Major tick labels (0, 20, 40, 60, 80, 100) */}
      {ticks
        .filter(t => t % 20 === 0)
        .map(t => {
          const a = scoreToAngle(t)
          const p = polar(GAUGE_R_TICKS_OUTER + 10, a)
          return (
            <text
              key={`lbl-${t}`}
              x={p.x}
              y={p.y}
              fill="var(--fg-2)"
              fontSize={9}
              fontFamily="var(--font-mono)"
              textAnchor="middle"
              dominantBaseline="middle"
            >
              {t}
            </text>
          )
        })}

      {/* Needle — thin line from a small pivot to the tip, animated on score change */}
      <motion.line
        x1={GAUGE_CX}
        y1={GAUGE_CY}
        x2={needleTip.x}
        y2={needleTip.y}
        stroke={statusColor}
        strokeWidth={2}
        strokeLinecap="round"
        initial={false}
        animate={{ x2: needleTip.x, y2: needleTip.y }}
        transition={{ duration: 0.6, ease: [0.4, 0, 0.2, 1] }}
        style={{ filter: `drop-shadow(0 0 4px ${statusColor})` }}
      />

      {/* Needle pivot */}
      <circle cx={GAUGE_CX} cy={GAUGE_CY} r={6} fill="var(--bg-1)" stroke={statusColor} strokeWidth={2} />
      <circle cx={GAUGE_CX} cy={GAUGE_CY} r={2.5} fill={statusColor} />

      {/* Center readout — large score, tiny label above */}
      <text
        x={GAUGE_CX}
        y={GAUGE_CY - 42}
        fill="var(--fg-2)"
        fontSize={9}
        fontFamily="var(--font-mono)"
        textAnchor="middle"
        letterSpacing="0.12em"
      >
        RISK SCORE
      </text>
      <text
        x={GAUGE_CX}
        y={GAUGE_CY - 18}
        fill="var(--fg-1)"
        fontSize={26}
        fontFamily="var(--font-display)"
        fontWeight={600}
        textAnchor="middle"
        style={{ fontVariantNumeric: 'tabular-nums' }}
      >
        {Math.round(score)}
      </text>
    </svg>
  )
}

// -----------------------------------------------------------------------------
// Small quote row (SPX / VIX) with delta
// -----------------------------------------------------------------------------

interface QuoteRowProps {
  symbol: string
  price: number
  change: number
  changePct: number
}

function QuoteRow({ symbol, price, change, changePct }: QuoteRowProps) {
  const up = change >= 0
  const changeColor = up ? 'text-up' : 'text-down'
  const sign = up ? '+' : '-'
  return (
    <div className="flex items-center justify-between py-[6px] border-b border-border-subtle last:border-0 text-[12.5px]">
      <span className="font-mono font-semibold tracking-wider text-[var(--fg-1)]">{symbol}</span>
      <div className="flex items-baseline gap-3 font-mono tabular-nums">
        <span className="text-[var(--fg-1)]">{price.toFixed(2)}</span>
        <span className={changeColor}>
          {sign}
          {Math.abs(change).toFixed(2)}
        </span>
        <span className={`${changeColor} text-[11.5px]`}>{formatPercent(changePct)}</span>
      </div>
    </div>
  )
}

// -----------------------------------------------------------------------------
// Indicator row
// -----------------------------------------------------------------------------

interface IndicatorRowProps {
  indicator: SemaphoreIndicator
}

function IndicatorRow({ indicator }: IndicatorRowProps) {
  const tone = toneFromStatus(indicator.status)
  return (
    <div className="flex items-center justify-between gap-3 py-[7px] border-b border-border-subtle last:border-0 text-[12.5px]">
      <span className="text-muted whitespace-nowrap">{indicator.label}</span>
      <div className="flex items-center gap-2 min-w-0">
        <span className="font-mono tabular-nums text-[var(--fg-1)] truncate" title={indicator.detail}>
          {indicator.detail}
        </span>
        <Badge tone={tone} size="sm">
          {indicator.status.toUpperCase()}
        </Badge>
      </div>
    </div>
  )
}

// -----------------------------------------------------------------------------
// Main card
// -----------------------------------------------------------------------------

interface SemaphoreCardProps {
  // Optional pre-fetched data (for testing / SSR). When omitted the hook is used.
  data?: SemaphoreData
}

export function SemaphoreCard({ data: dataProp }: SemaphoreCardProps = {}) {
  const query = useSemaphore()
  const data = dataProp ?? query.data
  const isLoading = !dataProp && query.isLoading

  if (isLoading || !data) {
    return (
      <Card>
        <Skeleton h={340} />
      </Card>
    )
  }

  const overallColor = colorVarForStatus(data.status)
  const regimeBullish = data.regime === 'BULLISH'
  const RegimeIcon = regimeBullish ? TrendingUp : TrendingDown

  // Filter out the synthetic "overall" indicator from the per-indicator list
  // because it is already represented by the large status dot.
  const rows = data.indicators.filter(i => i.id !== 'overall')

  return (
    <Card padding={20} className="overflow-hidden">
      {/* Header — title + exchange time */}
      <div className="flex items-start justify-between gap-3 mb-2">
        <div>
          <h3 className="m-0 text-[15px] font-semibold">Options Trading Semaphore</h3>
          <p className="m-0 mt-0.5 text-[11.5px] text-muted">
            SPX put-selling gate · composite indicator
          </p>
        </div>
        <div className="flex items-center gap-1.5 text-[11px] text-muted font-mono tabular-nums">
          <Clock size={11} />
          <span>{data.exchangeTime}</span>
        </div>
      </div>

      {/* Top split — gauge on the left, status + quotes on the right */}
      <div className="grid grid-cols-1 md:grid-cols-[minmax(220px,1fr)_1.2fr] gap-4 mt-2">
        {/* Gauge */}
        <div className="flex items-center justify-center min-h-[180px]">
          <Gauge score={data.score} status={data.status} />
        </div>

        {/* Status + regime + quotes */}
        <div className="flex flex-col gap-3">
          {/* Big status pill */}
          <div
            className="flex items-center gap-3 p-3 rounded-md border"
            style={{
              borderColor: overallColor,
              background: `color-mix(in srgb, ${overallColor} 10%, transparent)`,
            }}
          >
            <span
              className="relative inline-flex w-3.5 h-3.5 rounded-full pulse-dot"
              style={{
                background: overallColor,
                boxShadow: `0 0 12px ${overallColor}`,
              }}
            />
            <div className="flex flex-col">
              <span className="overline">Operatività</span>
              <span
                className="text-[16px] font-semibold tracking-wide"
                style={{ color: overallColor }}
              >
                {statusLabel(data.status)}
              </span>
            </div>
          </div>

          {/* Regime row */}
          <div className="flex items-center justify-between py-1">
            <span className="overline">Long-term Regime</span>
            <div
              className={`inline-flex items-center gap-1.5 font-semibold text-[13px] tracking-wide ${
                regimeBullish ? 'text-up' : 'text-down'
              }`}
            >
              <RegimeIcon size={14} />
              {data.regime}
            </div>
          </div>

          {/* Quotes */}
          <div className="flex flex-col">
            <QuoteRow
              symbol="SPX"
              price={data.spx.price}
              change={data.spx.change}
              changePct={data.spx.changePct}
            />
            <QuoteRow
              symbol="VIX"
              price={data.vix.price}
              change={data.vix.change}
              changePct={data.vix.changePct}
            />
          </div>
        </div>
      </div>

      {/* Divider + indicator rows */}
      <div className="mt-4 pt-3 border-t border-border">
        <div className="flex items-center justify-between mb-2">
          <span className="overline">Indicators</span>
          <span className="text-[11px] text-muted font-mono">{rows.length} signals</span>
        </div>
        {rows.map(ind => (
          <IndicatorRow key={ind.id} indicator={ind} />
        ))}
      </div>
    </Card>
  )
}
