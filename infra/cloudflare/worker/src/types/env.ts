/**
 * Cloudflare Worker Environment Bindings
 * Type definitions for D1 database and environment variables
 */

export interface Env {
  // D1 Database binding
  DB: D1Database

  // Analytics Engine dataset (Phase 7.3 observability) — optional because
  // local `wrangler dev` without the binding still needs to run.
  METRICS?: AnalyticsEngineDataset

  // Environment variables
  DASHBOARD_ORIGIN: string

  // Secrets (set via wrangler secret put)
  API_KEY: string
  ANTHROPIC_API_KEY: string
  TELEGRAM_BOT_TOKEN: string
  DISCORD_BOT_TOKEN: string
  DISCORD_PUBLIC_KEY: string
  DISCORD_CHANNEL_ID: string
  BOT_WHITELIST: string
}
