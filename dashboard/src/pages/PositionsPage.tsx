// PositionsPage — kit Overview-style layout:
//   Row 0: PositionsBreakdown (two donuts)
//   Row 1: PositionsKpiStrip (5 StatCards)
//   Row 2: Filter bar + table/cards area inside a padding-less Card
// Widgets enter with a staggered fade-in-up mirroring HomePage.
import { useState } from 'react'
import { motion } from 'motion/react'
import { Card } from '../components/ui/Card'
import { FilterOverlay } from '../components/ui/FilterOverlay'
import { Skeleton } from '../components/ui/Skeleton'
import { PositionsBreakdown } from '../components/positions/PositionsBreakdown'
import { PositionsKpiStrip } from '../components/positions/PositionsKpiStrip'
import {
  PositionsFilterBar,
  type PositionStatus,
  type ViewMode,
} from '../components/positions/PositionsFilterBar'
import { PositionsTable } from '../components/positions/PositionsTable'
import { PositionCard } from '../components/positions/PositionCard'
import { usePositions } from '../hooks/usePositions'
import type { PositionStatus as ApiPositionStatus } from '../types/position'

// Staggered fade-in-up — matches HomePage (0.28s, ease [0.4,0,0.2,1])
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

// Filter-bar status uses capitalized strings (All/Open/Closed/Pending); the
// API / Position type uses lowercase. Map "All" → undefined so the hook skips
// the filter, otherwise lowercase the UI value.
function toApiStatus(status: PositionStatus): ApiPositionStatus | undefined {
  if (status === 'All') return undefined
  return status.toLowerCase() as ApiPositionStatus
}

export function PositionsPage() {
  const [status, setStatus] = useState<PositionStatus>('All')
  const [typeFilter, setTypeFilter] = useState('All')
  const [query, setQuery] = useState('')
  const [view, setView] = useState<ViewMode>('table')

  const apiStatus = toApiStatus(status)
  const { data, isLoading, isFetching, refetch } = usePositions(
    apiStatus ? { status: apiStatus } : undefined
  )

  // Apply client-side filters for strategy + symbol search on top of the
  // server-side status filter
  const positions = (data?.positions ?? []).filter(p => {
    const matchesType = typeFilter === 'All' || p.strategy === typeFilter
    const matchesQuery =
      query === '' || p.symbol.toLowerCase().includes(query.toLowerCase())
    return matchesType && matchesQuery
  })

  return (
    <div className="p-8 flex flex-col gap-4">
      <motion.div {...stagger(0)}>
        <PositionsBreakdown />
      </motion.div>

      <motion.div {...stagger(1)}>
        <PositionsKpiStrip />
      </motion.div>

      <motion.div {...stagger(2)}>
        <Card padding={0}>
          <PositionsFilterBar
            status={status}
            setStatus={setStatus}
            typeFilter={typeFilter}
            setTypeFilter={setTypeFilter}
            query={query}
            setQuery={setQuery}
            view={view}
            setView={setView}
            onRefresh={() => refetch()}
            isFetching={isFetching}
          />
          <div className="relative min-h-[240px]">
            <FilterOverlay
              visible={isFetching && (data?.positions.length ?? 0) === 0}
              label="Loading positions…"
            />
            {isLoading ? (
              <div className="p-5">
                <Skeleton h={240} />
              </div>
            ) : view === 'table' ? (
              <PositionsTable positions={positions} />
            ) : (
              <div className="p-4 grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3.5">
                {positions.map(p => (
                  <PositionCard key={p.id} position={p} />
                ))}
              </div>
            )}
          </div>
          <div className="px-4 py-2 border-t border-border text-[11px] text-muted text-center">
            {data
              ? `Last updated: ${new Date(data.timestamp).toLocaleString()} · Auto-refresh every 30s`
              : ''}
          </div>
        </Card>
      </motion.div>
    </div>
  )
}
