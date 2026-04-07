/**
 * Market Formatter
 * Formats market data as Markdown message
 */

import type { Language } from '../i18n'
import type { MarketData } from '../types'
import { ivtsSignal } from '../semaphores'

/**
 * Format market data as Markdown
 * @param data - Market data
 * @param lang - Language ('it' or 'en')
 * @returns Formatted Markdown message
 */
export function formatMarket(data: MarketData, lang: Language): string {
  const isItalian = lang === 'it'

  // Header
  const lines: string[] = []
  const title = isItalian ? '📈 *MARKET DATA*' : '📈 *MARKET DATA*'
  lines.push(title)
  lines.push('━━━━━━━━━━━━━━━━━━━━')

  // SPX
  const spxStr = data.spx !== null ? formatNumber(data.spx, 2) : 'N/A'
  const changeStr = data.spxChange !== null
    ? `(${data.spxChange >= 0 ? '+' : ''}${data.spxChange.toFixed(2)})`
    : ''
  const changeSig = data.spxChange !== null
    ? data.spxChange >= 0 ? '🟢' : '🔴'
    : ''
  lines.push(`📉 SPX: ${spxStr} ${changeStr} ${changeSig}`)

  // VIX
  const vixStr = data.vix !== null ? data.vix.toFixed(1) : 'N/A'
  const vix3mStr = data.vix3m !== null ? data.vix3m.toFixed(1) : 'N/A'
  lines.push(`😱 VIX: ${vixStr}  VIX3M: ${vix3mStr}`)

  // IVTS
  const ivtsStr = data.ivts !== null ? data.ivts.toFixed(2) : 'N/A'
  const ivtsSig = ivtsSignal(data.ivts, data.ivtsState)
  const stateLabel = data.ivtsState === 'Active'
    ? (isItalian ? 'ATTIVO' : 'ACTIVE')
    : (isItalian ? 'SOSPESO' : 'SUSPENDED')
  lines.push(`🌡️ IVTS: ${ivtsStr}  ${ivtsSig} ${stateLabel}`)

  // IVTS Sparkline (last 30 days)
  if (data.ivtsSparkline.length > 0) {
    const sparklineLabel = isItalian
      ? 'IVTS ultimi 30gg'
      : 'IVTS last 30 days'
    const sparkline = generateSparkline(data.ivtsSparkline)
    lines.push('')
    lines.push(`📊 ${sparklineLabel}:`)
    lines.push(sparkline)
  }

  lines.push('')

  // Timestamp
  const timestampStr = formatTimestamp(data.timestamp, lang)
  lines.push(`🕐 ${timestampStr}`)

  return lines.join('\n')
}

/**
 * Generate ASCII sparkline from data
 */
function generateSparkline(data: number[]): string {
  if (data.length === 0) {
    return 'N/A'
  }

  const min = Math.min(...data)
  const max = Math.max(...data)
  const range = max - min

  if (range === 0) {
    return '▄'.repeat(data.length)
  }

  const chars = ['▁', '▂', '▃', '▄', '▅', '▆', '▇', '█']
  const sparkline = data.map((value) => {
    const normalized = (value - min) / range
    const index = Math.floor(normalized * (chars.length - 1))
    return chars[index]
  })

  return sparkline.join('')
}

/**
 * Format number with thousand separators
 */
function formatNumber(value: number, decimals: number): string {
  const parts = value.toFixed(decimals).split('.')
  if (parts[0]) {
    parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, '.')
  }
  return parts.join(',')
}

/**
 * Format timestamp
 */
function formatTimestamp(iso: string, lang: Language): string {
  try {
    const date = new Date(iso)
    const day = date.getDate().toString().padStart(2, '0')
    const month = (date.getMonth() + 1).toString().padStart(2, '0')
    const year = date.getFullYear()
    const hours = date.getHours().toString().padStart(2, '0')
    const minutes = date.getMinutes().toString().padStart(2, '0')
    const seconds = date.getSeconds().toString().padStart(2, '0')

    return `${day}/${month}/${year} ${hours}:${minutes}:${seconds} ET`
  } catch {
    return iso
  }
}
