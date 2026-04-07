/**
 * Telegram Bot Webhook Handler
 * Handles incoming Telegram webhook requests
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import { verifyTelegramSignature, isWhitelisted, isWhitelistedInDb } from '../bot/auth'
import { parseCommand, parseCallbackData, dispatchCommand, logCommandExecution } from '../bot/dispatcher'
import { detectLanguage, t } from '../bot/i18n'

const app = new Hono<{ Bindings: Env }>()

/**
 * Telegram Update structure
 */
interface TelegramUpdate {
  update_id: number
  message?: {
    message_id: number
    from: {
      id: number
      is_bot: boolean
      first_name: string
      last_name?: string
      username?: string
      language_code?: string
    }
    chat: {
      id: number
      type: string
    }
    date: number
    text?: string
  }
  callback_query?: {
    id: string
    from: {
      id: number
      is_bot: boolean
      first_name: string
      last_name?: string
      username?: string
      language_code?: string
    }
    message?: {
      message_id: number
      chat: {
        id: number
        type: string
      }
    }
    data?: string
  }
}

/**
 * POST /webhook/telegram
 * Telegram webhook endpoint
 */
app.post('/webhook/telegram', async (c) => {
  const body = await c.req.text()
  const headerToken = c.req.header('X-Telegram-Bot-Api-Secret-Token')

  // Verify signature
  const botToken = c.env.TELEGRAM_BOT_TOKEN
  if (!botToken) {
    console.error('[TELEGRAM] Bot token not configured')
    return c.json({ error: 'bot_not_configured' }, 500)
  }

  const isValid = await verifyTelegramSignature(body, botToken, headerToken || null)
  if (!isValid) {
    console.warn('[TELEGRAM] Invalid signature')
    return c.json({ error: 'invalid_signature' }, 401)
  }

  // Parse update
  let update: TelegramUpdate
  try {
    update = JSON.parse(body)
  } catch (error) {
    console.error('[TELEGRAM] Invalid JSON:', error)
    return c.json({ error: 'invalid_json' }, 400)
  }

  // Extract user ID and chat ID
  let userId: string
  let chatId: number
  let languageCode: string | undefined
  let commandText: string | undefined
  let callbackData: string | undefined

  if (update.message?.text) {
    // Text message
    userId = update.message.from.id.toString()
    chatId = update.message.chat.id
    languageCode = update.message.from.language_code
    commandText = update.message.text
  } else if (update.callback_query?.data) {
    // Callback query (button press)
    userId = update.callback_query.from.id.toString()
    chatId = update.callback_query.message?.chat.id || 0
    languageCode = update.callback_query.from.language_code
    callbackData = update.callback_query.data

    // Answer callback query to remove spinner
    await answerCallbackQuery(c.env.TELEGRAM_BOT_TOKEN, update.callback_query.id)
  } else {
    // Unsupported update type
    console.log('[TELEGRAM] Unsupported update type:', update)
    return c.json({ ok: true })
  }

  // Check whitelist (try DB first, fallback to env var for backward compatibility)
  let whitelisted = await isWhitelistedInDb(userId, 'telegram', c.env.DB)

  if (!whitelisted) {
    const whitelist = c.env.BOT_WHITELIST || ''
    whitelisted = isWhitelisted(userId, whitelist)
  }

  if (!whitelisted) {
    console.warn(`[TELEGRAM] User ${userId} not whitelisted`)
    const lang = detectLanguage(languageCode)
    await sendMessage(c.env.TELEGRAM_BOT_TOKEN, chatId, t('unauthorized', lang))
    await logCommandExecution(c.env, 'telegram', userId, commandText || callbackData || 'unknown', false, 'not_whitelisted')
    return c.json({ ok: true })
  }

  // Parse command
  const lang = detectLanguage(languageCode)
  let command

  if (commandText) {
    command = parseCommand(commandText)
  } else if (callbackData) {
    command = parseCallbackData(callbackData)
  } else {
    return c.json({ ok: true })
  }

  // Handle menu command specially to construct inline keyboard
  if (command.type === 'menu') {
    const menuText = t('menu_title', lang)
    const keyboard = {
      inline_keyboard: [
        [
          { text: t('menu_portfolio', lang), callback_data: 'query:portfolio' },
          { text: t('menu_status', lang), callback_data: 'query:status' }
        ],
        [
          { text: t('menu_campaigns', lang), callback_data: 'query:campaigns' },
          { text: t('menu_market', lang), callback_data: 'query:market' }
        ],
        [
          { text: t('menu_strategies', lang), callback_data: 'query:strategies' },
          { text: t('menu_alerts', lang), callback_data: 'query:alerts' }
        ],
        [
          { text: t('menu_risk', lang), callback_data: 'query:risk' },
          { text: t('menu_snapshot', lang), callback_data: 'query:snapshot' }
        ]
      ]
    }
    await sendMessage(c.env.TELEGRAM_BOT_TOKEN, chatId, menuText, keyboard)
    await logCommandExecution(c.env, 'telegram', userId, commandText || callbackData || 'menu', true)
    return c.json({ ok: true })
  }

  // Handle unknown command
  if (command.type === 'unknown') {
    await sendMessage(c.env.TELEGRAM_BOT_TOKEN, chatId, t('command_unknown', lang))
    await logCommandExecution(c.env, 'telegram', userId, command.raw, false, 'unknown_command')
    return c.json({ ok: true })
  }

  // Dispatch command
  try {
    // Create sendMessage wrapper for dispatcher
    const sendMessageWrapper = async (text: string, replyMarkup?: unknown) => {
      await sendMessage(c.env.TELEGRAM_BOT_TOKEN, chatId, text, replyMarkup)
    }

    await dispatchCommand(command, chatId.toString(), c.env, lang, sendMessageWrapper, 'telegram', userId)
    await logCommandExecution(c.env, 'telegram', userId, commandText || callbackData || 'unknown', true)
  } catch (error) {
    console.error('[TELEGRAM] Command dispatch error:', error)
    await sendMessage(c.env.TELEGRAM_BOT_TOKEN, chatId, t('error_generic', lang, { message: 'Internal error' }))
    await logCommandExecution(c.env, 'telegram', userId, commandText || callbackData || 'unknown', false, String(error))
  }

  return c.json({ ok: true })
})

/**
 * Send message to Telegram chat
 */
async function sendMessage(
  botToken: string,
  chatId: number,
  text: string,
  replyMarkup?: any
): Promise<void> {
  const url = `https://api.telegram.org/bot${botToken}/sendMessage`
  const body: any = {
    chat_id: chatId,
    text,
    parse_mode: 'HTML'
  }

  if (replyMarkup) {
    body.reply_markup = replyMarkup
  }

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  if (!response.ok) {
    const error = await response.text()
    console.error('[TELEGRAM] Send message failed:', error)
    throw new Error(`Failed to send message: ${error}`)
  }
}

/**
 * Answer callback query (removes spinner on button)
 */
async function answerCallbackQuery(botToken: string, callbackQueryId: string): Promise<void> {
  const url = `https://api.telegram.org/bot${botToken}/answerCallbackQuery`
  const body = {
    callback_query_id: callbackQueryId
  }

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  if (!response.ok) {
    const error = await response.text()
    console.error('[TELEGRAM] Answer callback query failed:', error)
    // Don't throw — this is not critical
  }
}

export { app as botTelegram }
