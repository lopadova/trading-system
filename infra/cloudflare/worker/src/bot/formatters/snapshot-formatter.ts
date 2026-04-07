/**
 * Snapshot Formatter
 * Combines multiple formatters for full snapshot
 */

import type { Language } from '../i18n'
import type { PortfolioData, StatusData, RiskData, MarketData } from '../types'
import { formatPortfolio } from './portfolio-formatter'
import { formatStatus } from './status-formatter'
import { formatRisk } from './risk-formatter'
import { formatMarket } from './market-formatter'

/**
 * Format snapshot data as array of Markdown messages
 * Snapshot is split into 2 messages:
 * 1. Portfolio + Status
 * 2. Risk + Market
 *
 * @param portfolio - Portfolio data
 * @param status - Status data
 * @param risk - Risk data
 * @param market - Market data
 * @param lang - Language ('it' or 'en')
 * @returns Array of formatted Markdown messages
 */
export function formatSnapshot(
  portfolio: PortfolioData,
  status: StatusData,
  risk: RiskData,
  market: MarketData,
  lang: Language
): string[] {
  const isItalian = lang === 'it'

  // Message 1: Portfolio + Status
  const msg1Lines: string[] = []
  const snapshotTitle = isItalian ? '📸 *SNAPSHOT COMPLETO* (1/2)' : '📸 *FULL SNAPSHOT* (1/2)'
  msg1Lines.push(snapshotTitle)
  msg1Lines.push('═══════════════════════════')
  msg1Lines.push('')
  msg1Lines.push(formatPortfolio(portfolio, lang))
  msg1Lines.push('')
  msg1Lines.push('─────────────────────────')
  msg1Lines.push('')
  msg1Lines.push(formatStatus(status, lang))

  // Message 2: Risk + Market
  const msg2Lines: string[] = []
  const snapshot2Title = isItalian ? '📸 *SNAPSHOT COMPLETO* (2/2)' : '📸 *FULL SNAPSHOT* (2/2)'
  msg2Lines.push(snapshot2Title)
  msg2Lines.push('═══════════════════════════')
  msg2Lines.push('')
  msg2Lines.push(formatRisk(risk, lang))
  msg2Lines.push('')
  msg2Lines.push('─────────────────────────')
  msg2Lines.push('')
  msg2Lines.push(formatMarket(market, lang))

  return [msg1Lines.join('\n'), msg2Lines.join('\n')]
}
