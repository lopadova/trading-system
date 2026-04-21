// PositionsBreakdown — side-by-side exposure donuts (strategy + asset) driven
// by usePositionsBreakdown. Each card renders a Donut with the total at its
// center and a text legend showing percentage + monetary value per segment.
import { Card } from '../ui/Card'
import { Skeleton } from '../ui/Skeleton'
import { Donut } from './Donut'
import { usePositionsBreakdown } from '../../hooks/usePositionsBreakdown'
import type { ExposureSegment } from '../../types/breakdown'

function Legend({ segments, total }: { segments: ExposureSegment[]; total: number }) {
  return (
    <div className="flex-1 flex flex-col gap-2">
      {segments.map(segment => {
        const pct = total > 0 ? (segment.value / total) * 100 : 0
        return (
          <div
            key={segment.label}
            className="flex items-center gap-2.5 text-[12.5px]"
          >
            <span
              className="w-2.5 h-2.5 rounded-sm shrink-0"
              style={{ background: segment.color }}
            />
            <span className="flex-1 text-[var(--fg-1)]">{segment.label}</span>
            <span className="font-mono text-muted tabular-nums">
              {pct.toFixed(1)}%
            </span>
            <span className="font-mono font-medium tabular-nums min-w-[70px] text-right">
              €{segment.value.toLocaleString('en-US')}
            </span>
          </div>
        )
      })}
    </div>
  )
}

export function PositionsBreakdown() {
  const { data, isLoading } = usePositionsBreakdown()

  // Loading state — render skeletons sized to match the final donut area so the
  // layout does not jump once data lands
  if (isLoading || !data) {
    return (
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <Card>
          <Skeleton h={200} />
        </Card>
        <Card>
          <Skeleton h={200} />
        </Card>
      </div>
    )
  }

  const sumStrategy = data.byStrategy.reduce((sum, x) => sum + x.value, 0)
  const sumAsset = data.byAsset.reduce((sum, x) => sum + x.value, 0)

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
      <Card>
        <h3 className="m-0 text-[14px] font-semibold mb-3.5">
          Exposure by Strategy
        </h3>
        <div className="flex items-center gap-5">
          <Donut
            segments={data.byStrategy}
            centerLabel={`€${(sumStrategy / 1000).toFixed(1)}k`}
            centerSub="total"
          />
          <Legend segments={data.byStrategy} total={sumStrategy} />
        </div>
      </Card>
      <Card>
        <h3 className="m-0 text-[14px] font-semibold mb-3.5">
          Exposure by Asset
        </h3>
        <div className="flex items-center gap-5">
          <Donut
            segments={data.byAsset}
            centerLabel={`€${(sumAsset / 1000).toFixed(1)}k`}
            centerSub="total"
          />
          <Legend segments={data.byAsset} total={sumAsset} />
        </div>
      </Card>
    </div>
  )
}
