using Xunit;
using Microsoft.Data.Sqlite;
using Dapper;
using TradingSystem.SharedKernel.Data;
using TradingSystem.TradingSupervisorService.Data;
using TradingSystem.OptionsExecutionService.Data;

namespace TradingSystem.E2E.Automated;

/// <summary>
/// Automated tests for database schema verification (no IBKR required)
/// </summary>
public sealed class DatabaseSchemaTests : IAsyncLifetime
{
    private SqliteConnection _supervisorConn = default!;
    private SqliteConnection _optionsConn = default!;

    public async Task InitializeAsync()
    {
        // Create in-memory databases for schema validation
        _supervisorConn = new SqliteConnection("Data Source=:memory:");
        await _supervisorConn.OpenAsync();

        _optionsConn = new SqliteConnection("Data Source=:memory:");
        await _optionsConn.OpenAsync();

        // Apply migrations
        IDbConnectionFactory supervisorFactory = new InMemoryConnectionFactory(_supervisorConn);
        IDbConnectionFactory optionsFactory = new InMemoryConnectionFactory(_optionsConn);

        await new MigrationRunner(supervisorFactory, Microsoft.Extensions.Logging.Abstractions.NullLogger<MigrationRunner>.Instance)
            .RunAsync(SupervisorMigrations.All, CancellationToken.None);

        await new MigrationRunner(optionsFactory, Microsoft.Extensions.Logging.Abstractions.NullLogger<MigrationRunner>.Instance)
            .RunAsync(OptionsMigrations.All, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _supervisorConn.DisposeAsync();
        await _optionsConn.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-01")]
    public async Task SupervisorDb_ShouldHaveRequiredTables()
    {
        // Arrange
        string[] expectedTables = new[]
        {
            "schema_migrations",
            "service_heartbeats",
            "sync_outbox",
            "alert_history",
            "log_reader_state",
            "ivts_snapshots",
            "positions_snapshot"
        };

        // Act
        string[] actualTables = (await _supervisorConn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"
        )).ToArray();

        // Assert
        foreach (string expectedTable in expectedTables)
        {
            Assert.Contains(expectedTable, actualTables);
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-02")]
    public async Task OptionsDb_ShouldHaveRequiredTables()
    {
        // Arrange
        string[] expectedTables = new[]
        {
            "schema_migrations",
            "campaigns",
            "positions",
            "strategy_cache",
            "contract_cache",
            "market_data_cache",
            "orders"
        };

        // Act
        string[] actualTables = (await _optionsConn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"
        )).ToArray();

        // Assert
        foreach (string expectedTable in expectedTables)
        {
            Assert.Contains(expectedTable, actualTables);
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-03")]
    public async Task ServiceHeartbeats_ShouldHaveCorrectSchema()
    {
        // Arrange & Act
        List<TableColumn> columns = (await _supervisorConn.QueryAsync<TableColumn>(
            "PRAGMA table_info(service_heartbeats)"
        )).ToList();

        // Assert
        Assert.Contains(columns, c => c.Name == "heartbeat_id" && c.Pk == 1);
        Assert.Contains(columns, c => c.Name == "hostname" && c.Type == "TEXT" && c.Notnull == 1);
        Assert.Contains(columns, c => c.Name == "cpu_percent" && c.Type == "REAL");
        Assert.Contains(columns, c => c.Name == "ram_percent" && c.Type == "REAL");
        Assert.Contains(columns, c => c.Name == "trading_mode" && c.Type == "TEXT");
        Assert.Contains(columns, c => c.Name == "recorded_at" && c.Type == "TEXT" && c.Notnull == 1);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-04")]
    public async Task SyncOutbox_ShouldHaveIndexOnStatus()
    {
        // Act
        List<IndexInfo> indexes = (await _supervisorConn.QueryAsync<IndexInfo>(
            "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='sync_outbox'"
        )).ToList();

        // Assert
        Assert.Contains(indexes, i => i.Name.Contains("sync_status") || i.Name.Contains("idx_outbox_status"));
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-05")]
    public async Task Campaigns_ShouldHavePrimaryKey()
    {
        // Act
        List<TableColumn> columns = (await _optionsConn.QueryAsync<TableColumn>(
            "PRAGMA table_info(campaigns)"
        )).ToList();

        TableColumn? pkColumn = columns.FirstOrDefault(c => c.Pk == 1);

        // Assert
        Assert.NotNull(pkColumn);
        Assert.Equal("campaign_id", pkColumn.Name);
        Assert.Equal("TEXT", pkColumn.Type);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-06")]
    public async Task Positions_ShouldHaveGreeksColumns()
    {
        // Act
        List<TableColumn> columns = (await _optionsConn.QueryAsync<TableColumn>(
            "PRAGMA table_info(positions)"
        )).ToList();

        // Assert - Greeks columns exist
        Assert.Contains(columns, c => c.Name == "delta" && c.Type == "REAL");
        Assert.Contains(columns, c => c.Name == "gamma" && c.Type == "REAL");
        Assert.Contains(columns, c => c.Name == "theta" && c.Type == "REAL");
        Assert.Contains(columns, c => c.Name == "vega" && c.Type == "REAL");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-07")]
    public async Task Orders_ShouldHaveIbkrOrderId()
    {
        // Act
        List<TableColumn> columns = (await _optionsConn.QueryAsync<TableColumn>(
            "PRAGMA table_info(orders)"
        )).ToList();

        // Assert
        Assert.Contains(columns, c => c.Name == "ibkr_order_id" && c.Type == "INTEGER");
        Assert.Contains(columns, c => c.Name == "status" && c.Type == "TEXT");
        Assert.Contains(columns, c => c.Name == "filled_price" && c.Type == "REAL");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-08")]
    public async Task SchemaMigrations_ShouldRecordAllMigrations()
    {
        // Act
        int supervisorCount = await _supervisorConn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM schema_migrations"
        );

        int optionsCount = await _optionsConn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM schema_migrations"
        );

        // Assert
        Assert.True(supervisorCount >= 1, "Supervisor migrations not applied");
        Assert.True(optionsCount >= 1, "Options migrations not applied");
    }

    // Helper classes for Dapper mapping
    private sealed record TableColumn(string Name, string Type, int Notnull, int Pk);
    private sealed record IndexInfo(string Name);

    // Simple in-memory connection factory for testing
    private sealed class InMemoryConnectionFactory : IDbConnectionFactory
    {
        private readonly SqliteConnection _connection;

        public InMemoryConnectionFactory(SqliteConnection connection)
        {
            _connection = connection;
        }

        public Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            // Return same connection (already open, in-memory)
            return Task.FromResult(_connection);
        }
    }
}
