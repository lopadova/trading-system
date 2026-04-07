using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Tests.Data;
using TradingSupervisorService.Migrations;
using Xunit;

namespace TradingSupervisorService.Tests.Migrations;

/// <summary>
/// Integration tests for TradingSupervisorService database migrations.
/// Verifies all migrations apply successfully and create correct schema.
/// TEST-22-11 through TEST-22-15
/// </summary>
public sealed class MigrationIntegrationTests : IAsyncLifetime
{
    private InMemoryConnectionFactory _factory = default!;
    private MigrationRunner _runner = default!;

    public async Task InitializeAsync()
    {
        _factory = new InMemoryConnectionFactory();
        _runner = new MigrationRunner(_factory, NullLogger<MigrationRunner>.Instance);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    // Helper to extract column names from PRAGMA table_info()
    private static async Task<List<string>> GetColumnNamesAsync(SqliteConnection conn, string tableName)
    {
        var result = await conn.QueryAsync("PRAGMA table_info(" + tableName + ")");
        return result.Select(row => (string)row.name).ToList();
    }

    [Fact(DisplayName = "TEST-22-11: All supervisor migrations apply successfully")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-11")]
    public async Task TEST_22_11_AllSupervisorMigrationsApplySuccessfully()
    {
        // Arrange: Migrations from SupervisorMigrations.All
        IReadOnlyList<IMigration> migrations = SupervisorMigrations.All;

        // Act: Run all migrations
        await _runner.RunAsync(migrations, CancellationToken.None);

        // Assert: Verify migrations table exists and has correct count
        await using SqliteConnection conn = await _factory.OpenAsync();
        int count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM schema_migrations");

        Assert.True(count >= 2, $"Expected at least 2 migrations applied, found {count}");
    }

    [Fact(DisplayName = "TEST-22-12: Migration 001 creates service_heartbeats table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-12")]
    public async Task TEST_22_12_Migration001CreatesHeartbeatsTable()
    {
        // Arrange & Act
        await _runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Assert: Verify service_heartbeats table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='service_heartbeats'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        List<string> columnNames = await GetColumnNamesAsync(conn, "service_heartbeats");

        // service_heartbeats should have: service_name, hostname, last_seen_at, uptime_seconds, cpu_percent, ram_percent, disk_free_gb, trading_mode
        Assert.Contains("service_name", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("hostname", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("last_seen_at", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("cpu_percent", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ram_percent", columnNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "TEST-22-13: Migration 001 creates sync_outbox table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-13")]
    public async Task TEST_22_13_Migration001CreatesOutboxTable()
    {
        // Arrange & Act
        await _runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Assert: Verify sync_outbox table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='sync_outbox'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        List<string> columnNames = await GetColumnNamesAsync(conn, "sync_outbox");

        Assert.Contains("event_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("event_type", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("payload_json", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("created_at", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("status", columnNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "TEST-22-14: Migration 001 creates alert_history table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-14")]
    public async Task TEST_22_14_Migration001CreatesAlertsTable()
    {
        // Arrange & Act
        await _runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Assert: Verify alert_history table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='alert_history'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        List<string> columnNames = await GetColumnNamesAsync(conn, "alert_history");

        Assert.Contains("alert_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("alert_type", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("severity", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("message", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("created_at", columnNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "TEST-22-15: Migration 002 creates ivts_snapshots table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-15")]
    public async Task TEST_22_15_Migration002CreatesIvtsSnapshotsTable()
    {
        // Arrange & Act
        await _runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Assert: Verify ivts_snapshots table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ivts_snapshots'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        List<string> columnNames = await GetColumnNamesAsync(conn, "ivts_snapshots");

        Assert.Contains("snapshot_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("symbol", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("timestamp_utc", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("iv_30d", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("iv_60d", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("term_structure_slope", columnNames, StringComparer.OrdinalIgnoreCase);
    }
}
