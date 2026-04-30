using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Workers;
using Xunit;

namespace OptionsExecutionService.Tests.Workers;

/// <summary>
/// Tests for OutboxReconcilerWorker - processes pending outbox entries for crash recovery.
/// Ensures pending broker operations are retried after service restart.
/// Phase 1: State Persistence & Idempotency - Task #18
/// </summary>
public sealed class OutboxReconcilerWorkerTests
{
    /// <summary>
    /// Verifies that worker retrieves pending entries and processes them.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ProcessesPendingEntries()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OutboxReconcilerWorker>>();
        var mockRepository = new Mock<IOrderOutboxRepository>();
        var mockConfig = new Mock<IConfiguration>();

        // Configure reconciler interval
        mockConfig.Setup(c => c["OutboxReconciler:IntervalSeconds"])
            .Returns("60");

        // Setup pending entries
        var pendingEntries = new List<OrderOutboxEntry>
        {
            new OrderOutboxEntry
            {
                OutboxId = 1,
                OrderId = "order-123",
                Operation = "PlaceOrder",
                Payload = "{\"symbol\":\"SPY\",\"action\":\"BUY\"}",
                Status = "pending",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5).ToString("O")
            }
        };

        mockRepository.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEntries);

        // Mock IServiceScopeFactory and IServiceScope for DI (worker uses IServiceScopeFactory for testability)
        var mockServiceScope = new Mock<IServiceScope>();
        var mockScopeServiceProvider = new Mock<IServiceProvider>();
        mockScopeServiceProvider.Setup(sp => sp.GetService(typeof(IOrderOutboxRepository)))
            .Returns(mockRepository.Object);
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockScopeServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockServiceScope.Object);

        var worker = new OutboxReconcilerWorker(
            mockScopeFactory.Object,
            mockLogger.Object,
            mockConfig.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2)); // Stop after 2 seconds

        // ============================================================
        // ACT
        // ============================================================

        // Start worker and let it run one iteration
        var task = worker.StartAsync(cts.Token);
        await Task.Delay(100); // Give it time to process
        await worker.StopAsync(cts.Token);
        await task;

        // ============================================================
        // ASSERT
        // ============================================================

        // Verify worker called GetPendingAsync at least once
        mockRepository.Verify(
            r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Worker should retrieve pending entries");
    }

    /// <summary>
    /// Verifies that worker marks entries as sent after successful processing.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MarksEntriesAsSent()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OutboxReconcilerWorker>>();
        var mockRepository = new Mock<IOrderOutboxRepository>();
        var mockConfig = new Mock<IConfiguration>();

        mockConfig.Setup(c => c["OutboxReconciler:IntervalSeconds"])
            .Returns("60");

        var pendingEntry = new OrderOutboxEntry
        {
            OutboxId = 1,
            OrderId = "order-123",
            Operation = "PlaceOrder",
            Payload = "{\"symbol\":\"SPY\"}",
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5).ToString("O")
        };

        // First call returns pending entry, subsequent calls return empty
        var callCount = 0;
        mockRepository.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? new List<OrderOutboxEntry> { pendingEntry } : new List<OrderOutboxEntry>();
            });

        // Mock IServiceScopeFactory and IServiceScope for DI (worker uses IServiceScopeFactory for testability)
        var mockServiceScope = new Mock<IServiceScope>();
        var mockScopeServiceProvider = new Mock<IServiceProvider>();
        mockScopeServiceProvider.Setup(sp => sp.GetService(typeof(IOrderOutboxRepository)))
            .Returns(mockRepository.Object);
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockScopeServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockServiceScope.Object);

        var worker = new OutboxReconcilerWorker(
            mockScopeFactory.Object,
            mockLogger.Object,
            mockConfig.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        // ============================================================
        // ACT
        // ============================================================

        var task = worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(cts.Token);
        await task;

        // ============================================================
        // ASSERT
        // ============================================================

        // Verify worker called MarkSentAsync for the processed entry
        mockRepository.Verify(
            r => r.MarkSentAsync(1, It.IsAny<CancellationToken>()),
            Times.Once,
            "Worker should mark entry as sent after processing");
    }

    /// <summary>
    /// Verifies that worker marks entries as failed when processing throws.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MarksEntriesAsFailed_OnError()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OutboxReconcilerWorker>>();
        var mockRepository = new Mock<IOrderOutboxRepository>();
        var mockConfig = new Mock<IConfiguration>();

        mockConfig.Setup(c => c["OutboxReconciler:IntervalSeconds"])
            .Returns("60");

        var pendingEntry = new OrderOutboxEntry
        {
            OutboxId = 1,
            OrderId = "order-123",
            Operation = "PlaceOrder",
            Payload = "{invalid json}",  // This will cause parsing error
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5).ToString("O")
        };

        var callCount = 0;
        mockRepository.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? new List<OrderOutboxEntry> { pendingEntry } : new List<OrderOutboxEntry>();
            });

        // Mock IServiceScopeFactory and IServiceScope for DI (worker uses IServiceScopeFactory for testability)
        var mockServiceScope = new Mock<IServiceScope>();
        var mockScopeServiceProvider = new Mock<IServiceProvider>();
        mockScopeServiceProvider.Setup(sp => sp.GetService(typeof(IOrderOutboxRepository)))
            .Returns(mockRepository.Object);
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockScopeServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockServiceScope.Object);

        var worker = new OutboxReconcilerWorker(
            mockScopeFactory.Object,
            mockLogger.Object,
            mockConfig.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        // ============================================================
        // ACT
        // ============================================================

        var task = worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(cts.Token);
        await task;

        // ============================================================
        // ASSERT
        // ============================================================

        // Verify worker called MarkFailedAsync for the entry that failed processing
        mockRepository.Verify(
            r => r.MarkFailedAsync(1, It.IsAny<CancellationToken>()),
            Times.Once,
            "Worker should mark entry as failed when processing throws");
    }
}
