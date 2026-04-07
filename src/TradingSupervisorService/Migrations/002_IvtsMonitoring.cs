using Dapper;
using Microsoft.Data.Sqlite;
using SharedKernel.Data;

namespace TradingSupervisorService.Migrations;

/// <summary>
/// Migration 002: Add IVTS (Implied Volatility Term Structure) monitoring tables.
/// Creates ivts_snapshots table for storing IV data and analytics.
/// Reuses existing alert_history table for IVTS alerts.
/// </summary>
public sealed class Migration002_IvtsMonitoring : IMigration
{
    public int Version => 2;
    public string Name => "IvtsMonitoring";

    public string UpSql => """
        CREATE TABLE IF NOT EXISTS ivts_snapshots (
            snapshot_id         TEXT    PRIMARY KEY,
            symbol              TEXT    NOT NULL,
            timestamp_utc       TEXT    NOT NULL,
            iv_30d              REAL    NOT NULL,
            iv_60d              REAL    NOT NULL,
            iv_90d              REAL    NOT NULL,
            iv_120d             REAL    NOT NULL,
            ivr_percentile      REAL,
            term_structure_slope REAL   NOT NULL,
            is_inverted         INTEGER NOT NULL DEFAULT 0,
            iv_min_52w          REAL,
            iv_max_52w          REAL,
            created_at          TEXT    NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_ivts_symbol_timestamp
        ON ivts_snapshots (symbol, timestamp_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_ivts_timestamp
        ON ivts_snapshots (timestamp_utc DESC);

        CREATE INDEX IF NOT EXISTS idx_ivts_ivr
        ON ivts_snapshots (symbol, ivr_percentile DESC)
        WHERE ivr_percentile IS NOT NULL;
        """;
}
