/**
 * Bot Command Dispatcher
 * Routes commands to appropriate handlers
 */

import type { Env } from '../types/env'
import type { Language } from './i18n'
import { t } from './i18n'

/**
 * Bot command types
 */
export type BotCommand =
  | { type: 'menu' }
  | { type: 'query'; query: 'portfolio' | 'status' | 'campaigns' | 'market' | 'strategies' | 'alerts' | 'risk' | 'snapshot' }
  | { type: 'detail'; entity: 'campaign'; id: string }
  | { type: 'refresh'; last: string }
  | { type: 'whitelist'; action: 'add' | 'remove' | 'list'; userId?: string }
  | { type: 'unknown'; raw: string }

/**
 * Parse slash command text to BotCommand
 * @param text - Command text (e.g., "/portfolio", "/start")
 * @returns Parsed command
 */
export function parseCommand(text: string): BotCommand {
  const normalized = text.trim().toLowerCase()

  // Menu commands
  if (normalized === '/start' || normalized === '/menu' || normalized === '/help') {
    return { type: 'menu' }
  }

  // Query commands
  if (normalized === '/portfolio') {
    return { type: 'query', query: 'portfolio' }
  }
  if (normalized === '/status') {
    return { type: 'query', query: 'status' }
  }
  if (normalized === '/campaigns') {
    return { type: 'query', query: 'campaigns' }
  }
  if (normalized === '/market') {
    return { type: 'query', query: 'market' }
  }
  if (normalized === '/strategies') {
    return { type: 'query', query: 'strategies' }
  }
  if (normalized === '/alerts') {
    return { type: 'query', query: 'alerts' }
  }
  if (normalized === '/risk') {
    return { type: 'query', query: 'risk' }
  }
  if (normalized === '/snapshot') {
    return { type: 'query', query: 'snapshot' }
  }

  // Whitelist admin commands
  if (normalized.startsWith('/whitelist')) {
    const parts = text.trim().split(/\s+/)

    if (parts.length === 1 || parts[1] === 'list') {
      return { type: 'whitelist', action: 'list' }
    }

    if (parts[1] === 'add' && parts.length >= 3 && parts[2]) {
      return { type: 'whitelist', action: 'add', userId: parts[2] }
    }

    if (parts[1] === 'remove' && parts.length >= 3 && parts[2]) {
      return { type: 'whitelist', action: 'remove', userId: parts[2] }
    }

    // Invalid whitelist command syntax
    return { type: 'unknown', raw: text }
  }

  // Unknown command
  return { type: 'unknown', raw: text }
}

/**
 * Parse callback data to BotCommand
 * @param data - Callback data string (e.g., "detail:campaign:abc-123", "query:portfolio")
 * @returns Parsed command
 */
export function parseCallbackData(data: string): BotCommand {
  const parts = data.split(':')

  if (parts[0] === 'menu') {
    return { type: 'menu' }
  }

  if (parts[0] === 'query' && parts.length >= 2) {
    const query = parts[1] as BotCommand extends { type: 'query'; query: infer Q } ? Q : never
    return { type: 'query', query }
  }

  if (parts[0] === 'detail' && parts.length >= 3 && parts[2]) {
    if (parts[1] === 'campaign') {
      return { type: 'detail', entity: 'campaign', id: parts[2] }
    }
  }

  if (parts[0] === 'refresh' && parts.length >= 2 && parts[1]) {
    return { type: 'refresh', last: parts[1] }
  }

  return { type: 'unknown', raw: data }
}

/**
 * Message sender function type
 * Abstracts Telegram/Discord send logic
 */
export type SendMessageFn = (text: string, replyMarkup?: unknown) => Promise<void>

/**
 * Dispatch command to appropriate handler
 * @param command - Parsed command
 * @param chatId - Chat ID to send response to
 * @param env - Cloudflare Worker environment
 * @param lang - User language
 * @param sendMessage - Platform-specific message sender
 * @param botType - Bot type ('telegram' or 'discord') for whitelist operations
 * @param adminUserId - Admin user ID for whitelist operations
 */
