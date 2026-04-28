using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Tests.Data;
using OptionsExecutionService.Migrations;
using Xunit;

namespace OptionsExecutionService.Tests.Migrations;

/// <summary>
/// Integration tests for OptionsExecutionService database migrations.
/// Verifies all migrations apply successfully and create correct schema.
/// TEST-22-16 through TEST-22-20
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

    [Fact(DisplayName = "TEST-22-16: All options execution migrations apply successfully")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-16")]
    public async Task TEST_22_16_AllOptionsMigrationsApplySuccessfully()
    {
        // Arrange: Migrations from OptionsMigrations.All
        IReadOnlyList<IMigration> migrations = OptionsMigrations.All;

        // Act: Run all migrations
        await _runner.RunAsync(migrations, CancellationToken.None);

        // Assert: Verify migrations table exists and has correct count
        await using SqliteConnection conn = await _factory.OpenAsync();
        int count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM schema_migrations");

        // Phase 1: migration 005 (AddOrderEvents) brings the count to 5.
        Assert.Equal(5, count);
    }

    [Fact(DisplayName = "TEST-22-17: Migration 001 creates campaigns table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-17")]
    public async Task TEST_22_17_Migration001CreatesCampaignsTable()
    {
        // Arrange & Act
        await _runner.RunAsync(OptionsMigrations.All, CancellationToken.None);

        // Assert: Verify campaigns table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='campaigns'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        var columns = await conn.QueryAsync<dynamic>("PRAGMA table_info(campaigns)");
        List<string> columnNames = columns.Select(c => (string)c.name).ToList();

        Assert.Contains("campaign_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("strategy_name", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("status", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("created_at", columnNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "TEST-22-18: Migration 001 creates positions table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-18")]
    public async Task TEST_22_18_Migration001CreatesPositionsTable()
    {
        // Arrange & Act
        await _runner.RunAsync(OptionsMigrations.All, CancellationToken.None);

        // Assert: Verify positions table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='positions'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        var columns = await conn.QueryAsync<dynamic>("PRAGMA table_info(positions)");
        List<string> columnNames = columns.Select(c => (string)c.name).ToList();

        Assert.Contains("position_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("campaign_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("contract_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("quantity", columnNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "TEST-22-19: Migration 002 adds greeks columns to positions")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-19")]
    public async Task TEST_22_19_Migration002AddsGreeksColumns()
    {
        // Arrange & Act
        await _runner.RunAsync(OptionsMigrations.All, CancellationToken.None);

        // Assert: Verify greeks columns exist
        await using SqliteConnection conn = await _factory.OpenAsync();
        var columns = await conn.QueryAsync<dynamic>("PRAGMA table_info(positions)");
        List<string> columnNames = columns.Select(c => (string)c.name).ToList();

        Assert.Contains("delta", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("gamma", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("theta", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("vega", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("implied_volatility", columnNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "TEST-22-20: Migration 003 creates order_tracking table")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-20")]
    public async Task TEST_22_20_Migration003CreatesOrderTrackingTable()
    {
        // Arrange & Act
        await _runner.RunAsync(OptionsMigrations.All, CancellationToken.None);

        // Assert: Verify order_tracking table exists
        await using SqliteConnection conn = await _factory.OpenAsync();
        int tableExists = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='order_tracking'");

        Assert.Equal(1, tableExists);

        // Verify table structure
        var columns = await conn.QueryAsync<dynamic>("PRAGMA table_info(order_tracking)");
        List<string> columnNames = columns.Select(c => (string)c.name).ToList();

        Assert.Contains("order_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ibkr_order_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("campaign_id", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("status", columnNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("created_at", columnNames, StringComparer.OrdinalIgnoreCase);
    }
}
