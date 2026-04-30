using Microsoft.Extensions.Logging;
using OptionsExecutionService.Repositories;
using SharedKernel.Data;
using SharedKernel.Domain;
using Xunit;

namespace OptionsExecutionService.Tests.Repositories;

/// <summary>
/// Tests for OrderOutboxRepository - outbox pattern for atomic broker+DB operations.
/// Ensures crash recovery by persisting order intentions before execution.
/// Phase 1: State Persistence & Idempotency
/// </summary>
public sealed class OrderOutboxRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbConnectionFactory _db;
    private readonly OrderOutboxRepository _repository;

    public OrderOutboxRepositoryTests()
    {
        // Create unique test database
        _dbPath = $"test-order-outbox-{Guid.NewGuid()}.db";
        _db = new SqliteConnectionFactory(_dbPath);

        // NOTE: Migration will be created in Task #14
        // For now, repository methods will throw NotImplementedException (RED phase)

        _repository = new OrderOutboxRepository(
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
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task InsertAsync_CreatesNewOutboxEntry()
    {
        // Arrange
        var entry = new OrderOutboxEntry
        {
            OrderId = "order-123",
            Operation = "PlaceOrder",
            Payload = "{\"symbol\":\"SPY\",\"action\":\"BUY\",\"quantity\":1}",
            Status = "pending"
        };

        // Act
        await _repository.InsertAsync(entry);

        // Assert
        var retrieved = await _repository.GetPendingAsync(limit: 10);
        var single = Assert.Single(retrieved);
        Assert.Equal(entry.OrderId, single.OrderId);
        Assert.Equal(entry.Operation, single.Operation);
        Assert.Equal(entry.Payload, single.Payload);
        Assert.Equal(entry.Status, single.Status);
        Assert.NotEqual(0, single.OutboxId); // Auto-generated
        Assert.NotNull(single.CreatedAt);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingEntries()
    {
        // Arrange
        await _repository.InsertAsync(new OrderOutboxEntry
        {
            OrderId = "order-1",
            Operation = "PlaceOrder",
            Payload = "{}",
            Status = "pending"
        });

        await _repository.InsertAsync(new OrderOutboxEntry
        {
            OrderId = "order-2",
            Operation = "CancelOrder",
            Payload = "{}",
            Status = "sent"
        });

        // Act
        var pending = await _repository.GetPendingAsync(limit: 10);

        // Assert
        var single = Assert.Single(pending);
        Assert.Equal("order-1", single.OrderId);
    }

    [Fact]
    public async Task MarkSentAsync_UpdatesStatusAndTimestamp()
    {
        // Arrange
        await _repository.InsertAsync(new OrderOutboxEntry
        {
            OrderId = "order-123",
            Operation = "PlaceOrder",
            Payload = "{}",
            Status = "pending"
        });

        var pending = await _repository.GetPendingAsync(limit: 10);
        var outboxId = pending.First().OutboxId;

        // Act
        await _repository.MarkSentAsync(outboxId);

        // Assert
        var afterMark = await _repository.GetPendingAsync(limit: 10);
        Assert.Empty(afterMark);
    }
}
