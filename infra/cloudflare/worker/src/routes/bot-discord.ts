/**
 * Discord Bot Webhook Handler
 * Handles incoming Discord interaction requests
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import { verifyDiscordSignature, isWhitelisted, isWhitelistedInDb } from '../bot/auth'
import { parseCommand, dispatchCommand, logCommandExecution } from '../bot/dispatcher'
import { detectLanguage, t } from '../bot/i18n'

const app = new Hono<{ Bindings: Env }>()

/**
 * Discord Interaction types
 */
enum InteractionType {
  PING = 1,
  APPLICATION_COMMAND = 2,
  MESSAGE_COMPONENT = 3,
  APPLICATION_COMMAND_AUTOCOMPLETE = 4,
  MODAL_SUBMIT = 5
}

/**
 * Discord Interaction response types
 */
enum InteractionResponseType {
  PONG = 1,
  CHANNEL_MESSAGE_WITH_SOURCE = 4,
  DEFERRED_CHANNEL_MESSAGE_WITH_SOURCE = 5,
  DEFERRED_UPDATE_MESSAGE = 6,
  UPDATE_MESSAGE = 7
}

/**
 * Discord Interaction structure
 */
interface DiscordInteraction {
  id: string
  application_id: string
  type: InteractionType
  data?: {
    id: string
    name: string
    options?: Array<{
      name: string
      value: string | number | boolean
    }>
  }
  guild_id?: string
  channel_id?: string
  member?: {
    user?: {
      id: string
      username: string
      locale?: string
    }
  }
  user?: {
    id: string
    username: string
    locale?: string
  }
  token: string
  version: 1
}

/**
 * POST /webhook/discord
 * Discord interactions endpoint
 */
