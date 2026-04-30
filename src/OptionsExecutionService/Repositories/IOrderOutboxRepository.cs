namespace OptionsExecutionService.Repositories;

/// <summary>
/// Repository for managing outbox entries for atomic broker+DB operations.
/// Implements outbox pattern for crash recovery and idempotency.
/// Phase 1: State Persistence & Idempotency
/// </summary>
public interface IOrderOutboxRepository
{
    /// <summary>
    /// Inserts a new outbox entry to record an order operation intention.
    /// This is the first step of the outbox pattern: Write intention before execution.
    /// </summary>
    /// <param name="entry">Outbox entry containing operation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The inserted entry with OutboxId populated.</returns>
    Task<OrderOutboxEntry> InsertAsync(OrderOutboxEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Retrieves pending outbox entries (status = 'pending') for processing.
    /// Used by OutboxReconcilerWorker to find operations that need execution.
    /// </summary>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of pending outbox entries ordered by CreatedAt (oldest first).</returns>
    Task<IReadOnlyList<OrderOutboxEntry>> GetPendingAsync(int limit, CancellationToken ct = default);

    /// <summary>
    /// Marks an outbox entry as successfully sent/executed.
    /// This is the final step of the outbox pattern: Mark complete after execution.
    /// Updates status to 'sent' and sets SentAt timestamp.
    /// </summary>
    /// <param name="outboxId">Outbox entry ID to mark as sent.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkSentAsync(long outboxId, CancellationToken ct = default);

    /// <summary>
    /// Marks an outbox entry as failed after execution error.
    /// Updates status to 'failed' for manual review/retry.
    /// </summary>
    /// <param name="outboxId">Outbox entry ID to mark as failed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkFailedAsync(long outboxId, CancellationToken ct = default);
}
