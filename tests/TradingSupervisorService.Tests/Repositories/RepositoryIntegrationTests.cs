using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Helpers;
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

    [Fact(DisplayName = "TEST-22-26: HeartbeatRepository inserts and retrieves metrics")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-26")]
    public async Task TEST_22_26_HeartbeatRepositoryInsertsAndRetrievesMetrics()
    {
        // Arrange
        MachineMetrics metrics = new()
        {
            Hostname = "INTEGRATION-TEST",
            TimestampUtc = DateTime.UtcNow,
            UptimeSeconds = 7200,
            CpuPercent = 45.5,
            RamPercent = 67.2,
            DiskFreeGb = 150.0,
            TradingMode = "paper"
        };

        // Act: Insert metrics
        await _heartbeatRepo.InsertAsync(metrics, CancellationToken.None);

        // Retrieve latest
        MachineMetrics? retrieved = await _heartbeatRepo.GetLatestAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("INTEGRATION-TEST", retrieved.Hostname);
        Assert.Equal(45.5, retrieved.CpuPercent, precision: 1);
        Assert.Equal(67.2, retrieved.RamPercent, precision: 1);
        Assert.Equal(150.0, retrieved.DiskFreeGb, precision: 1);
        Assert.Equal(7200, retrieved.UptimeSeconds);
        Assert.Equal("paper", retrieved.TradingMode);
    }

    [Fact(DisplayName = "TEST-22-27: OutboxRepository enqueues and dequeues events")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-27")]
    public async Task TEST_22_27_OutboxRepositoryEnqueuesAndDequeuesEvents()
    {
        // Arrange
        OutboxEvent event1 = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "test.event",
            Payload = "{\"test\":\"data\"}",
            CreatedAt = DateTime.UtcNow,
            Synced = false
        };

        OutboxEvent event2 = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "test.event2",
            Payload = "{\"test\":\"data2\"}",
            CreatedAt = DateTime.UtcNow,
            Synced = false
        };

        // Act: Enqueue events
        await _outboxRepo.EnqueueAsync(event1, CancellationToken.None);
        await _outboxRepo.EnqueueAsync(event2, CancellationToken.None);

        // Dequeue pending events
        List<OutboxEvent> pending = await _outboxRepo.GetPendingSyncAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(2, pending.Count);
        Assert.Contains(pending, e => e.EventId == event1.EventId);
        Assert.Contains(pending, e => e.EventId == event2.EventId);
        Assert.All(pending, e => Assert.False(e.Synced));
    }

    [Fact(DisplayName = "TEST-22-28: AlertRepository creates and retrieves alerts")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-28")]
    public async Task TEST_22_28_AlertRepositoryCreatesAndRetrievesAlerts()
    {
        // Arrange
        Alert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            Severity = "critical",
            Message = "Integration test alert",
            Source = "TEST",
            CreatedAt = DateTime.UtcNow,
            SentToTelegram = false
        };

        // Act: Insert alert
        await _alertRepo.InsertAsync(alert, CancellationToken.None);

        // Retrieve unsent alerts
        List<Alert> unsent = await _alertRepo.GetUnsentAlertsAsync(10, CancellationToken.None);

        // Assert
        Assert.Single(unsent);
        Assert.Equal("critical", unsent[0].Severity);
        Assert.Equal("Integration test alert", unsent[0].Message);
        Assert.Equal("TEST", unsent[0].Source);
        Assert.False(unsent[0].SentToTelegram);
    }

    [Fact(DisplayName = "TEST-22-29: LogReaderStateRepository persists and loads state")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-29")]
    public async Task TEST_22_29_LogReaderStateRepositoryPersistsAndLoadsState()
    {
        // Arrange
        LogReaderState state = new()
        {
            ServiceName = "OptionsExecutionService",
            LastReadPosition = 12345,
            LastReadTimestamp = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        // Act: Save state
        await _logReaderRepo.SaveStateAsync(state, CancellationToken.None);

        // Load state
        LogReaderState? loaded = await _logReaderRepo.LoadStateAsync("OptionsExecutionService", CancellationToken.None);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("OptionsExecutionService", loaded.ServiceName);
        Assert.Equal(12345, loaded.LastReadPosition);
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
            TimestampUtc = DateTime.UtcNow,
            UnderlyingSymbol = "SPY",
            Strike = 450.0m,
            Expiration = new DateTime(2025, 12, 31),
            OptionType = "CALL",
            ImpliedVolatility = 0.25,
            MarketPrice = 5.50m,
            UnderlyingPrice = 455.0m
        };

        // Act: Insert snapshot
        await _ivtsRepo.InsertSnapshotAsync(snapshot, CancellationToken.None);

        // Retrieve latest for symbol
        List<IvtsSnapshot> snapshots = await _ivtsRepo.GetLatestSnapshotsAsync("SPY", 10, CancellationToken.None);

        // Assert
        Assert.Single(snapshots);
        Assert.Equal("SPY", snapshots[0].UnderlyingSymbol);
        Assert.Equal(450.0m, snapshots[0].Strike);
        Assert.Equal("CALL", snapshots[0].OptionType);
        Assert.Equal(0.25, snapshots[0].ImpliedVolatility, precision: 2);
    }
}
