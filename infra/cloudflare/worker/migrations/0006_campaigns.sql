-- Migration: campaigns table
-- Purpose: dashboard needs to surface campaign name alongside active positions.
-- The supervisor/execution services already track campaign_id on positions; this
-- table lets the Worker LEFT JOIN to a human-readable label.
-- Date: 2026-04-21

CREATE TABLE IF NOT EXISTS campaigns (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    state TEXT NOT NULL CHECK(state IN ('draft', 'active', 'paused', 'closed')),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_campaigns_state
ON campaigns(state);
