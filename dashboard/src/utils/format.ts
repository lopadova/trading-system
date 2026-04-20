// Culture-invariant formatters — always US-style thousands/decimal separators
// regardless of the user's browser locale. Currency symbol is placed before
// the magnitude (e.g. "$125,430.50", "€38,900").

const LOCALE = 'en-US'

export type Currency = 'USD' | 'EUR'

/**
 * Format a currency amount with a fixed number of decimals.
 * Uses the en-US locale so the thousands separator is always ',' and decimal
 * separator is always '.' — never the user's locale.
 */
export function formatCurrency(amount: number, currency: Currency, decimals = 2): string {
  const symbol = currency === 'USD' ? '$' : '€'
  const sign = amount < 0 ? '-' : ''
  const abs = Math.abs(amount).toLocaleString(LOCALE, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })
  return `${sign}${symbol}${abs}`
}

/**
 * Format a percentage value with a sign prefix (e.g. "+14.30%" or "-7.92%").
 * `value` is interpreted as already in percent units (e.g. 14.3 → "+14.30%").
 */
export function formatPercent(value: number, decimals = 2): string {
  const sign = value >= 0 ? '+' : '-'
  return `${sign}${Math.abs(value).toFixed(decimals)}%`
}

/**
 * Render a human-readable delta — "↑ +$2,340.80 (+1.90%)" — with arrow,
 * signed currency amount, and signed percent. Negative amounts get "↓ -$...".
 */
export function formatDelta(amount: number, pct: number, currency: Currency): string {
  const up = amount >= 0
  const arrow = up ? '↑' : '↓'
  // Build currency magnitude without its own sign, then re-prefix +/- so the
  // arrow + sign visually agree
  const baseMagnitude = formatCurrency(Math.abs(amount), currency).replace(/^-/, '')
  const money = `${up ? '+' : '-'}${baseMagnitude}`
  return `${arrow} ${money} (${formatPercent(pct)})`
}
