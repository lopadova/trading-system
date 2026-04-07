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
        -- Add Greeks columns to active_positions table
        ALTER TABLE active_positions ADD COLUMN delta REAL NULL;
        ALTER TABLE active_positions ADD COLUMN gamma REAL NULL;
        ALTER TABLE active_positions ADD COLUMN theta REAL NULL;
        ALTER TABLE active_positions ADD COLUMN vega REAL NULL;
        ALTER TABLE active_positions ADD COLUMN implied_volatility REAL NULL;
        ALTER TABLE active_positions ADD COLUMN greeks_updated_at TEXT NULL;
        ALTER TABLE active_positions ADD COLUMN underlying_price REAL NULL;

        -- Index for querying positions by Greeks values (e.g., high gamma positions)
        CREATE INDEX IF NOT EXISTS idx_active_positions_delta
            ON active_positions(delta)
            WHERE delta IS NOT NULL;

        CREATE INDEX IF NOT EXISTS idx_active_positions_gamma
            ON active_positions(gamma)
            WHERE gamma IS NOT NULL;
        """;
}
