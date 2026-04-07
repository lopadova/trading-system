using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using SharedKernel.Tests.Data;
using TradingSupervisorService.Collectors;
using TradingSupervisorService.Ibkr;
using TradingSupervisorService.Migrations;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Services;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests.Workers;

/// <summary>
/// Integration tests for worker lifecycle: start, stop, cancellation.
/// Tests that workers start correctly, execute their cycles, and stop gracefully.
/// TEST-22-21 through TEST-22-25
/// </summary>
public sealed class WorkerLifecycleIntegrationTests : IAsyncLifetime
{
    private InMemoryConnectionFactory _factory = default!;
    private IHeartbeatRepository _heartbeatRepo = default!;
    private IOutboxRepository _outboxRepo = default!;
    private IAlertRepository _alertRepo = default!;
    private ILogReaderStateRepository _logReaderRepo = default!;
    private Mock<IMachineMetricsCollector> _mockCollector = default!;

    public async Task InitializeAsync()
    {
        _factory = new InMemoryConnectionFactory();

        // Run migrations to set up schema
        MigrationRunner runner = new(_factory, NullLogger<MigrationRunner>.Instance);
        await runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        // Create repositories
        _heartbeatRepo = new HeartbeatRepository(_factory, NullLogger<HeartbeatRepository>.Instance);
        _outboxRepo = new OutboxRepository(_factory, NullLogger<OutboxRepository>.Instance);
        _alertRepo = new AlertRepository(_factory, NullLogger<AlertRepository>.Instance);
        _logReaderRepo = new LogReaderStateRepository(_factory, NullLogger<LogReaderStateRepository>.Instance);

        // Mock machine metrics collector
        _mockCollector = new Mock<IMachineMetricsCollector>();
        _mockCollector.Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MachineMetrics
            {
                Hostname = "TEST-HOST",
                CpuPercent = 25.0,
                RamPercent = 50.0,
                DiskFreeGb = 100.0,
                UptimeSeconds = 3600,
                TimestampUtc = DateTime.UtcNow
            });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact(DisplayName = "TEST-22-21: HeartbeatWorker starts and executes cycle")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-21")]
    public async Task TEST_22_21_HeartbeatWorkerStartsAndExecutesCycle()
    {
        // Arrange: Create worker with short interval
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:HeartbeatIntervalSeconds"] = "1"
            })
            .Build();

        HeartbeatWorker worker = new(
            NullLogger<HeartbeatWorker>.Instance,
            _mockCollector.Object,
            _heartbeatRepo,
            config);

        using CancellationTokenSource cts = new();

        // Act: Start worker and let it run for a short time
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        // Stop worker gracefully
        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when worker is cancelled
        }

        // Assert: Verify that heartbeat was written to database
        IReadOnlyList<ServiceHeartbeat> heartbeats = await _heartbeatRepo.GetAllAsync(CancellationToken.None);
        Assert.NotEmpty(heartbeats);
        ServiceHeartbeat latest = heartbeats[0];
        Assert.Equal("TEST-HOST", latest.Hostname);
        Assert.Equal(25.0, latest.CpuPercent);
    }

    [Fact(DisplayName = "TEST-22-22: OutboxSyncWorker starts and stops gracefully")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-22")]
    public async Task TEST_22_22_OutboxSyncWorkerStartsAndStopsGracefully()
    {
        // Arrange: Create worker with short interval
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:OutboxSyncIntervalSeconds"] = "1",
                ["OutboxSync:CloudflareWorkerUrl"] = "http://localhost:8787"
            })
            .Build();

        Mock<IHttpClientFactory> mockHttpFactory = new();
        mockHttpFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        OutboxSyncWorker worker = new(
            NullLogger<OutboxSyncWorker>.Instance,
            _outboxRepo,
            config,
            mockHttpFactory.Object);

        using CancellationTokenSource cts = new();

        // Act: Start worker
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);

        // Stop worker gracefully
        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when worker is cancelled
        }

        // Assert: Worker should stop without throwing exceptions
        Assert.True(workerTask.IsCompleted || workerTask.IsCanceled);
    }

    [Fact(DisplayName = "TEST-22-23: TelegramWorker handles cancellation correctly")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-23")]
    public async Task TEST_22_23_TelegramWorkerHandlesCancellationCorrectly()
    {
        // Arrange: Create worker with short interval
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:TelegramIntervalSeconds"] = "1"
            })
            .Build();

        Mock<ITelegramAlerter> mockTelegram = new();
        mockTelegram.Setup(x => x.SendImmediateAsync(It.IsAny<TelegramAlert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        TelegramWorker worker = new(
            NullLogger<TelegramWorker>.Instance,
            mockTelegram.Object,
            config);

        using CancellationTokenSource cts = new();

        // Act: Start worker
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);

        // Cancel immediately
        cts.Cancel();

        // Assert: Worker should handle cancellation gracefully (no unhandled exceptions)
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected - this is the correct behavior
        }

        Assert.True(workerTask.IsCompleted || workerTask.IsCanceled);
    }

    [Fact(DisplayName = "TEST-22-24: LogReaderWorker starts with valid configuration")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-24")]
    public async Task TEST_22_24_LogReaderWorkerStartsWithValidConfiguration()
    {
        // Arrange: Create worker with short interval
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:LogReaderIntervalSeconds"] = "1",
                ["LogReader:OptionsExecutionLogPath"] = "logs/test-options.log"
            })
            .Build();

        LogReaderWorker worker = new(
            NullLogger<LogReaderWorker>.Instance,
            _logReaderRepo,
            _alertRepo,
            config);

        using CancellationTokenSource cts = new();

        // Act: Start worker
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);

        // Stop worker
        cts.Cancel();
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert: Worker should start and stop without errors
        Assert.True(workerTask.IsCompleted || workerTask.IsCanceled);
    }

    [Fact(DisplayName = "TEST-22-25: Multiple workers can run concurrently")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-25")]
    public async Task TEST_22_25_MultipleWorkersCanRunConcurrently()
    {
        // Arrange: Create multiple workers
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:HeartbeatIntervalSeconds"] = "1",
                ["Monitoring:OutboxSyncIntervalSeconds"] = "1",
                ["OutboxSync:CloudflareWorkerUrl"] = "http://localhost:8787"
            })
            .Build();

        Mock<IHttpClientFactory> mockHttpFactory = new();
        mockHttpFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        HeartbeatWorker heartbeatWorker = new(
            NullLogger<HeartbeatWorker>.Instance,
            _mockCollector.Object,
            _heartbeatRepo,
            config);

        OutboxSyncWorker outboxWorker = new(
            NullLogger<OutboxSyncWorker>.Instance,
            _outboxRepo,
            config,
            mockHttpFactory.Object);

        using CancellationTokenSource cts = new();

        // Act: Start both workers concurrently
        Task heartbeatTask = heartbeatWorker.StartAsync(cts.Token);
        Task outboxTask = outboxWorker.StartAsync(cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        // Stop both workers
        cts.Cancel();

        // Assert: Both workers should complete or cancel without interfering with each other
        try
        {
            await Task.WhenAll(heartbeatTask, outboxTask);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Assert.True(heartbeatTask.IsCompleted || heartbeatTask.IsCanceled);
        Assert.True(outboxTask.IsCompleted || outboxTask.IsCanceled);

        // Verify heartbeat was written
        IReadOnlyList<ServiceHeartbeat> heartbeats = await _heartbeatRepo.GetAllAsync(CancellationToken.None);
        Assert.NotEmpty(heartbeats);
    }
}