export async function dispatchCommand(
  command: BotCommand,
  chatId: string,
  env: Env,
  lang: Language,
  sendMessage: SendMessageFn,
  botType: 'telegram' | 'discord' = 'telegram',
  adminUserId?: string
): Promise<void> {
  // Import query handlers
  const { queryPortfolio } = await import('./queries/portfolio-query')
  const { queryStatus } = await import('./queries/status-query')
  const { queryRisk } = await import('./queries/risk-query')
  const { queryMarket } = await import('./queries/market-query')
  const { queryAlerts } = await import('./queries/alerts-query')
  const { queryCampaigns } = await import('./queries/campaigns-query')
  const { queryStrategies } = await import('./queries/strategies-query')

  // Import formatters
  const { formatPortfolio } = await import('./formatters/portfolio-formatter')
  const { formatStatus } = await import('./formatters/status-formatter')
  const { formatRisk } = await import('./formatters/risk-formatter')
  const { formatMarket } = await import('./formatters/market-formatter')
  const { formatSnapshot } = await import('./formatters/snapshot-formatter')

  try {
    switch (command.type) {
      case 'menu':
        // Menu will be handled in bot-telegram.ts / bot-discord.ts
        // to construct inline keyboard / buttons
        break

      case 'query':
        // Handle query commands
        switch (command.query) {
          case 'portfolio': {
            const data = await queryPortfolio(env.DB)
            const text = formatPortfolio(data, lang)
            await sendMessage(text)
            break
          }

          case 'status': {
            const data = await queryStatus(env.DB)
            const text = formatStatus(data, lang)
            await sendMessage(text)
            break
          }

          case 'risk': {
            const data = await queryRisk(env.DB)
            const text = formatRisk(data, lang)
            await sendMessage(text)
            break
          }

          case 'market': {
            const data = await queryMarket(env.DB)
            const text = formatMarket(data, lang)
            await sendMessage(text)
            break
          }

          case 'alerts': {
            const data = await queryAlerts(env.DB)
            const text = formatAlerts(data, lang)
            await sendMessage(text)
            break
          }

          case 'campaigns': {
            const data = await queryCampaigns(env.DB)
            const text = formatCampaigns(data, lang)
            await sendMessage(text)
            break
          }

          case 'strategies': {
            const data = await queryStrategies(env.DB)
            const text = formatStrategies(data, lang)
            await sendMessage(text)
            break
          }

          case 'snapshot': {
            // Snapshot requires multiple queries
            const portfolio = await queryPortfolio(env.DB)
            const status = await queryStatus(env.DB)
            const risk = await queryRisk(env.DB)
            const market = await queryMarket(env.DB)

            const messages = formatSnapshot(portfolio, status, risk, market, lang)

            // Send messages sequentially
            for (const msg of messages) {
              await sendMessage(msg)
            }
            break
          }
        }
        break

      case 'detail':
        // Detail handlers not yet implemented
        console.log(`[BOT] Detail: ${command.entity}:${command.id} for chat ${chatId} (lang: ${lang})`)
        await sendMessage(t('error_generic', lang, { message: 'Detail view not yet implemented' }))
        break

      case 'refresh':
        // Refresh handlers not yet implemented
        console.log(`[BOT] Refresh: ${command.last} for chat ${chatId} (lang: ${lang})`)
        await sendMessage(t('error_generic', lang, { message: 'Refresh not yet implemented' }))
        break

      case 'whitelist': {
        // Import whitelist functions
        const { addToWhitelist, removeFromWhitelist, listWhitelist } = await import('./auth')

        // Handle whitelist commands
        switch (command.action) {
          case 'add': {
            if (!command.userId) {
              await sendMessage(t('whitelist_add_missing_userid', lang))
              break
            }

            const added = await addToWhitelist(command.userId, botType, env.DB, adminUserId)
            if (added) {
              await sendMessage(t('whitelist_add_success', lang, { userId: command.userId }))
            } else {
              await sendMessage(t('whitelist_add_already_exists', lang, { userId: command.userId }))
            }
            break
          }

          case 'remove': {
            if (!command.userId) {
              await sendMessage(t('whitelist_remove_missing_userid', lang))
              break
            }

            const removed = await removeFromWhitelist(command.userId, botType, env.DB)
            if (removed) {
              await sendMessage(t('whitelist_remove_success', lang, { userId: command.userId }))
            } else {
              await sendMessage(t('whitelist_remove_not_found', lang, { userId: command.userId }))
            }
            break
          }

          case 'list': {
            const users = await listWhitelist(botType, env.DB)

            if (users.length === 0) {
              await sendMessage(t('whitelist_list_empty', lang))
            } else {
              const lines: string[] = []
              const title = t('whitelist_list_title', lang)
              lines.push(title)
              lines.push('━━━━━━━━━━━━━━━━━━━━')

              users.forEach((u) => {
                const addedAt = formatShortDate(u.added_at)
                const addedBy = u.added_by ? `by ${u.added_by}` : ''
                const notes = u.notes ? `(${u.notes})` : ''
                lines.push(`👤 \`${u.user_id}\``)
                lines.push(`   ${addedAt} ${addedBy} ${notes}`)
              })

              await sendMessage(lines.join('\n'))
            }
            break
          }
        }
        break
      }

      case 'unknown':
        // Unknown command
        await sendMessage(t('command_unknown', lang))
        break
    }
  } catch (error) {
    console.error('[BOT] dispatchCommand error:', error)
    await sendMessage(t('error_generic', lang, { message: String(error) }))
  }
}

