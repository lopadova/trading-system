using SharedKernel.Data;

namespace OptionsExecutionService.Migrations;

/// <summary>
/// Migration 002: Add Greeks columns to active_positions table.
/// Stores Delta, Gamma, Theta, Vega for real-time position management and risk analysis.
/// </summary>
public sealed class AddGreeksColumns002 : IMigration
{
    public int Version => 2;
    public string Name => "AddGreeksColumns";

    public string UpSql => """
        -- Add Greeks columns to positions table
        ALTER TABLE positions ADD COLUMN delta REAL NULL;
        ALTER TABLE positions ADD COLUMN gamma REAL NULL;
        ALTER TABLE positions ADD COLUMN theta REAL NULL;
        ALTER TABLE positions ADD COLUMN vega REAL NULL;
        ALTER TABLE positions ADD COLUMN implied_volatility REAL NULL;
        ALTER TABLE positions ADD COLUMN greeks_updated_at TEXT NULL;
        ALTER TABLE positions ADD COLUMN underlying_price REAL NULL;

        -- Index for querying positions by Greeks values (e.g., high gamma positions)
        CREATE INDEX IF NOT EXISTS idx_positions_delta
            ON positions(delta)
            WHERE delta IS NOT NULL;

        CREATE INDEX IF NOT EXISTS idx_positions_gamma
            ON positions(gamma)
            WHERE gamma IS NOT NULL;
        """;
}
