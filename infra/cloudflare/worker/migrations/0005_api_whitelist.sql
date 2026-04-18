-- Migration 0005: API Key Whitelist Table
-- Stores API keys for Worker endpoint authentication
-- Used by Dashboard, Windows Services, and other authorized clients

CREATE TABLE IF NOT EXISTS whitelist (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  api_key TEXT NOT NULL UNIQUE,
  description TEXT NOT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  last_used_at TEXT,
  active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0, 1))
);

-- Index for quick API key validation
CREATE INDEX IF NOT EXISTS idx_whitelist_api_key ON whitelist(api_key) WHERE active = 1;

-- Index for monitoring usage
CREATE INDEX IF NOT EXISTS idx_whitelist_last_used ON whitelist(last_used_at);
