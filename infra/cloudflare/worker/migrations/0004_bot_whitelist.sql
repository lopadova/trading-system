-- Migration 0004: Bot Whitelist Table
-- Stores whitelisted users for bot access control

CREATE TABLE IF NOT EXISTS bot_whitelist (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id TEXT NOT NULL UNIQUE,
  bot_type TEXT NOT NULL CHECK(bot_type IN ('telegram', 'discord')),
  added_at TEXT NOT NULL DEFAULT (datetime('now')),
  added_by TEXT,
  notes TEXT
);

-- Index for quick lookup during auth
CREATE INDEX IF NOT EXISTS idx_bot_whitelist_user ON bot_whitelist(user_id, bot_type);
