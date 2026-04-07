/**
 * Cloudflare Worker Environment Bindings
 * Type definitions for D1 database and environment variables
 */

export interface Env {
  // D1 Database binding
  DB: D1Database

  // Environment variables
  DASHBOARD_ORIGIN: string

  // Secrets (set via wrangler secret put)
  API_KEY: string
}
