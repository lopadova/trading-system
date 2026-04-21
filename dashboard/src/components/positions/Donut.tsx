// Donut — a lightweight ring chart primitive backed by SVG strokeDasharray.
// Renders one background circle (for the empty track) plus one circle per
// segment; each segment is offset along the circumference so they line up
// end-to-end. The center holds a label + sub-label (typical: total + caption).
import type { HTMLAttributes } from 'react'
import type { ExposureSegment } from '../../types/breakdown'

export interface DonutProps extends HTMLAttributes<HTMLDivElement> {
  segments: ExposureSegment[]
  size?: number
  thickness?: number
  centerLabel: string
  centerSub: string
}

export function Donut({
  segments,
  size = 180,
  thickness = 28,
  centerLabel,
  centerSub,
  ...rest
}: DonutProps) {
  const total = segments.reduce((sum, x) => sum + x.value, 0) || 1
  const cx = size / 2
  const cy = size / 2
  const radius = (size - thickness) / 2
  const circumference = 2 * Math.PI * radius

  // Pre-compute each arc's dashoffset once so render stays pure (no mutation).
  // cumulative[i] = sum of dashes for arcs [0..i-1]; that's the negative offset
  // each arc needs to land end-to-end along the ring.
  const arcs = segments.reduce<{
    offsetSoFar: number
    out: { key: number; dash: number; gap: number; offset: number; color: string }[]
  }>(
    (acc, segment, index) => {
      const frac = segment.value / total
      const dash = frac * circumference
      const gap = circumference - dash
      acc.out.push({ key: index, dash, gap, offset: acc.offsetSoFar, color: segment.color })
      acc.offsetSoFar += dash
      return acc
    },
    { offsetSoFar: 0, out: [] }
  ).out

  return (
    <div
      className="relative shrink-0"
      style={{ width: size, height: size }}
      {...rest}
    >
      <svg width={size} height={size} style={{ transform: 'rotate(-90deg)' }}>
        {/* Background track — kept neutral so empty segments still render a ring */}
        <circle
          cx={cx}
          cy={cy}
          r={radius}
          fill="none"
          stroke="var(--bg-1)"
          strokeWidth={thickness}
        />
        {arcs.map(arc => (
          <circle
            key={arc.key}
            cx={cx}
            cy={cy}
            r={radius}
            fill="none"
            stroke={arc.color}
            strokeWidth={thickness}
            strokeDasharray={`${arc.dash} ${arc.gap}`}
            strokeDashoffset={-arc.offset}
          />
        ))}
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <div className="font-mono text-[18px] font-semibold tabular-nums text-[var(--fg-1)]">
          {centerLabel}
        </div>
        <div className="text-[10.5px] text-muted mt-0.5 uppercase tracking-wider">
          {centerSub}
        </div>
      </div>
    </div>
  )
}
