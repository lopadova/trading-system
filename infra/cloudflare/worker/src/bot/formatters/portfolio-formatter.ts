/**
 * Portfolio Formatter
 * Formats portfolio data as Markdown message
 */

import type { Language } from '../i18n'
import type { PortfolioData } from '../types'
import { pnlSignal, ivtsSignal } from '../semaphores'

/**
 * Format portfolio data as Markdown
 * @param data - Portfolio data
 * @param lang - Language ('it' or 'en')
 * @returns Formatted Markdown message
 */
export function formatPortfolio(data: PortfolioData, lang: Language): string {
  const isItalian = lang === 'it'

  // Header
  const lines: string[] = []
  lines.push(isItalian ? '📊 *PORTFOLIO*' : '📊 *PORTFOLIO*')
  lines.push('━━━━━━━━━━━━━━━━━━━━')

  // PnL metrics
  const todaySignal = pnlSignal(data.pnlToday)
  const mtdSignal = pnlSignal(data.pnlMTD)
  const ytdSignal = pnlSignal(data.pnlYTD)

  const todayLabel = isItalian ? 'PnL Oggi' : 'PnL Today'
  const mtdLabel = isItalian ? 'PnL MTD' : 'PnL MTD'
  const ytdLabel = isItalian ? 'PnL YTD' : 'PnL YTD'
  const winRateLabel = isItalian ? 'Win Rate' : 'Win Rate'

  lines.push(
    `💰 ${todayLabel}: ${formatPnL(data.pnlToday)} ${todaySignal}`
  )
  lines.push(
    `💰 ${mtdLabel}: ${formatPnL(data.pnlMTD)} ${mtdSignal}`
  )
  lines.push(
    `💰 ${ytdLabel}: ${formatPnL(data.pnlYTD)} ${ytdSignal}`
  )

  if (data.winRate !== null) {
    const wrSignal = data.winRate >= 70 ? '🟢' : data.winRate >= 50 ? '🟡' : '🔴'
    lines.push(`📈 ${winRateLabel}: ${data.winRate.toFixed(0)}% ${wrSignal}`)
  }

  lines.push('')

  // Active campaigns
  const campaignsLabel = isItalian ? 'CAMPAGNE ATTIVE' : 'ACTIVE CAMPAIGNS'
  lines.push(`📌 ${campaignsLabel}: ${data.activeCampaigns.length}`)

  if (data.activeCampaigns.length > 0) {
    data.activeCampaigns.forEach((c, idx) => {
      const prefix = idx < data.activeCampaigns.length - 1 ? '├─' : '└─'
      const pnlStr = formatPnL(c.pnl)
      const signal = pnlSignal(c.pnl)
      lines.push(
        `${prefix} ${c.name}  D+${c.daysElapsed}  PnL: ${pnlStr}  ${signal}`
      )
    })
  } else {
    const noCampaigns = isItalian ? 'Nessuna campagna attiva' : 'No active campaigns'
    lines.push(`  ${noCampaigns}`)
  }

  lines.push('')

  // Market data
  const ivtsStr = data.ivts !== null ? data.ivts.toFixed(2) : 'N/A'
  const ivtsSignalStr = ivtsSignal(data.ivts, data.ivtsState)
  const stateLabel = data.ivtsState === 'Active'
    ? (isItalian ? 'ATTIVO' : 'ACTIVE')
    : (isItalian ? 'SOSPESO' : 'SUSPENDED')

  lines.push(`🌡️ IVTS: ${ivtsStr}  ${ivtsSignalStr} ${stateLabel}`)

  const spxStr = data.spx !== null ? formatNumber(data.spx, 2) : 'N/A'
  lines.push(`📉 SPX: ${spxStr}`)

  const vixStr = data.vix !== null ? data.vix.toFixed(1) : 'N/A'
  const vix3mStr = data.vix3m !== null ? data.vix3m.toFixed(1) : 'N/A'
  lines.push(`😱 VIX: ${vixStr}  VIX3M: ${vix3mStr}`)

  lines.push('')

  // Timestamp
  const timestampStr = formatTimestamp(data.timestamp, lang)
  lines.push(`🕐 ${timestampStr}`)

  return lines.join('\n')
}

/**
 * Format PnL value
 */
function formatPnL(value: number | null): string {
  if (value === null) {
    return 'N/A'
  }

  const sign = value >= 0 ? '+' : ''
  return `${sign}$${value.toFixed(0)}`
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
