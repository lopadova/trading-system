using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Tests.Helpers;
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

    [Fact(DisplayName = "TEST-22-11: All supervisor migrations apply successfully")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-11")]
    public async Task TEST_22_11_AllSupervisorMigrationsApplySuccessfully()
    {
        // Arrange: Migrations from SupervisorMigrations.All
        IMigration[] migrations = SupervisorMigrations.All;

        // Act: Run all migrations
        await _runner.RunAsync(migrations, CancellationToken.None);

        // Assert: Verify migrations table exists and has correct count
        await using SqliteConnection conn = await _factory.OpenAsync();
        int count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM _migrations WHERE name LIKE '001_%' OR name LIKE '002_%'");

        Assert.True(count >= 2, $"Expected at least 2 migrations applied, found {count}");
    }

    [Fact(DisplayName = "TEST-22-12: Migration 001 creates heartbeats table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-12")]
    public async Task TEST_22_12_Migration001CreatesHeartbeatsTable()
    {
        // Arrange & Act
        await _runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Assert: Verify heartbeats table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='heartbeats'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        var columns = await conn.QueryAsync<string>("PRAGMA table_info(heartbeats)");
        List<string> columnNames = columns.Select(c => c).ToList();

        // heartbeats should have: id, hostname, timestamp_utc, uptime_seconds, cpu_percent, ram_percent, disk_free_gb, trading_mode
        Assert.Contains("hostname", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("timestamp_utc", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("cpu_percent", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ram_percent", columnNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "TEST-22-13: Migration 001 creates outbox table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-13")]
    public async Task TEST_22_13_Migration001CreatesOutboxTable()
    {
        // Arrange & Act
        await _runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Assert: Verify outbox table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='outbox'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        var columns = await conn.QueryAsync<string>("PRAGMA table_info(outbox)");
        List<string> columnNames = columns.Select(c => c).ToList();

        Assert.Contains("event_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("event_type", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("payload", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("created_at", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("synced", columnNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "TEST-22-14: Migration 001 creates alerts table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-14")]
    public async Task TEST_22_14_Migration001CreatesAlertsTable()
    {
        // Arrange & Act
        await _runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Assert: Verify alerts table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='alerts'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        var columns = await conn.QueryAsync<string>("PRAGMA table_info(alerts)");
        List<string> columnNames = columns.Select(c => c).ToList();

        Assert.Contains("alert_id", columnNames, StringComparer.OrdinalIgnoreCase);
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
        var columns = await conn.QueryAsync<string>("PRAGMA table_info(ivts_snapshots)");
        List<string> columnNames = columns.Select(c => c).ToList();

        Assert.Contains("snapshot_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("timestamp_utc", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("underlying_symbol", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("strike", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("implied_volatility", columnNames, StringComparer.OrdinalIgnoreCase);
    }
}
