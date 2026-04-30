using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Workers;
using SharedKernel.Data;
using SharedKernel.Domain;
using Xunit;

namespace OptionsExecutionService.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the outbox pattern.
/// Tests the complete flow: write to outbox → reconciler processes → mark as sent.
/// Phase 1: State Persistence & Idempotency - Task #21
/// </summary>
public sealed class OutboxPatternIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbConnectionFactory _db;
    private readonly OrderOutboxRepository _outboxRepository;

    public OutboxPatternIntegrationTests()
    {
        // Create unique test database
        _dbPath = $"test-outbox-integration-{Guid.NewGuid()}.db";
        _db = new SqliteConnectionFactory(_dbPath);

        // Run migration to create outbox table
        MigrationRunner runner = new(
            _db,
            new LoggerFactory().CreateLogger<MigrationRunner>());

        runner.RunAsync(
            new[] { new OptionsExecutionService.Migrations.AddOrderOutbox006() },
            CancellationToken.None).GetAwaiter().GetResult();

        // Create repository
        _outboxRepository = new OrderOutboxRepository(
            _db,
            new LoggerFactory().CreateLogger<OrderOutboxRepository>());
    }

    public void Dispose()
    {
        // Force SQLite to close all connections
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // Small delay to ensure all file handles are released
        System.Threading.Thread.Sleep(50);

        // Clean up test database
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // File handle not fully released on Windows - ignore and let OS clean up
            }
        }
    }

    /// <summary>
    /// Tests the complete outbox pattern flow: insert → process → mark sent.
    /// </summary>
    [Fact]
    public async Task OutboxPattern_EndToEnd_WritesProcessesAndMarksSent()
    {
        // ============================================================
        // ARRANGE: Insert order intention to outbox
        // ============================================================

        var orderEntry = new OrderOutboxEntry
        {
            OrderId = "order-integration-test-001",
            Operation = "PlaceOrder",
            Payload = "{\"symbol\":\"SPY\",\"action\":\"BUY\",\"quantity\":1}",
            Status = "pending"
        };

        await _outboxRepository.InsertAsync(orderEntry);

        // ============================================================
        // ACT: Simulate reconciler processing
        // ============================================================

        // Get pending entries (simulates what reconciler does)
        var pendingEntries = await _outboxRepository.GetPendingAsync(limit: 10);

        var entry = Assert.Single(pendingEntries);
        Assert.Equal("order-integration-test-001", entry.OrderId);
        Assert.Equal("PlaceOrder", entry.Operation);
        Assert.Equal("pending", entry.Status);

        // Simulate successful processing
        await _outboxRepository.MarkSentAsync(entry.OutboxId);

        // ============================================================
        // ASSERT: Verify entry is no longer pending
        // ============================================================

        var remainingPending = await _outboxRepository.GetPendingAsync(limit: 10);
        Assert.Empty(remainingPending);
    }

    /// <summary>
    /// Tests the failure path: insert → process fails → mark failed.
    /// </summary>
    [Fact]
    public async Task OutboxPattern_EndToEnd_MarksFailedOnError()
    {
        // ============================================================
        // ARRANGE: Insert invalid order intention
        // ============================================================

        var orderEntry = new OrderOutboxEntry
        {
            OrderId = "order-integration-test-002",
            Operation = "PlaceOrder",
            Payload = "{invalid json}",  // This will cause processing to fail
            Status = "pending"
        };

        await _outboxRepository.InsertAsync(orderEntry);

        // ============================================================
        // ACT: Simulate reconciler processing failure
        // ============================================================

        var pendingEntries = await _outboxRepository.GetPendingAsync(limit: 10);
        var entry = pendingEntries[0];

        // Simulate processing failure (invalid JSON)
        try
        {
            System.Text.Json.JsonDocument.Parse(entry.Payload);
            Assert.Fail("Should have thrown JSON exception");
        }
        catch (System.Text.Json.JsonException)
        {
            // Expected - mark as failed
            await _outboxRepository.MarkFailedAsync(entry.OutboxId);
        }

        // ============================================================
        // ASSERT: Verify entry is marked as failed
        // ============================================================

        var remainingPending = await _outboxRepository.GetPendingAsync(limit: 10);
        Assert.Empty(remainingPending);
    }

    /// <summary>
    /// Tests reconciler worker processing multiple entries in batch.
    /// </summary>
    [Fact]
    public async Task OutboxReconciler_ProcessesMultipleEntries()
    {
        // ============================================================
        // ARRANGE: Insert multiple pending entries
        // ============================================================

        for (int i = 1; i <= 5; i++)
        {
            await _outboxRepository.InsertAsync(new OrderOutboxEntry
            {
                OrderId = $"order-batch-{i}",
                Operation = "PlaceOrder",
                Payload = $"{{\"symbol\":\"SPY\",\"quantity\":{i}}}",
                Status = "pending"
            });
        }

        // ============================================================
        // ACT: Process all entries
        // ============================================================

        var pendingEntries = await _outboxRepository.GetPendingAsync(limit: 100);
        Assert.Equal(5, pendingEntries.Count);

        // Mark all as sent
        foreach (var entry in pendingEntries)
        {
            await _outboxRepository.MarkSentAsync(entry.OutboxId);
        }

        // ============================================================
        // ASSERT: Verify all processed
        // ============================================================

        var remainingPending = await _outboxRepository.GetPendingAsync(limit: 100);
        Assert.Empty(remainingPending);
    }

    /// <summary>
    /// Tests that reconciler respects the limit parameter (processes in batches).
    /// </summary>
    [Fact]
    public async Task OutboxReconciler_RespectsLimitParameter()
    {
        // ============================================================
        // ARRANGE: Insert 10 entries
        // ============================================================

        for (int i = 1; i <= 10; i++)
        {
            await _outboxRepository.InsertAsync(new OrderOutboxEntry
            {
                OrderId = $"order-limit-{i}",
                Operation = "PlaceOrder",
                Payload = "{}",
                Status = "pending"
            });
        }

        // ============================================================
        // ACT: Get with limit of 5
        // ============================================================

        var firstBatch = await _outboxRepository.GetPendingAsync(limit: 5);

        // ============================================================
        // ASSERT: Verify only 5 returned
        // ============================================================

        Assert.Equal(5, firstBatch.Count);

        // Process first batch
        foreach (var entry in firstBatch)
        {
            await _outboxRepository.MarkSentAsync(entry.OutboxId);
        }

        // Get second batch
        var secondBatch = await _outboxRepository.GetPendingAsync(limit: 5);
        Assert.Equal(5, secondBatch.Count);
    }

    /// <summary>
    /// Tests idempotency: processing same entry multiple times should be safe.
    /// </summary>
    [Fact]
    public async Task OutboxPattern_Idempotent_MarkSentMultipleTimes()
    {
        // ============================================================
        // ARRANGE: Insert entry and process it
        // ============================================================

        var entry = new OrderOutboxEntry
        {
            OrderId = "order-idempotent-001",
            Operation = "PlaceOrder",
            Payload = "{}",
            Status = "pending"
        };

        await _outboxRepository.InsertAsync(entry);

        var pending = await _outboxRepository.GetPendingAsync(limit: 10);
        var outboxId = pending[0].OutboxId;

        // ============================================================
        // ACT: Mark as sent multiple times
        // ============================================================

        await _outboxRepository.MarkSentAsync(outboxId);
        await _outboxRepository.MarkSentAsync(outboxId);  // Second call should be idempotent
        await _outboxRepository.MarkSentAsync(outboxId);  // Third call should be idempotent

        // ============================================================
        // ASSERT: No errors thrown, entry still marked as sent
        // ============================================================

        var remainingPending = await _outboxRepository.GetPendingAsync(limit: 10);
        Assert.Empty(remainingPending);
    }
}
