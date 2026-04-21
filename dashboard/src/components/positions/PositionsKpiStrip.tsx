// PositionsKpiStrip — 5-up KPI row above the positions table:
// Open P&L · Realized 7d (placeholder) · Open count · Avg DTE · Buying Power.
// The buying-power card pulls from useRiskMetrics so it tracks the same
// real-time risk snapshot as the Overview page.
import { StatCard } from '../ui/StatCard'
import {
  TrendingUp,
  CheckCircle2,
  Layers,
  Calendar,
  DollarSign,
} from 'lucide-react'
import { usePositions } from '../../hooks/usePositions'
import { useRiskMetrics } from '../../hooks/useRiskMetrics'
import { formatCurrency } from '../../utils/format'
import type { Position } from '../../types/position'

// DTE helper — days from today to the option expiration. Returns 0 for
// non-option positions (no expiration) or if the expiration is in the past.
function daysToExpiration(position: Position): number {
  if (!position.expiration) return 0
  const exp = new Date(position.expiration).getTime()
  const now = Date.now()
  const diff = Math.round((exp - now) / (1000 * 60 * 60 * 24))
  return Math.max(0, diff)
}

export function PositionsKpiStrip() {
  // Only open positions contribute to the live P&L / DTE aggregates
  const { data: positions } = usePositions({ status: 'open' })
  const { data: risk } = useRiskMetrics()

  const open = positions?.positions ?? []
  const openPl = open.reduce((sum, p) => sum + (p.unrealizedPnl ?? 0), 0)
  const avgDte = open.length
    ? Math.round(open.reduce((sum, p) => sum + daysToExpiration(p), 0) / open.length)
    : 0

  return (
    <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
      <StatCard
        label="Open P&L"
        value={formatCurrency(openPl, 'USD')}
        delta="today"
        deltaTone={openPl >= 0 ? 'green' : 'red'}
        icon={TrendingUp}
      />
      <StatCard
        label="Realized · 7d"
        value="—"
        delta="rolling 7 days"
        deltaTone="muted"
        icon={CheckCircle2}
      />
      <StatCard
        label="Open Positions"
        value={String(open.length)}
        delta={`avg DTE ${avgDte}d`}
        icon={Layers}
      />
      <StatCard
        label="Avg DTE"
        value={`${avgDte}d`}
        delta="across open book"
        icon={Calendar}
      />
      <StatCard
        label="Buying Power"
        value={risk ? formatCurrency(risk.buyingPower, 'USD', 0) : '—'}
        delta={risk ? `margin ${risk.marginUsedPct}%` : ''}
        deltaTone={risk && risk.marginUsedPct > 70 ? 'red' : 'yellow'}
        icon={DollarSign}
      />
    </div>
  )
}
