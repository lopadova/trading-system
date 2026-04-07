-- Migration 0002: EL Conversion Log Table
-- Stores conversion history for EasyLanguage → SDF v1 via Claude API

CREATE TABLE IF NOT EXISTS el_conversion_log (
  id TEXT PRIMARY KEY,
  easylanguage_code TEXT NOT NULL,
  convertible TEXT NOT NULL, -- "true" | "false" | "partial"
  confidence REAL NOT NULL,
  result_json TEXT,
  issues_count INTEGER NOT NULL DEFAULT 0,
  elapsed_ms INTEGER NOT NULL,
  created_at TEXT NOT NULL
);

-- Index on created_at for chronological queries
CREATE INDEX idx_el_conversion_created ON el_conversion_log(created_at DESC);

-- Index on convertible for filtering by conversion status
CREATE INDEX idx_el_conversion_convertible ON el_conversion_log(convertible);
