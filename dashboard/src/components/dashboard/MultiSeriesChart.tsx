// MultiSeriesChart — 3-series line chart (Portfolio / S&P 500 / SWDA) with
// per-series toggle chips and a range selector. Series are rendered from
// cached performance/series data for the active asset bucket.

import { useState } from 'react'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  Area,
  CartesianGrid,
} from 'recharts'
import { SegmentedControl } from '../ui/SegmentedControl'
import { FilterOverlay } from '../ui/FilterOverlay'
import { usePerformanceSeries } from '../../hooks/usePerformanceSeries'
import type { AssetBucket, PerfRange } from '../../types/performance'
import { cn } from '../../utils/cn'

const RANGES: PerfRange[] = ['1W', '1M', '3M', 'YTD', '1Y', 'ALL']

interface SeriesSpec {
  key: 'portfolio' | 'sp500' | 'swda'
  label: string
  color: string
  strokeWidth: number
  dash: string | undefined
}

const SERIES: SeriesSpec[] = [
  { key: 'portfolio', label: 'Portfolio', color: '#2f81f7', strokeWidth: 2, dash: undefined },
  { key: 'sp500', label: 'S&P 500', color: '#a371f7', strokeWidth: 1.5, dash: '4 3' },
  { key: 'swda', label: 'SWDA', color: '#3fb950', strokeWidth: 1.5, dash: '1 3' },
]

type SeriesKey = SeriesSpec['key']

export function MultiSeriesChart({ asset }: { asset: AssetBucket }) {
  const [range, setRange] = useState<PerfRange>('1M')
  // Per-series visibility state — all three visible by default
  const [shown, setShown] = useState<Record<SeriesKey, boolean>>({
    portfolio: true,
    sp500: true,
    swda: true,
  })
  const { data, isFetching } = usePerformanceSeries(asset, range)

  // Transform parallel arrays into recharts row objects keyed by i
  const chartData = data
    ? data.portfolio.map((v, i) => ({
        i,
        portfolio: v,
        sp500: data.sp500[i],
        swda: data.swda[i],
      }))
    : []

  return (
    <div className="relative">
      <div className="flex justify-between items-start mb-3">
        <div>
          <h3 className="text-[15px] font-semibold m-0">Account Performance</h3>
          <div className="text-[12px] text-muted mt-0.5">
            Normalized to 100 · vs S&amp;P 500 and SWDA
          </div>
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
                on
                  ? 'bg-[var(--bg-3)] text-[var(--fg-1)]'
                  : 'bg-transparent text-muted opacity-60',
              )}
            >
              <span
                className="w-2.5 h-0.5 rounded"
                style={{ background: s.color }}
              />
              {s.label}
            </button>
          )
        })}
      </div>

      <div className="h-[260px] relative">
        <FilterOverlay
          visible={isFetching && chartData.length === 0}
          label="Loading chart…"
        />
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={chartData} margin={{ top: 10, right: 12, bottom: 10, left: 0 }}>
            <defs>
              {/* Portfolio area gradient — fades from accent blue down to transparent */}
              <linearGradient id="msPerfGrad" x1="0" x2="0" y1="0" y2="1">
                <stop offset="0%" stopColor="#2f81f7" stopOpacity={0.3} />
                <stop offset="100%" stopColor="#2f81f7" stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid
              stroke="var(--border-2)"
              strokeDasharray="0"
              vertical={false}
            />
            <XAxis
              dataKey="i"
              stroke="var(--fg-2)"
              fontSize={10}
              tickLine={false}
              axisLine={{ stroke: 'var(--border-1)' }}
            />
            <YAxis
              stroke="var(--fg-2)"
              fontSize={10}
              tickLine={false}
              axisLine={{ stroke: 'var(--border-1)' }}
              domain={['auto', 'auto']}
            />
            <Tooltip
              contentStyle={{
                background: 'var(--bg-2)',
                border: '1px solid var(--border-1)',
                borderRadius: 8,
                fontSize: 12,
              }}
              labelStyle={{ color: 'var(--fg-2)' }}
            />
            {shown.portfolio && (
              <Area
                type="monotone"
                dataKey="portfolio"
                stroke="none"
                fill="url(#msPerfGrad)"
                isAnimationActive={false}
              />
            )}
            {SERIES.map(
              s =>
                shown[s.key] && (
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
                ),
            )}
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
