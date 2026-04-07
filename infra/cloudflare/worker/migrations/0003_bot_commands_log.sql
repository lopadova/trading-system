-- Migration 0003: Bot Commands Log
-- Tracks all bot command executions for audit and debugging

CREATE TABLE IF NOT EXISTS bot_command_log (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  executed_at TEXT NOT NULL DEFAULT (datetime('now')),
  bot_type TEXT NOT NULL,
  user_id TEXT NOT NULL,
  command TEXT NOT NULL,
  response_ok INTEGER NOT NULL DEFAULT 0,
  error TEXT
);

CREATE INDEX IF NOT EXISTS idx_bot_log_user ON bot_command_log(user_id, executed_at DESC);
CREATE INDEX IF NOT EXISTS idx_bot_log_executed ON bot_command_log(executed_at DESC);