/**
 * Helper formatters for simple list-based queries
 */
function formatAlerts(data: { alerts: Array<{ severity: string; message: string; createdAt: string; resolved: boolean }>; timestamp: string }, lang: string): string {
  const isItalian = lang === 'it'
  const lines: string[] = []
  const title = isItalian ? '🔔 *ALERT*' : '🔔 *ALERTS*'
  lines.push(title)
  lines.push('━━━━━━━━━━━━━━━━━━━━')

  if (data.alerts.length === 0) {
    const noAlerts = isItalian ? 'Nessun alert recente' : 'No recent alerts'
    lines.push(noAlerts)
  } else {
    data.alerts.forEach((a) => {
      const icon = a.severity === 'critical' ? '🔴' : a.severity === 'warning' ? '🟡' : '🔵'
      const status = a.resolved ? (isItalian ? '[RISOLTO]' : '[RESOLVED]') : ''
      lines.push(`${icon} ${a.message} ${status}`)
      lines.push(`   ${formatShortDate(a.createdAt)}`)
      lines.push('')
    })
  }

  return lines.join('\n')
}

function formatCampaigns(data: { campaigns: Array<{ name: string; status: string; positionsCount: number; pnl: number | null }>; timestamp: string }, lang: string): string {
  const isItalian = lang === 'it'
  const lines: string[] = []
  const title = isItalian ? '🎯 *CAMPAGNE ATTIVE*' : '🎯 *ACTIVE CAMPAIGNS*'
  lines.push(title)
  lines.push('━━━━━━━━━━━━━━━━━━━━')

  if (data.campaigns.length === 0) {
    const noCampaigns = isItalian ? 'Nessuna campagna attiva' : 'No active campaigns'
    lines.push(noCampaigns)
  } else {
    data.campaigns.forEach((c) => {
      const pnlStr = c.pnl !== null ? `${c.pnl >= 0 ? '+' : ''}$${c.pnl.toFixed(0)}` : 'N/A'
      const posLabel = isItalian ? 'pos' : 'pos'
      lines.push(`🎯 *${c.name}*`)
      lines.push(`   ${c.status} | ${c.positionsCount} ${posLabel} | PnL: ${pnlStr}`)
      lines.push('')
    })
  }

  return lines.join('\n')
}

function formatStrategies(data: { strategies: Array<{ name: string; status: string; lastSignal: string | null }>; timestamp: string }, lang: string): string {
  const isItalian = lang === 'it'
  const lines: string[] = []
  const title = isItalian ? '🧠 *STRATEGIE*' : '🧠 *STRATEGIES*'
  lines.push(title)
  lines.push('━━━━━━━━━━━━━━━━━━━━')

  if (data.strategies.length === 0) {
    const noStrategies = isItalian ? 'Nessuna strategia attiva' : 'No active strategies'
    lines.push(noStrategies)
  } else {
    data.strategies.forEach((s) => {
      const lastSignal = s.lastSignal ? formatShortDate(s.lastSignal) : 'N/A'
      const lastLabel = isItalian ? 'Ultimo' : 'Last'
      lines.push(`🧠 *${s.name}*`)
      lines.push(`   ${s.status} | ${lastLabel}: ${lastSignal}`)
      lines.push('')
    })
  }

  return lines.join('\n')
}

function formatShortDate(iso: string): string {
  try {
    const date = new Date(iso)
    const day = date.getDate().toString().padStart(2, '0')
    const month = (date.getMonth() + 1).toString().padStart(2, '0')
    const hours = date.getHours().toString().padStart(2, '0')
    const minutes = date.getMinutes().toString().padStart(2, '0')
    return `${day}/${month} ${hours}:${minutes}`
  } catch {
    return iso
  }
}

/**
 * Log command execution to database
 * @param env - Cloudflare Worker environment
 * @param botType - 'telegram' or 'discord'
 * @param userId - User ID
 * @param command - Command string
 * @param responseOk - Whether response was successful
 * @param error - Error message if failed
 */
export async function logCommandExecution(
  env: Env,
  botType: 'telegram' | 'discord',
  userId: string,
  command: string,
  responseOk: boolean,
  error?: string
): Promise<void> {
  try {
    await env.DB.prepare(
      'INSERT INTO bot_command_log (bot_type, user_id, command, response_ok, error) VALUES (?, ?, ?, ?, ?)'
    )
      .bind(botType, userId, command, responseOk ? 1 : 0, error || null)
      .run()
  } catch (dbError) {
    console.error('[BOT] Failed to log command execution:', dbError)
    // Don't throw — logging failure should not block bot response
  }
}
