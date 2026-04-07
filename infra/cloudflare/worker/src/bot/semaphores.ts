/**
 * Bot Semaphores — Signal Logic
 * Visual indicators (🔴🟡🟢⚪) for risk metrics
 */

export type Signal = '🟢' | '🟡' | '🔴' | '⚪'

/**
 * PnL signal
 * Green: >= 0, Yellow: >= -200, Red: < -200, White: null
 */
export function pnlSignal(pnl: number | null): Signal {
  if (pnl === null || pnl === undefined) {
    return '⚪'
  }
  if (pnl >= 0) {
    return '🟢'
  }
  if (pnl >= -200) {
    return '🟡'
  }
  return '🔴'
}

/**
 * PnL vs Stop signal
 * Calculates ratio = abs(pnl) / abs(stop)
 * Green: < 50%, Yellow: 50-80%, Red: >= 80%, White: null
 */
export function pnlVsStopSignal(pnl: number | null, stop: number | null): Signal {
  if (pnl === null || pnl === undefined || stop === null || stop === undefined) {
    return '⚪'
  }
  if (stop === 0) {
    return '⚪'
  }

  const ratio = Math.abs(pnl) / Math.abs(stop)

  if (ratio < 0.5) {
    return '🟢'
  }
  if (ratio < 0.8) {
    return '🟡'
  }
  return '🔴'
}

/**
 * Heartbeat age signal (minutes)
 * Green: < 3min, Yellow: 3-10min, Red: >= 10min, White: null
 */
export function heartbeatSignal(ageMinutes: number | null): Signal {
  if (ageMinutes === null || ageMinutes === undefined) {
    return '⚪'
  }
  if (ageMinutes < 3) {
    return '🟢'
  }
  if (ageMinutes < 10) {
    return '🟡'
  }
  return '🔴'
}

/**
 * IVTS signal
 * Green: Active AND < 1.10, Yellow: 1.10-1.15, Red: Suspended OR > 1.15, White: null
 */
export function ivtsSignal(ivts: number | null, state: string | null): Signal {
  if (ivts === null || ivts === undefined || state === null || state === undefined) {
    return '⚪'
  }

  if (state !== 'Active') {
    return '🔴'
  }

  if (ivts > 1.15) {
    return '🔴'
  }
  if (ivts >= 1.10) {
    return '🟡'
  }
  return '🟢'
}

/**
 * Delta signal
 * Calculates ratio = abs(delta) / abs(limit)
 * Green: < 60%, Yellow: 60-85%, Red: >= 85%, White: null
 */
export function deltaSignal(delta: number | null, limit: number | null): Signal {
  if (delta === null || delta === undefined || limit === null || limit === undefined) {
    return '⚪'
  }
  if (limit === 0) {
    return '⚪'
  }

  const ratio = Math.abs(delta) / Math.abs(limit)

  if (ratio < 0.6) {
    return '🟢'
  }
  if (ratio < 0.85) {
    return '🟡'
  }
  return '🔴'
}

/**
 * Theta signal
 * Calculates ratio = abs(theta) / abs(limit)
 * Green: < 60%, Yellow: 60-85%, Red: >= 85%, White: null
 */
export function thetaSignal(theta: number | null, limit: number | null): Signal {
  if (theta === null || theta === undefined || limit === null || limit === undefined) {
    return '⚪'
  }
  if (limit === 0) {
    return '⚪'
  }

  const ratio = Math.abs(theta) / Math.abs(limit)

  if (ratio < 0.6) {
    return '🟢'
  }
  if (ratio < 0.85) {
    return '🟡'
  }
  return '🔴'
}

/**
 * SPX vs Wing distance signal (points)
 * Green: > 150pt, Yellow: 50-150pt, Red: < 50pt, White: null
 */
export function spxVsWingSignal(spx: number | null, wing: number | null): Signal {
  if (spx === null || spx === undefined || wing === null || wing === undefined) {
    return '⚪'
  }

  const distance = Math.abs(spx - wing)

  if (distance > 150) {
    return '🟢'
  }
  if (distance > 50) {
    return '🟡'
  }
  return '🔴'
}

/**
 * Days remaining signal
 * Calculates percentage remaining = (days / maxDays) * 100
 * Green: > 30%, Yellow: 10-30%, Red: < 10%, White: null
 */
export function daysRemainingSignal(days: number | null, maxDays: number | null): Signal {
  if (days === null || days === undefined || maxDays === null || maxDays === undefined) {
    return '⚪'
  }
  if (maxDays === 0) {
    return '⚪'
  }

  const percentRemaining = (days / maxDays) * 100

  if (percentRemaining > 30) {
    return '🟢'
  }
  if (percentRemaining >= 10) {
    return '🟡'
  }
  return '🔴'
}

/**
 * Process status signal
 * Green: running, Red: stopped, Yellow: anything else, White: null
 */
export function processSignal(status: string | null): Signal {
  if (status === null || status === undefined) {
    return '⚪'
  }

  if (status === 'running') {
    return '🟢'
  }
  if (status === 'stopped') {
    return '🔴'
  }
  return '🟡'
}
