using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Data;
using TradingSupervisorService.Repositories;
using Xunit;

namespace TradingSupervisorService.Tests.Repositories;

/// <summary>
/// Unit tests for IvtsRepository.
/// Uses in-memory SQLite database for isolated testing.
/// </summary>
public sealed class IvtsRepositoryTests : IDisposable
{
    private readonly SqliteConnection _keepAliveConnection;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IvtsRepository _repo;

    public IvtsRepositoryTests()
    {
        // Create in-memory database with keep-alive connection
        _keepAliveConnection = new SqliteConnection("Data Source=:memory:;Mode=Memory;Cache=Shared");
        _keepAliveConnection.Open();

        // Create connection factory
        _dbFactory = new InMemoryConnectionFactory(_keepAliveConnection.ConnectionString);

        // Create repository with null logger (no logging in tests)
        ILogger<IvtsRepository> logger = new LoggerFactory().CreateLogger<IvtsRepository>();
        _repo = new IvtsRepository(_dbFactory, logger);

        // Create schema
        CreateSchemaAsync().GetAwaiter().GetResult();
    }

    private async Task CreateSchemaAsync()
    {
        await using SqliteConnection conn = await _dbFactory.OpenAsync();

        // Create ivts_snapshots table (from migration 002)
        const string createTable = """
            CREATE TABLE ivts_snapshots (
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
            """;

        // Create alert_history table (from migration 001)
        const string createAlertsTable = """
            CREATE TABLE alert_history (
                alert_id       TEXT PRIMARY KEY,
                alert_type     TEXT NOT NULL,
                severity       TEXT NOT NULL,
                message        TEXT NOT NULL,
                details_json   TEXT,
                source_service TEXT NOT NULL,
                created_at     TEXT NOT NULL,
                resolved_at    TEXT,
                resolved_by    TEXT
            );
            """;

        await conn.ExecuteAsync(createTable);
        await conn.ExecuteAsync(createAlertsTable);
    }

    [Fact]
    public async Task InsertSnapshotAsync_ValidSnapshot_Succeeds()
    {
        // Arrange
        DateTime now = DateTime.UtcNow;
        IvtsSnapshot snapshot = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = "SPX",
            TimestampUtc = now.ToString("O"),
            Iv30d = 0.15,
            Iv60d = 0.18,
            Iv90d = 0.20,
            Iv120d = 0.22,
            IvrPercentile = 0.75,
            TermStructureSlope = 0.00077,
            IsInverted = false,
            IvMin52Week = 0.10,
            IvMax52Week = 0.30,
            CreatedAt = now.ToString("O")
        };

        // Act
        await _repo.InsertSnapshotAsync(snapshot, CancellationToken.None);

        // Assert - retrieve and verify
        IvtsSnapshot? retrieved = await _repo.GetLatestSnapshotAsync("SPX", CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(snapshot.SnapshotId, retrieved.SnapshotId);
        Assert.Equal(snapshot.Symbol, retrieved.Symbol);
        Assert.Equal(snapshot.Iv30d, retrieved.Iv30d);
        Assert.Equal(snapshot.Iv60d, retrieved.Iv60d);
        Assert.Equal(snapshot.IvrPercentile, retrieved.IvrPercentile);
        Assert.Equal(snapshot.IsInverted, retrieved.IsInverted);
    }

    [Fact]
    public async Task GetLatestSnapshotAsync_NoData_ReturnsNull()
    {
        // Act
        IvtsSnapshot? snapshot = await _repo.GetLatestSnapshotAsync("SPY", CancellationToken.None);

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task GetLatestSnapshotAsync_MultipleSnapshots_ReturnsNewest()
    {
        // Arrange - insert 3 snapshots with different timestamps
        DateTime baseTime = DateTime.UtcNow.AddHours(-2);

        IvtsSnapshot snapshot1 = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = "SPX",
            TimestampUtc = baseTime.ToString("O"),
            Iv30d = 0.15,
            Iv60d = 0.18,
            Iv90d = 0.20,
            Iv120d = 0.22,
            TermStructureSlope = 0.00077,
            IsInverted = false,
            CreatedAt = baseTime.ToString("O")
        };

        IvtsSnapshot snapshot2 = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = "SPX",
            TimestampUtc = baseTime.AddHours(1).ToString("O"),
            Iv30d = 0.16,
            Iv60d = 0.19,
            Iv90d = 0.21,
            Iv120d = 0.23,
            TermStructureSlope = 0.00077,
            IsInverted = false,
            CreatedAt = baseTime.AddHours(1).ToString("O")
        };

