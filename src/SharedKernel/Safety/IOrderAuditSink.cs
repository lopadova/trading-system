namespace SharedKernel.Safety;

/// <summary>
/// Contract for persisting order-audit rows. Implementations MUST NOT throw
/// from <see cref="WriteAsync"/> — audit writes live inside the order-placement
/// hot path and blowing up there would defeat the very safety gate that
/// produced the audit row in the first place. Errors MUST be logged and
/// swallowed; the audit sink is best-effort from the caller's point of view.
/// The local SQLite mirror (<c>order_audit_log_local</c>) is the authoritative
/// record until the outbox ship-out succeeds.
/// </summary>
public interface IOrderAuditSink
{
    /// <summary>
    /// Durably stores the audit entry (local SQLite) and queues it for
    /// shipping to the Worker. Safe to call from the order-placement pipeline.
    /// </summary>
    Task WriteAsync(OrderAuditEntry entry, CancellationToken ct);
}
