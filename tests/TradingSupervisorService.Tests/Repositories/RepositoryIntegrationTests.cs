using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Data;
using TradingSupervisorService.Collectors;
using TradingSupervisorService.Migrations;
using TradingSupervisorService.Repositories;
using Xunit;

namespace TradingSupervisorService.Tests.Repositories;

/// <summary>
/// Integration tests for TradingSupervisorService repositories with SQLite.
/// Verifies repositories persist and retrieve data correctly with real database.
/// TEST-22-26 through TEST-22-30
/// </summary>
public sealed class RepositoryIntegrationTests : IAsyncLifetime
{
    private InMemoryConnectionFactory _factory = default!;
    private IHeartbeatRepository _heartbeatRepo = default!;
    private IOutboxRepository _outboxRepo = default!;
    private IAlertRepository _alertRepo = default!;
    private ILogReaderStateRepository _logReaderRepo = default!;
    private IIvtsRepository _ivtsRepo = default!;

    public async Task InitializeAsync()
    {
        _factory = new InMemoryConnectionFactory();

        // Run migrations
        MigrationRunner runner = new(_factory, NullLogger<MigrationRunner>.Instance);
        await runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Create repositories
        _heartbeatRepo = new HeartbeatRepository(_factory, NullLogger<HeartbeatRepository>.Instance);
        _outboxRepo = new OutboxRepository(_factory, NullLogger<OutboxRepository>.Instance);
        _alertRepo = new AlertRepository(_factory, NullLogger<AlertRepository>.Instance);
        _logReaderRepo = new LogReaderStateRepository(_factory, NullLogger<LogReaderStateRepository>.Instance);
        _ivtsRepo = new IvtsRepository(_factory, NullLogger<IvtsRepository>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact(DisplayName = "TEST-22-26: HeartbeatRepository upserts and retrieves heartbeats")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-26")]
    public async Task TEST_22_26_HeartbeatRepositoryUpsertsAndRetrievesHeartbeats()
    {
        // Arrange
        ServiceHeartbeat heartbeat = new()
        {
            ServiceName = "TestService",
            Hostname = "INTEGRATION-TEST",
            LastSeenAt = DateTime.UtcNow.ToString("O"),
            UptimeSeconds = 7200,
            CpuPercent = 45.5,
            RamPercent = 67.2,
            DiskFreeGb = 150.0,
            TradingMode = "paper",
            Version = "1.0.0",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };

        // Act: Upsert heartbeat
        await _heartbeatRepo.UpsertAsync(heartbeat, CancellationToken.None);

        // Retrieve all
        IReadOnlyList<ServiceHeartbeat> retrieved = await _heartbeatRepo.GetAllAsync(CancellationToken.None);

        // Assert
        Assert.Single(retrieved);
        ServiceHeartbeat first = retrieved[0];
        Assert.Equal("INTEGRATION-TEST", first.Hostname);
        Assert.Equal(45.5, first.CpuPercent, precision: 1);
        Assert.Equal(67.2, first.RamPercent, precision: 1);
        Assert.Equal(150.0, first.DiskFreeGb, precision: 1);
        Assert.Equal(7200, first.UptimeSeconds);
        Assert.Equal("paper", first.TradingMode);
    }

    [Fact(DisplayName = "TEST-22-27: OutboxRepository inserts and retrieves pending entries")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-27")]
    public async Task TEST_22_27_OutboxRepositoryInsertsAndRetrievesPendingEntries()
    {
        // Arrange
        OutboxEntry entry1 = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "test.event",
            PayloadJson = "{\"test\":\"data\"}",
            DedupeKey = null,
            Status = "pending",
            RetryCount = 0,
            LastError = null,
            NextRetryAt = DateTime.UtcNow.ToString("O"),
            CreatedAt = DateTime.UtcNow.ToString("O"),
            SentAt = null
        };

        OutboxEntry entry2 = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "test.event2",
            PayloadJson = "{\"test\":\"data2\"}",
            DedupeKey = null,
            Status = "pending",
            RetryCount = 0,
            LastError = null,
            NextRetryAt = DateTime.UtcNow.ToString("O"),
            CreatedAt = DateTime.UtcNow.ToString("O"),
            SentAt = null
        };

        // Act: Insert entries
        await _outboxRepo.InsertAsync(entry1, CancellationToken.None);
        await _outboxRepo.InsertAsync(entry2, CancellationToken.None);

        // Get pending entries
        IReadOnlyList<OutboxEntry> pending = await _outboxRepo.GetPendingAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(2, pending.Count);
        Assert.Contains(pending, e => e.EventId == entry1.EventId);
        Assert.Contains(pending, e => e.EventId == entry2.EventId);
        Assert.All(pending, e => Assert.Equal("pending", e.Status));
    }

    [Fact(DisplayName = "TEST-22-28: AlertRepository inserts and retrieves unresolved alerts")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-28")]
    public async Task TEST_22_28_AlertRepositoryInsertsAndRetrievesUnresolvedAlerts()
    {
        // Arrange
        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "system.test",
            Severity = "critical",
            Message = "Integration test alert",
            DetailsJson = null,
            SourceService = "TEST",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        // Act: Insert alert
        await _alertRepo.InsertAsync(alert, CancellationToken.None);

        // Retrieve unresolved alerts
        IReadOnlyList<AlertRecord> unresolved = await _alertRepo.GetUnresolvedAsync(10, CancellationToken.None);

        // Assert
        Assert.Single(unresolved);
        Assert.Equal("critical", unresolved[0].Severity);
        Assert.Equal("Integration test alert", unresolved[0].Message);
        Assert.Equal("TEST", unresolved[0].SourceService);
        Assert.Null(unresolved[0].ResolvedAt);
    }

    [Fact(DisplayName = "TEST-22-29: LogReaderStateRepository upserts and retrieves state")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-29")]
    public async Task TEST_22_29_LogReaderStateRepositoryUpsertsAndRetrievesState()
    {
        // Arrange
        LogReaderStateRecord state = new()
        {
            FilePath = "/var/log/OptionsExecutionService.log",
            LastPosition = 12345,
            LastSize = 50000,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };

        // Act: Upsert state
        await _logReaderRepo.UpsertStateAsync(state, CancellationToken.None);

        // Get state
        LogReaderStateRecord? loaded = await _logReaderRepo.GetStateAsync("/var/log/OptionsExecutionService.log", CancellationToken.None);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("/var/log/OptionsExecutionService.log", loaded.FilePath);
        Assert.Equal(12345, loaded.LastPosition);
        Assert.Equal(50000, loaded.LastSize);
    }

    [Fact(DisplayName = "TEST-22-30: IvtsRepository stores and queries snapshots")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-30")]
    public async Task TEST_22_30_IvtsRepositoryStoresAndQueriesSnapshots()
    {
        // Arrange
        IvtsSnapshot snapshot = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = "SPY",
            TimestampUtc = DateTime.UtcNow.ToString("O"),
            Iv30d = 0.25,
            Iv60d = 0.28,
            Iv90d = 0.30,
            Iv120d = 0.32,
            IvrPercentile = 0.45,
            TermStructureSlope = 0.0007,
            IsInverted = false,
            IvMin52Week = 0.15,
            IvMax52Week = 0.50,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        // Act: Insert snapshot
        await _ivtsRepo.InsertSnapshotAsync(snapshot, CancellationToken.None);

        // Retrieve latest for symbol
        IvtsSnapshot? latest = await _ivtsRepo.GetLatestSnapshotAsync("SPY", CancellationToken.None);

        // Assert
        Assert.NotNull(latest);
        Assert.Equal("SPY", latest.Symbol);
        Assert.Equal(0.25, latest.Iv30d, precision: 2);
        Assert.Equal(0.28, latest.Iv60d, precision: 2);
        Assert.NotNull(latest.IvrPercentile);
        Assert.Equal(0.45, latest.IvrPercentile.Value, precision: 2);
    }
}
