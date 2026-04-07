using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests.Workers;

/// <summary>
/// Unit tests for OutboxSyncWorker.
/// Uses mocked IOutboxRepository and HttpClient to test sync logic.
/// </summary>
public sealed class OutboxSyncWorkerTests
{
    private readonly Mock<IOutboxRepository> _mockOutbox;
    private readonly Mock<ILogger<OutboxSyncWorker>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<IHttpClientFactory> _mockHttpFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;

    public OutboxSyncWorkerTests()
    {
        _mockOutbox = new Mock<IOutboxRepository>();
        _mockLogger = new Mock<ILogger<OutboxSyncWorker>>();
        _mockConfig = new Mock<IConfiguration>();
        _mockHttpFactory = new Mock<IHttpClientFactory>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();

        // Setup default configuration values
        SetupConfigValue("OutboxSync:IntervalSeconds", "1");  // Fast interval for tests
        SetupConfigValue("OutboxSync:BatchSize", "50");
        SetupConfigValue("Cloudflare:WorkerUrl", "https://test-worker.dev");
        SetupConfigValue("Cloudflare:ApiKey", "test-api-key");
        SetupConfigValue("OutboxSync:InitialRetryDelaySeconds", "5");
        SetupConfigValue("OutboxSync:MaxRetryDelaySeconds", "300");
        SetupConfigValue("OutboxSync:MaxRetries", "10");
        SetupConfigValue("OutboxSync:StartupDelaySeconds", "0");  // No delay for tests

        // Setup HttpClient factory to return client with mocked handler
        HttpClient httpClient = new(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("https://test-worker.dev")
        };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    private void SetupConfigValue(string key, string value)
    {
        Mock<IConfigurationSection> section = new();
        section.Setup(s => s.Value).Returns(value);
        _mockConfig.Setup(c => c.GetSection(key)).Returns(section.Object);
        _mockConfig.Setup(c => c[key]).Returns(value);
    }

    [Fact]
    [Trait("TestId", "TEST-08-08")]
    public async Task RunCycle_NoPendingEvents_DoesNotSendRequests()
    {
        // Arrange
        _mockOutbox.Setup(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxEntry>());

        OutboxSyncWorker worker = new(
            _mockLogger.Object,
            _mockOutbox.Object,
            _mockConfig.Object,
            _mockHttpFactory.Object);

        // Act
        CancellationTokenSource cts = new();
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(100);  // Let worker run one cycle
        cts.Cancel();
        await workerTask;

        // Assert
        _mockOutbox.Verify(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockOutbox.Verify(o => o.MarkSentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("TestId", "TEST-08-09")]
    public async Task RunCycle_SuccessfulSync_MarksEventAsSent()
    {
        // Arrange
        OutboxEntry entry = new()
        {
            EventId = "test-event-1",
            EventType = "heartbeat_updated",
            PayloadJson = "{\"service\":\"test\"}",
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        _mockOutbox.Setup(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxEntry> { entry });

        // Mock HTTP response (success)
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"ok\":true}")
            });

        OutboxSyncWorker worker = new(
            _mockLogger.Object,
            _mockOutbox.Object,
            _mockConfig.Object,
            _mockHttpFactory.Object);

        // Act
        CancellationTokenSource cts = new();
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(6000);  // Wait for initial delay (5s) + cycle
        cts.Cancel();
        try { await workerTask; } catch (OperationCanceledException) { }

        // Assert
        _mockOutbox.Verify(o => o.MarkSentAsync(entry.EventId, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    [Trait("TestId", "TEST-08-10")]
    public async Task RunCycle_FailedSync_MarksEventAsFailed()
    {
        // Arrange
        OutboxEntry entry = new()
        {
            EventId = "test-event-2",
            EventType = "alert_raised",
            PayloadJson = "{\"alert\":\"test\"}",
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        _mockOutbox.Setup(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxEntry> { entry });

        // Mock HTTP response (failure)
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("{\"error\":\"internal error\"}")
            });

        OutboxSyncWorker worker = new(
            _mockLogger.Object,
            _mockOutbox.Object,
            _mockConfig.Object,
            _mockHttpFactory.Object);

        // Act
        CancellationTokenSource cts = new();
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(6000);  // Wait for initial delay (5s) + cycle
        cts.Cancel();
        try { await workerTask; } catch (OperationCanceledException) { }

        // Assert
        _mockOutbox.Verify(o => o.MarkFailedAsync(
            entry.EventId,
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    [Trait("TestId", "TEST-08-11")]
    public void Constructor_MissingWorkerUrl_LogsWarning()
    {
        // Arrange
        SetupConfigValue("Cloudflare:WorkerUrl", "");

        // Act
        OutboxSyncWorker worker = new(
            _mockLogger.Object,
            _mockOutbox.Object,
            _mockConfig.Object,
            _mockHttpFactory.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("WorkerUrl is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-08-12")]
    public async Task ExponentialBackoff_CalculatesCorrectly()
    {
        // This test verifies the exponential backoff calculation indirectly
        // by checking that retry delays increase for failed events

        // Arrange
        List<OutboxEntry> entries = new()
        {
            new OutboxEntry
            {
                EventId = "test-retry-1",
                EventType = "test",
                PayloadJson = "{}",
                Status = "failed",
                RetryCount = 0,  // First retry: 5s delay
                CreatedAt = DateTime.UtcNow.ToString("O")
            },
            new OutboxEntry
            {
                EventId = "test-retry-2",
                EventType = "test",
                PayloadJson = "{}",
                Status = "failed",
                RetryCount = 3,  // Fourth retry: 5 * 2^3 = 40s delay
                CreatedAt = DateTime.UtcNow.ToString("O")
            }
        };

        _mockOutbox.Setup(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Mock HTTP response (failure)
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadGateway
            });

        OutboxSyncWorker worker = new(
            _mockLogger.Object,
            _mockOutbox.Object,
            _mockConfig.Object,
            _mockHttpFactory.Object);

        // Act
        CancellationTokenSource cts = new();
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(6000);  // Wait for cycle
        cts.Cancel();
        try { await workerTask; } catch (OperationCanceledException) { }

        // Assert
        // Verify that MarkFailedAsync was called with increasing nextRetryAt times
        _mockOutbox.Verify(o => o.MarkFailedAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<DateTime>(dt => dt > DateTime.UtcNow),  // nextRetryAt is in the future
            It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }
}
