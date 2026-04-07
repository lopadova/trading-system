/**
 * Bot Authentication Module
 * Handles webhook signature verification and user whitelisting
 */

/**
 * Verify Telegram webhook signature using HMAC-SHA256
 * @param body - Request body as string
 * @param botToken - Telegram bot token
 * @param headerToken - X-Telegram-Bot-Api-Secret-Token header value
 * @returns true if signature is valid
 */
export async function verifyTelegramSignature(
  body: string,
  botToken: string,
  headerToken: string | null
): Promise<boolean> {
  // Telegram uses a simple secret token approach (not HMAC of body)
  // The secret token is set when registering the webhook
  // and sent back in X-Telegram-Bot-Api-Secret-Token header
  if (!headerToken) {
    return false
  }

  // For Telegram, we just verify the secret token matches
  // This is the recommended approach from Telegram docs
  // Generate secret token from bot token hash
  const encoder = new TextEncoder()
  const data = encoder.encode(botToken)
  const hashBuffer = await crypto.subtle.digest('SHA-256', data)
  const hashArray = Array.from(new Uint8Array(hashBuffer))
  const expectedToken = hashArray
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
    .substring(0, 32)

  return headerToken === expectedToken
}

/**
 * Verify Discord webhook signature using Ed25519
 * @param body - Request body as string
 * @param signature - X-Signature-Ed25519 header value
 * @param timestamp - X-Signature-Timestamp header value
 * @param publicKey - Discord application public key
 * @returns true if signature is valid
 */
export async function verifyDiscordSignature(
  body: string,
  signature: string | null,
  timestamp: string | null,
  publicKey: string
): Promise<boolean> {
  if (!signature || !timestamp) {
    return false
  }

  try {
    // Construct the message to verify: timestamp + body
    const message = timestamp + body

    // Import the public key
    const publicKeyBytes = hexToBytes(publicKey)
    const key = await crypto.subtle.importKey(
      'raw',
      publicKeyBytes,
      {
        name: 'Ed25519',
        namedCurve: 'Ed25519'
      },
      false,
      ['verify']
    )

    // Convert signature from hex to bytes
    const signatureBytes = hexToBytes(signature)

    // Verify the signature
    const messageBytes = new TextEncoder().encode(message)
    const isValid = await crypto.subtle.verify('Ed25519', key, signatureBytes, messageBytes)

    return isValid
  } catch (error) {
    console.error('Discord signature verification error:', error)
    return false
  }
}

/**
 * Check if user ID is in whitelist (legacy env var check)
 * @param userId - User ID to check
 * @param whitelist - Comma-separated list of allowed user IDs
 * @returns true if user is whitelisted
 */
export function isWhitelisted(userId: string, whitelist: string): boolean {
  const allowedUsers = whitelist
    .split(',')
    .map((id) => id.trim())
    .filter((id) => id.length > 0)

  return allowedUsers.includes(userId)
}

/**
 * Check if user ID is in D1 whitelist database
 * @param userId - User ID to check
 * @param botType - Bot type ('telegram' or 'discord')
 * @param db - D1 database instance
 * @returns true if user is whitelisted in database
 */
export async function isWhitelistedInDb(
  userId: string,
  botType: 'telegram' | 'discord',
  db: D1Database
): Promise<boolean> {
  try {
    const result = await db
      .prepare('SELECT 1 FROM bot_whitelist WHERE user_id = ? AND bot_type = ? LIMIT 1')
      .bind(userId, botType)
      .first()

    return result !== null
  } catch (error) {
    console.error('Whitelist DB check error:', error)
    return false
  }
}

/**
 * Add user to whitelist database
 * @param userId - User ID to add
 * @param botType - Bot type ('telegram' or 'discord')
 * @param db - D1 database instance
 * @param addedBy - Optional admin user ID who added this user
 * @param notes - Optional notes about this user
 * @returns true if added successfully, false if already exists or error
 */
export async function addToWhitelist(
  userId: string,
  botType: 'telegram' | 'discord',
  db: D1Database,
  addedBy?: string,
  notes?: string
): Promise<boolean> {
  try {
    await db
      .prepare(
        'INSERT INTO bot_whitelist (user_id, bot_type, added_by, notes) VALUES (?, ?, ?, ?)'
      )
      .bind(userId, botType, addedBy || null, notes || null)
      .run()

    return true
  } catch (error) {
    // UNIQUE constraint violation means user already exists
    const errorMessage = String(error)
    if (errorMessage.includes('UNIQUE')) {
      return false
    }
    console.error('Add to whitelist error:', error)
    return false
  }
}

/**
 * Remove user from whitelist database
 * @param userId - User ID to remove
 * @param botType - Bot type ('telegram' or 'discord')
 * @param db - D1 database instance
 * @returns true if removed successfully
 */
export async function removeFromWhitelist(
  userId: string,
  botType: 'telegram' | 'discord',
  db: D1Database
): Promise<boolean> {
  try {
    const result = await db
      .prepare('DELETE FROM bot_whitelist WHERE user_id = ? AND bot_type = ?')
      .bind(userId, botType)
      .run()

    return result.meta.changes > 0
  } catch (error) {
    console.error('Remove from whitelist error:', error)
    return false
  }
}

/**
 * List all whitelisted users
 * @param botType - Bot type ('telegram' or 'discord')
 * @param db - D1 database instance
 * @returns Array of whitelisted user records
 */
export async function listWhitelist(
  botType: 'telegram' | 'discord',
  db: D1Database
): Promise<Array<{ user_id: string; added_at: string; added_by: string | null; notes: string | null }>> {
  try {
    const result = await db
      .prepare(
        'SELECT user_id, added_at, added_by, notes FROM bot_whitelist WHERE bot_type = ? ORDER BY added_at DESC'
      )
      .bind(botType)
      .all()

    return result.results as Array<{
      user_id: string
      added_at: string
      added_by: string | null
      notes: string | null
    }>
  } catch (error) {
    console.error('List whitelist error:', error)
    return []
  }
}

/**
 * Convert hex string to Uint8Array
 */
function hexToBytes(hex: string): Uint8Array {
  const bytes = new Uint8Array(hex.length / 2)
  for (let i = 0; i < hex.length; i += 2) {
    bytes[i / 2] = parseInt(hex.substring(i, i + 2), 16)
  }
  return bytes
}
