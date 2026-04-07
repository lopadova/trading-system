/**
 * EasyLanguage to SDF v1 Conversion API
 * Endpoint that uses Claude API to convert EL code to SDF v1 format
 */

import { Hono } from 'hono'
import Anthropic from '@anthropic-ai/sdk'
import type { Env } from '../types/env'
import { EL_CONVERTER_SYSTEM_PROMPT } from '../prompts/el-converter-system'

const app = new Hono<{ Bindings: Env }>()

// ============================================================================
// TYPE DEFINITIONS
// ============================================================================

interface ConvertRequest {
  easylanguage_code: string
  user_notes?: string
}

interface ConversionIssue {
  type: 'not_supported' | 'ambiguous' | 'manual_required'
  el_construct: string
  description: string
  suggestion: string
}

interface ConvertResponse {
  convertible: boolean | 'partial'
  confidence: number
  result_json: Record<string, unknown> | null
  issues: ConversionIssue[]
  warnings: string[]
  notes: string
}

// ============================================================================
// ENDPOINT IMPLEMENTATION
// ============================================================================

/**
 * POST /api/v1/strategies/convert-el
 * Converts EasyLanguage code to SDF v1 format using Claude API
 *
 * Request body:
 * {
 *   "easylanguage_code": "string (required, max 50k chars)",
 *   "user_notes": "string (optional)"
 * }
 *
 * Response:
 * {
 *   "convertible": true | false | "partial",
 *   "confidence": 0.0-1.0,
 *   "result_json": {...} | null,
 *   "issues": [...],
 *   "warnings": [...],
 *   "notes": "string"
 * }
 */
app.post('/convert-el', async (c) => {
  try {
    // Validate request body
    const body = await c.req.json<ConvertRequest>().catch(() => null)

    if (!body || !body.easylanguage_code?.trim()) {
      return c.json({ error: 'easylanguage_code required' }, 400)
    }

    // Check code size limit (50k chars max)
    if (body.easylanguage_code.length > 50_000) {
      return c.json({ error: 'Code too large (max 50,000 chars)' }, 413)
    }

    // Check API key availability
    const apiKey = c.env.ANTHROPIC_API_KEY
    if (!apiKey) {
      console.error('ANTHROPIC_API_KEY not configured')
      return c.json({
        error: 'AI conversion not available',
        message: 'Anthropic API key not configured. Feature disabled.'
      }, 503)
    }

    // Initialize Anthropic client
    const anthropic = new Anthropic({ apiKey })

    // Call Claude API
    console.log('Calling Claude API for EL conversion...')
    const startTime = Date.now()

    const message = await anthropic.messages.create({
      model: 'claude-sonnet-4-5',
      max_tokens: 4096,
      system: EL_CONVERTER_SYSTEM_PROMPT,
      messages: [
        {
          role: 'user',
          content: `Converti questo codice EasyLanguage in SDF v1:\n\n\`\`\`\n${body.easylanguage_code}\n\`\`\``
        }
      ]
    })

    const elapsedMs = Date.now() - startTime
    console.log(`Claude API responded in ${elapsedMs}ms`)

    // Extract JSON from response
    const textContent = message.content
      .filter(block => block.type === 'text')
      .map(block => block.type === 'text' ? block.text : '')
      .join('')

    // Remove markdown code fence if present
    const jsonMatch = textContent.match(/```json\s*\n?([\s\S]*?)\n?```/) ||
                      textContent.match(/\{[\s\S]*\}/)

    if (!jsonMatch) {
      console.error('No JSON found in Claude response:', textContent)
      return c.json({
        error: 'unparseable_response',
        message: 'Claude API response not parseable as JSON'
      }, 500)
    }

    const jsonText = jsonMatch[1] || jsonMatch[0]
    const result: ConvertResponse = JSON.parse(jsonText)

    // Validate response schema
    if (typeof result.convertible === 'undefined' ||
        typeof result.confidence !== 'number' ||
        !Array.isArray(result.issues)) {
      console.error('Invalid response schema from Claude:', result)
      return c.json({
        error: 'invalid_response_schema',
        message: 'Claude response missing required fields'
      }, 500)
    }

    // Log conversion to D1
    try {
      await c.env.DB.prepare(`
        INSERT INTO el_conversion_log (
          id, easylanguage_code, convertible, confidence,
          result_json, issues_count, elapsed_ms, created_at
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
      `).bind(
        crypto.randomUUID(),
        body.easylanguage_code.substring(0, 10000), // Max 10k chars for log
        result.convertible.toString(),
        result.confidence,
        result.result_json ? JSON.stringify(result.result_json) : null,
        result.issues.length,
        elapsedMs,
        new Date().toISOString()
      ).run()
    } catch (err) {
      console.error('Failed to log conversion to D1:', err)
      // Don't fail the request if logging fails
      // This satisfies TEST-SW-07b-09 (graceful degradation)
    }

    // Return successful conversion result
    return c.json(result)

  } catch (error) {
    console.error('Error in /convert-el:', error)

    // Handle Anthropic API errors specifically
    if (error instanceof Anthropic.APIError) {
      return c.json({
        error: 'anthropic_api_error',
        message: error.message,
        status: error.status
      }, error.status || 500)
    }

    // Generic error response
    return c.json({
      error: 'internal_error',
      message: 'An error occurred during conversion'
    }, 500)
  }
})

export { app as strategiesConvert }
