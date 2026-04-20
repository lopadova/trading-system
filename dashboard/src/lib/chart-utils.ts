// Chart helpers — Y-axis normalization with padding and date/month label generation
// for X-axis ticks. Used by the Overview widgets' Recharts series.

export interface YAxisRange {
  min: number
  max: number
}

/**
 * Normalize a numeric series into a padded Y-axis range so chart lines don't
 * touch the top/bottom edges. Returns a sensible default when the input is empty.
 */
export function normalizeYAxis(values: number[], padRatio = 0.08): YAxisRange {
  // Guard: empty input returns a visible default range so the chart axis is still drawn
  if (values.length === 0) return { min: 0, max: 100 }
  const max = Math.max(...values)
  const min = Math.min(...values)
  // Ensure we always have at least a small pad even for constant series (max === min)
  const pad = (max - min) * padRatio || 2
  return { min: min - pad, max: max + pad }
}

export interface DateLabel {
  idx: number
  label: string
}

/**
 * Generate `steps` evenly-spaced date tick labels ending at `endDate`, working
 * backwards over `span` days. Used on the X-axis of daily performance charts.
 */
export function generateDateLabels(endDate: Date, span: number, steps: number): DateLabel[] {
  const labels: DateLabel[] = []
  for (let i = 0; i < steps; i++) {
    // Map step i in [0, steps-1] to series index in [0, span-1]
    const idx = Math.round((i / (steps - 1)) * (span - 1))
    const daysAgo = span - 1 - idx
    const d = new Date(endDate)
    d.setDate(d.getDate() - daysAgo)
    labels.push({ idx, label: d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) })
  }
  return labels
}

/**
 * Generate `steps` evenly-spaced month tick labels (e.g. "Apr 26"), working
 * backwards over `span` months from `endDate`. Used on long-range performance charts.
 */
export function generateMonthLabels(endDate: Date, span: number, steps: number): DateLabel[] {
  const labels: DateLabel[] = []
  for (let i = 0; i < steps; i++) {
    const idx = Math.round((i / (steps - 1)) * (span - 1))
    const monthsAgo = span - 1 - idx
    const d = new Date(endDate)
    d.setMonth(d.getMonth() - monthsAgo)
    labels.push({ idx, label: d.toLocaleDateString('en-US', { month: 'short', year: '2-digit' }) })
  }
  return labels
}