        IvtsSnapshot snapshot3 = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = "SPX",
            TimestampUtc = baseTime.AddHours(2).ToString("O"),
            Iv30d = 0.17,
            Iv60d = 0.20,
            Iv90d = 0.22,
            Iv120d = 0.24,
            TermStructureSlope = 0.00077,
            IsInverted = false,
            CreatedAt = baseTime.AddHours(2).ToString("O")
        };

        await _repo.InsertSnapshotAsync(snapshot1, CancellationToken.None);
        await _repo.InsertSnapshotAsync(snapshot2, CancellationToken.None);
        await _repo.InsertSnapshotAsync(snapshot3, CancellationToken.None);

        // Act
        IvtsSnapshot? latest = await _repo.GetLatestSnapshotAsync("SPX", CancellationToken.None);

        // Assert - should return snapshot3 (newest)
        Assert.NotNull(latest);
        Assert.Equal(snapshot3.SnapshotId, latest.SnapshotId);
        Assert.Equal(0.17, latest.Iv30d);
    }

    [Fact]
    public async Task Get52WeekIvRangeAsync_WithData_ReturnsMinMax()
    {
        // Arrange - insert snapshots with varying IV levels
        DateTime now = DateTime.UtcNow;

        // Low IV snapshot
        IvtsSnapshot lowIv = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = "SPX",
            TimestampUtc = now.AddDays(-100).ToString("O"),
            Iv30d = 0.10,  // min
            Iv60d = 0.12,
            Iv90d = 0.14,
            Iv120d = 0.16,
            TermStructureSlope = 0.00067,
            IsInverted = false,
            CreatedAt = now.AddDays(-100).ToString("O")
        };

        // High IV snapshot
        IvtsSnapshot highIv = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = "SPX",
            TimestampUtc = now.AddDays(-50).ToString("O"),
            Iv30d = 0.28,  // max
            Iv60d = 0.30,
            Iv90d = 0.32,
            Iv120d = 0.34,
            TermStructureSlope = 0.00067,
            IsInverted = false,
            CreatedAt = now.AddDays(-50).ToString("O")
        };

        // Current IV snapshot
        IvtsSnapshot currentIv = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = "SPX",
            TimestampUtc = now.ToString("O"),
            Iv30d = 0.15,
            Iv60d = 0.18,
            Iv90d = 0.20,
            Iv120d = 0.22,
            TermStructureSlope = 0.00077,
            IsInverted = false,
            CreatedAt = now.ToString("O")
        };

        await _repo.InsertSnapshotAsync(lowIv, CancellationToken.None);
        await _repo.InsertSnapshotAsync(highIv, CancellationToken.None);
        await _repo.InsertSnapshotAsync(currentIv, CancellationToken.None);

        // Act
        (double MinIv, double MaxIv)? range = await _repo.Get52WeekIvRangeAsync("SPX", CancellationToken.None);

        // Assert
        Assert.NotNull(range);
        // Min should be average of 0.10 and 0.12 = 0.11
        Assert.Equal(0.11, range.Value.MinIv, precision: 3);
        // Max should be average of 0.28 and 0.30 = 0.29
        Assert.Equal(0.29, range.Value.MaxIv, precision: 3);
    }

    [Fact]
    public async Task Get52WeekIvRangeAsync_NoData_ReturnsNull()
    {
        // Act
        (double MinIv, double MaxIv)? range = await _repo.Get52WeekIvRangeAsync("SPY", CancellationToken.None);

        // Assert
        Assert.Null(range);
    }

    [Fact]
    public async Task InsertAlertAsync_ValidAlert_Succeeds()
    {
        // Arrange
        DateTime now = DateTime.UtcNow;
        IvtsAlert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "IvrThresholdBreach",
            Severity = "warning",
            Symbol = "SPX",
            Message = "IVR is 85%",
            SnapshotId = Guid.NewGuid().ToString(),
            DetailsJson = "{\"ivr\":0.85}",
            SourceService = "TradingSupervisorService",
            CreatedAt = now.ToString("O")
        };

        // Act
        await _repo.InsertAlertAsync(alert, CancellationToken.None);

        // Assert - retrieve and verify
        List<IvtsAlert> activeAlerts = await _repo.GetActiveAlertsAsync("SPX", CancellationToken.None);
        Assert.Single(activeAlerts);
        Assert.Equal(alert.AlertId, activeAlerts[0].AlertId);
        Assert.Equal(alert.AlertType, activeAlerts[0].AlertType);
        Assert.Equal(alert.Severity, activeAlerts[0].Severity);
    }

    [Fact]
    public async Task ResolveAlertAsync_ExistingAlert_UpdatesResolvedFields()
    {
        // Arrange - create and insert alert
        DateTime now = DateTime.UtcNow;
        IvtsAlert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "InvertedTermStructure",
            Severity = "critical",
            Symbol = "SPX",
            Message = "Term structure is inverted",
            SnapshotId = Guid.NewGuid().ToString(),
            SourceService = "TradingSupervisorService",
            CreatedAt = now.ToString("O")
        };

        await _repo.InsertAlertAsync(alert, CancellationToken.None);

        // Act - resolve the alert
        await _repo.ResolveAlertAsync(alert.AlertId, "auto", CancellationToken.None);

        // Assert - active alerts should be empty
        List<IvtsAlert> activeAlerts = await _repo.GetActiveAlertsAsync("SPX", CancellationToken.None);
        Assert.Empty(activeAlerts);
    }

    [Fact]
    public async Task GetActiveAlertsAsync_OnlyReturnsUnresolvedAlerts()
    {
        // Arrange - create two alerts, resolve one
        DateTime now = DateTime.UtcNow;

        IvtsAlert alert1 = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "IvrThresholdBreach",
            Severity = "warning",
            Symbol = "SPX",
            Message = "Alert 1",
            SnapshotId = Guid.NewGuid().ToString(),
            SourceService = "TradingSupervisorService",
            CreatedAt = now.ToString("O")
        };

        IvtsAlert alert2 = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "IvtsSpike",
            Severity = "warning",
            Symbol = "SPX",
            Message = "Alert 2",
            SnapshotId = Guid.NewGuid().ToString(),
            SourceService = "TradingSupervisorService",
            CreatedAt = now.ToString("O")
        };

        await _repo.InsertAlertAsync(alert1, CancellationToken.None);
        await _repo.InsertAlertAsync(alert2, CancellationToken.None);

        // Resolve alert1
        await _repo.ResolveAlertAsync(alert1.AlertId, "auto", CancellationToken.None);

        // Act
        List<IvtsAlert> activeAlerts = await _repo.GetActiveAlertsAsync("SPX", CancellationToken.None);

        // Assert - should only return alert2
        Assert.Single(activeAlerts);
        Assert.Equal(alert2.AlertId, activeAlerts[0].AlertId);
    }

    public void Dispose()
    {
        _keepAliveConnection.Close();
        _keepAliveConnection.Dispose();
    }

    /// <summary>
    /// In-memory connection factory for testing.
    /// Uses shared cache to maintain database state across connections.
    /// </summary>
    private sealed class InMemoryConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public InMemoryConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
        {
            SqliteConnection conn = new(_connectionString);
            await conn.OpenAsync(ct);
            // Note: We DON'T set WAL mode for in-memory databases (not supported)
            await conn.ExecuteAsync("PRAGMA foreign_keys=ON;");
            return conn;
        }
    }
}
