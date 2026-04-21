using SharedKernel.Data;

namespace TradingSupervisorService.Migrations;

/// <summary>
/// Migration 004 (Phase 7.3): Adds disk_total_gb and network_kbps columns to service_heartbeats.
/// <para>
/// Rationale: the heartbeat payload already ships these to the Worker (D1 migration 0008 adds
/// matching columns). We mirror the schema locally so that if the sync is down, operators can
/// still see full heartbeat history by querying supervisor.db directly.
/// </para>
/// <para>
/// Both columns are nullable — collectors may fail to resolve drive/network stats on
/// locked-down hosts or in tests, and we never block a heartbeat on an ancillary metric.
/// </para>
/// </summary>
public sealed class Migration004_HeartbeatDiskNetworkCols : IMigration
{
    public int Version => 4;
    public string Name => "HeartbeatDiskNetworkCols";

    public string UpSql => """
        -- Add disk_total_gb and network_kbps to service_heartbeats.
        -- Nullable so we can ingest legacy rows without triggering NOT NULL errors.
        ALTER TABLE service_heartbeats ADD COLUMN disk_total_gb REAL;
        ALTER TABLE service_heartbeats ADD COLUMN network_kbps  REAL;
        """;
}