app.post('/webhook/discord', async (c) => {
  const body = await c.req.text()
  const signature = c.req.header('X-Signature-Ed25519')
  const timestamp = c.req.header('X-Signature-Timestamp')

  // Verify signature
  const publicKey = c.env.DISCORD_PUBLIC_KEY
  if (!publicKey) {
    console.error('[DISCORD] Public key not configured')
    return c.json({ error: 'bot_not_configured' }, 500)
  }

  const isValid = await verifyDiscordSignature(body, signature || null, timestamp || null, publicKey)
  if (!isValid) {
    console.warn('[DISCORD] Invalid signature')
    return c.json({ error: 'invalid signature' }, 401)
  }

  // Parse interaction
  let interaction: DiscordInteraction
  try {
    interaction = JSON.parse(body)
  } catch (error) {
    console.error('[DISCORD] Invalid JSON:', error)
    return c.json({ error: 'invalid_json' }, 400)
  }

  // Handle PING (required for Discord verification)
  if (interaction.type === InteractionType.PING) {
    return c.json({ type: InteractionResponseType.PONG })
  }

  // Extract user ID
  const user = interaction.member?.user || interaction.user
  if (!user) {
    console.error('[DISCORD] No user in interaction')
    return c.json({ error: 'no_user' }, 400)
  }

  const userId = user.id
  const languageCode = user.locale

  // Check whitelist (try DB first, fallback to env var for backward compatibility)
  let whitelisted = await isWhitelistedInDb(userId, 'discord', c.env.DB)

  if (!whitelisted) {
    const whitelist = c.env.BOT_WHITELIST || ''
    whitelisted = isWhitelisted(userId, whitelist)
  }

  if (!whitelisted) {
    console.warn(`[DISCORD] User ${userId} not whitelisted`)
    const lang = detectLanguage(languageCode)
    await logCommandExecution(
      c.env,
      'discord',
      userId,
      interaction.data?.name || 'unknown',
      false,
      'not_whitelisted'
    )
    return c.json({
      type: InteractionResponseType.CHANNEL_MESSAGE_WITH_SOURCE,
      data: {
        content: t('unauthorized', lang),
        flags: 64 // Ephemeral message (only visible to user)
      }
    })
  }

  // Handle application command
  if (interaction.type === InteractionType.APPLICATION_COMMAND) {
    const commandName = interaction.data?.name || ''
    const lang = detectLanguage(languageCode)

    // Parse command from slash command name
    const command = parseCommand(`/${commandName}`)

    // Handle menu command
    if (command.type === 'menu') {
      const menuText = t('menu_title', lang)
      const components = [
        {
          type: 1, // Action row
          components: [
            {
              type: 2, // Button
              style: 1, // Primary
              label: t('menu_portfolio', lang),
              custom_id: 'query:portfolio'
            },
            {
              type: 2,
              style: 1,
              label: t('menu_status', lang),
              custom_id: 'query:status'
            }
          ]
        },
        {
          type: 1,
          components: [
            {
              type: 2,
              style: 1,
              label: t('menu_campaigns', lang),
              custom_id: 'query:campaigns'
            },
            {
              type: 2,
              style: 1,
              label: t('menu_market', lang),
              custom_id: 'query:market'
            }
          ]
        }
      ]

      await logCommandExecution(c.env, 'discord', userId, commandName, true)

      return c.json({
        type: InteractionResponseType.CHANNEL_MESSAGE_WITH_SOURCE,
        data: {
          content: menuText,
          components
        }
      })
    }

    // Handle snapshot command (requires deferred response)
    if (command.type === 'query' && command.query === 'snapshot') {
      // Send deferred response immediately
      const response = c.json({
        type: InteractionResponseType.DEFERRED_CHANNEL_MESSAGE_WITH_SOURCE
      })

      // Process snapshot in background using executionCtx.waitUntil
      c.executionCtx.waitUntil(
        (async () => {
          try {
            console.log(`[DISCORD] Processing snapshot for user ${userId}`)

            // Create sendMessage wrapper for dispatcher
            const sendMessageWrapper = async (text: string) => {
              await sendFollowupMessage(
                c.env.DISCORD_BOT_TOKEN,
                interaction.application_id,
                interaction.token,
                text
              )
            }

            await dispatchCommand(command, interaction.channel_id || userId, c.env, lang, sendMessageWrapper, 'discord', userId)

            await logCommandExecution(c.env, 'discord', userId, commandName, true)
          } catch (error) {
            console.error('[DISCORD] Snapshot error:', error)
            await sendFollowupMessage(
              c.env.DISCORD_BOT_TOKEN,
              interaction.application_id,
              interaction.token,
              t('error_generic', lang, { message: 'Snapshot failed' })
            )
            await logCommandExecution(c.env, 'discord', userId, commandName, false, String(error))
          }
        })()
      )

      return response
    }

    // Handle unknown command
    if (command.type === 'unknown') {
      await logCommandExecution(c.env, 'discord', userId, commandName, false, 'unknown_command')
      return c.json({
        type: InteractionResponseType.CHANNEL_MESSAGE_WITH_SOURCE,
        data: {
          content: t('command_unknown', lang),
          flags: 64 // Ephemeral
        }
      })
    }

    // Handle other commands (immediate response)
    try {
      // Create sendMessage wrapper for dispatcher
      const sendMessageWrapper = async (text: string) => {
        // Note: For immediate commands, we can't send messages directly from dispatcher
        // The response is sent via the return value below
        console.log(`[DISCORD] Would send: ${text}`)
      }

      await dispatchCommand(command, interaction.channel_id || userId, c.env, lang, sendMessageWrapper, 'discord', userId)
      await logCommandExecution(c.env, 'discord', userId, commandName, true)

      return c.json({
        type: InteractionResponseType.CHANNEL_MESSAGE_WITH_SOURCE,
        data: {
          content: t('command_processing', lang)
        }
      })
    } catch (error) {
      console.error('[DISCORD] Command error:', error)
      await logCommandExecution(c.env, 'discord', userId, commandName, false, String(error))
      return c.json({
        type: InteractionResponseType.CHANNEL_MESSAGE_WITH_SOURCE,
        data: {
          content: t('error_generic', lang, { message: 'Command failed' }),
          flags: 64
        }
      })
    }
  }

  // Unsupported interaction type
  console.log('[DISCORD] Unsupported interaction type:', interaction.type)
  return c.json({ error: 'unsupported_type' }, 400)
})

/**
 * Send followup message to Discord interaction
 */
async function sendFollowupMessage(
  botToken: string,
  applicationId: string,
  interactionToken: string,
  content: string
): Promise<void> {
  const url = `https://discord.com/api/v10/webhooks/${applicationId}/${interactionToken}`
  const body = { content }

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  if (!response.ok) {
    const error = await response.text()
    console.error('[DISCORD] Send followup failed:', error)
    throw new Error(`Failed to send followup: ${error}`)
  }
}

export { app as botDiscord }
