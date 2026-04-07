/**
 * Risk Formatter
 * Formats risk metrics data as Markdown message
 */

import type { Language } from '../i18n'
import type { RiskData } from '../types'
import {
  pnlSignal,
  pnlVsStopSignal,
  deltaSignal,
  thetaSignal,
  spxVsWingSignal,
  daysRemainingSignal
} from '../semaphores'

/**
 * Format risk data as Markdown
 * @param data - Risk data
 * @param lang - Language ('it' or 'en')
 * @returns Formatted Markdown message
 */
export function formatRisk(data: RiskData, lang: Language): string {
  const isItalian = lang === 'it'

  // Header
  const lines: string[] = []
  const title = isItalian ? '⚠️ *RISK MONITOR*' : '⚠️ *RISK MONITOR*'
  lines.push(title)
  lines.push('━━━━━━━━━━━━━━━━━━━━')

  if (data.campaigns.length === 0) {
    const noCampaigns = isItalian
      ? 'Nessuna campagna attiva'
      : 'No active campaigns'
    lines.push(noCampaigns)
  } else {
    data.campaigns.forEach((c, idx) => {
      // Campaign header
      lines.push(`📍 *${c.name}*`)

      // PnL and Stop
      const pnlStr = formatPnL(c.pnl)
      const pnlSig = pnlSignal(c.pnl)
      const pnlLabel = isItalian ? 'PnL' : 'PnL'
      lines.push(`   ${pnlLabel}: ${pnlStr} ${pnlSig}`)

      if (c.stop !== null) {
        const stopStr = formatPnL(c.stop)
        const pnlVsStopSig = pnlVsStopSignal(c.pnl, c.stop)
        const stopLabel = isItalian ? 'Stop' : 'Stop'
        const vsLabel = isItalian ? 'vs Stop' : 'vs Stop'
        lines.push(`   ${stopLabel}: ${stopStr}`)

        if (c.pnl !== null) {
          const ratio = ((Math.abs(c.pnl) / Math.abs(c.stop)) * 100).toFixed(0)
          lines.push(`   ${vsLabel}: ${ratio}% ${pnlVsStopSig}`)
        }
      }

      // Greeks
      if (c.delta !== null) {
        const deltaSig = deltaSignal(c.delta, 1000) // TODO: Get actual limit
        const deltaLabel = isItalian ? 'Delta' : 'Delta'
        lines.push(`   ${deltaLabel}: ${c.delta.toFixed(0)} ${deltaSig}`)
      }

      if (c.theta !== null) {
        const thetaSig = thetaSignal(c.theta, 1000) // TODO: Get actual limit
        const thetaLabel = isItalian ? 'Theta' : 'Theta'
        lines.push(`   ${thetaLabel}: ${c.theta.toFixed(0)} ${thetaSig}`)
      }

      // Wing distance
      if (c.spxCurrent !== null && c.wingLower !== null && c.wingUpper !== null) {
        const distLower = c.spxCurrent - c.wingLower
        const distUpper = c.wingUpper - c.spxCurrent
        const minDist = Math.min(distLower, distUpper)
        const closestWing = distLower < distUpper ? c.wingLower : c.wingUpper
        const wingSig = spxVsWingSignal(c.spxCurrent, closestWing)
        const wingLabel = isItalian ? 'Dist. Wing' : 'Wing Dist'
        lines.push(`   ${wingLabel}: ${minDist.toFixed(0)}pt ${wingSig}`)
      }

      // Days remaining
      if (c.daysElapsed !== null && c.maxDays !== null) {
        const daysRemaining = c.maxDays - c.daysElapsed
        const daysSig = daysRemainingSignal(daysRemaining, c.maxDays)
        const daysLabel = isItalian ? 'Giorni Rim.' : 'Days Rem.'
        lines.push(`   ${daysLabel}: ${daysRemaining}/${c.maxDays} ${daysSig}`)
      }

      // Separator between campaigns
      if (idx < data.campaigns.length - 1) {
        lines.push('')
      }
    })
  }

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
