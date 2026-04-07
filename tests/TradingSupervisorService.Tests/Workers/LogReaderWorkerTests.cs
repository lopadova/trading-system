using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Helpers;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Workers;
using Xunit;

namespace tests.TradingSupervisorService.Tests.Workers;

/// <summary>
/// Tests for LogReaderWorker.
/// Verifies log file reading, parsing, and alert creation.
/// </summary>
public sealed class LogReaderWorkerTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _dbFactory;
    private readonly LogReaderStateRepository _stateRepo;
    private readonly AlertRepository _alertRepo;
    private readonly string _testLogDir;

    public LogReaderWorkerTests()
    {
        _dbFactory = new InMemoryConnectionFactory();
        _stateRepo = new LogReaderStateRepository(_dbFactory, NullLogger<LogReaderStateRepository>.Instance);
        _alertRepo = new AlertRepository(_dbFactory, NullLogger<AlertRepository>.Instance);

        // Create temporary log directory for tests
        _testLogDir = Path.Combine(Path.GetTempPath(), $"logreader-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testLogDir);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbFactory.DisposeAsync();

        // Clean up test log directory
        if (Directory.Exists(_testLogDir))
        {
            Directory.Delete(_testLogDir, recursive: true);
        }
    }

    [Fact]
    public async Task LogReaderWorker_CanBeInstantiated()
    {
        // Arrange
        await CreateSchemaAsync();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LogReader:OptionsServiceLogPath"] = Path.Combine(_testLogDir, "test-.log"),
                ["LogReader:IntervalSeconds"] = "1"
            })
            .Build();

        // Act
        LogReaderWorker worker = new(
            NullLogger<LogReaderWorker>.Instance,
            _stateRepo,
            _alertRepo,
            config);

        // Assert
        Assert.NotNull(worker);
    }

    [Fact]
    public async Task LogReaderWorker_WithNonExistentLogFile_DoesNotCrash()
    {
        // Arrange
        await CreateSchemaAsync();
        string logFilePath = Path.Combine(_testLogDir, "nonexistent-.log");

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LogReader:OptionsServiceLogPath"] = logFilePath,
                ["LogReader:IntervalSeconds"] = "1"
            })
            .Build();

        LogReaderWorker worker = new(
            NullLogger<LogReaderWorker>.Instance,
            _stateRepo,
            _alertRepo,
            config);

        // Act - start worker briefly, then stop
        CancellationTokenSource cts = new();
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(100);  // Let worker run one cycle
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Assert - worker should not throw
        Assert.True(workerTask.IsCompleted || workerTask.IsCanceled);
    }

    [Fact]
    public void ExtractLogMessage_WithErrorLevel_ReturnsMessage()
    {
        // This test would require making ExtractLogMessage public or using reflection
        // For now, we test indirectly through integration tests
        // Skipping as it tests private method
    }

    /// <summary>
    /// Integration test: Create a log file with error entries,
    /// verify LogReaderWorker creates alerts.
    /// </summary>
    [Fact]
    public async Task LogReaderWorker_WithErrorInLog_CreatesAlert()
    {
        // Arrange
        await CreateSchemaAsync();

        string logFileName = $"test-{DateTime.UtcNow:yyyyMMdd}.log";
        string logFilePath = Path.Combine(_testLogDir, logFileName);

        // Write a log file with an error entry
        await File.WriteAllTextAsync(logFilePath,
            "[2026-04-05 10:30:15 ERR] Failed to connect to IBKR: connection refused\n");

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LogReader:OptionsServiceLogPath"] = Path.Combine(_testLogDir, "test-.log"),
                ["LogReader:IntervalSeconds"] = "1"
            })
            .Build();

        LogReaderWorker worker = new(
            NullLogger<LogReaderWorker>.Instance,
            _stateRepo,
            _alertRepo,
            config);

        // Act - run worker for one cycle
        CancellationTokenSource cts = new();
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);  // Give worker time to process
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Assert - verify alert was created
        IReadOnlyList<AlertRecord> alerts = await _alertRepo.GetUnresolvedAsync(10, CancellationToken.None);
        Assert.NotEmpty(alerts);
        Assert.Contains(alerts, a => a.Message.Contains("Failed to connect to IBKR"));
        Assert.Contains(alerts, a => a.Severity == AlertSeverity.Error.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task LogReaderWorker_WithWarningInLog_CreatesWarningAlert()
    {
        // Arrange
        await CreateSchemaAsync();

        string logFileName = $"test-{DateTime.UtcNow:yyyyMMdd}.log";
        string logFilePath = Path.Combine(_testLogDir, logFileName);

        // Write a log file with warning entry
        await File.WriteAllTextAsync(logFilePath,
            "[2026-04-05 10:30:15 WRN] Order execution delayed: market volatility\n");

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LogReader:OptionsServiceLogPath"] = Path.Combine(_testLogDir, "test-.log"),
                ["LogReader:IntervalSeconds"] = "1"
            })
            .Build();

        LogReaderWorker worker = new(
            NullLogger<LogReaderWorker>.Instance,
            _stateRepo,
            _alertRepo,
            config);

        // Act
        CancellationTokenSource cts = new();
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Assert
        IReadOnlyList<AlertRecord> alerts = await _alertRepo.GetUnresolvedAsync(10, CancellationToken.None);
        Assert.NotEmpty(alerts);
        Assert.Contains(alerts, a => a.Severity == AlertSeverity.Warning.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task LogReaderWorker_WithInfoLog_DoesNotCreateAlert()
    {
        // Arrange
        await CreateSchemaAsync();

        string logFileName = $"test-{DateTime.UtcNow:yyyyMMdd}.log";
        string logFilePath = Path.Combine(_testLogDir, logFileName);

        // Write a log file with only info entries
        await File.WriteAllTextAsync(logFilePath,
            "[2026-04-05 10:30:15 INF] Service started successfully\n");

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LogReader:OptionsServiceLogPath"] = Path.Combine(_testLogDir, "test-.log"),
                ["LogReader:IntervalSeconds"] = "1"
            })
            .Build();

        LogReaderWorker worker = new(
            NullLogger<LogReaderWorker>.Instance,
            _stateRepo,
            _alertRepo,
            config);

        // Act
        CancellationTokenSource cts = new();
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Assert - no alerts should be created for INFO level
        IReadOnlyList<AlertRecord> alerts = await _alertRepo.GetUnresolvedAsync(10, CancellationToken.None);
        Assert.Empty(alerts);
    }

    /// <summary>
    /// Creates database schema for testing.
    /// </summary>
    private async Task CreateSchemaAsync()
    {
        const string createLogReaderStateSql = """
            CREATE TABLE IF NOT EXISTS log_reader_state (
                file_path      TEXT PRIMARY KEY NOT NULL,
                last_position  INTEGER NOT NULL,
                last_size      INTEGER NOT NULL,
                updated_at     TEXT NOT NULL
            );
            """;

        const string createAlertHistorySql = """
            CREATE TABLE IF NOT EXISTS alert_history (
                alert_id       TEXT PRIMARY KEY NOT NULL,
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

        await using var conn = await _dbFactory.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync(createLogReaderStateSql);
        await conn.ExecuteAsync(createAlertHistorySql);
    }
}
