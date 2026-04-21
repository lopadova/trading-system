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

  // Running offset used to position each arc end-to-end along the ring
  let offset = 0

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
        {segments.map((segment, index) => {
          const frac = segment.value / total
          const dash = frac * circumference
          const gap = circumference - dash
          const arc = (
            <circle
              key={index}
              cx={cx}
              cy={cy}
              r={radius}
              fill="none"
              stroke={segment.color}
              strokeWidth={thickness}
              strokeDasharray={`${dash} ${gap}`}
              strokeDashoffset={-offset}
            />
          )
          offset += dash
          return arc
        })}
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
