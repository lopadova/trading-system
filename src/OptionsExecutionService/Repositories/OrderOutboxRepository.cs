using Microsoft.Extensions.Logging;
using SharedKernel.Data;

namespace OptionsExecutionService.Repositories;

/// <summary>
/// Repository for managing outbox entries for atomic broker+DB operations.
/// Implements outbox pattern for crash recovery and idempotency.
/// Phase 1: State Persistence & Idempotency
/// </summary>
public sealed class OrderOutboxRepository : IOrderOutboxRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<OrderOutboxRepository> _logger;

    public OrderOutboxRepository(IDbConnectionFactory db, ILogger<OrderOutboxRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<OrderOutboxEntry> InsertAsync(OrderOutboxEntry entry, CancellationToken ct = default)
    {
        throw new NotImplementedException("Task #13: RED phase - method not yet implemented");
    }

    public Task<IReadOnlyList<OrderOutboxEntry>> GetPendingAsync(int limit, CancellationToken ct = default)
    {
        throw new NotImplementedException("Task #13: RED phase - method not yet implemented");
    }

    public Task MarkSentAsync(long outboxId, CancellationToken ct = default)
    {
        throw new NotImplementedException("Task #13: RED phase - method not yet implemented");
    }

    public Task MarkFailedAsync(long outboxId, CancellationToken ct = default)
    {
        throw new NotImplementedException("Task #13: RED phase - method not yet implemented");
    }
}
