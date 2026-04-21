namespace SharedKernel.Safety;

/// <summary>
/// Immutable record representing a single row in the order audit log — the
/// single source of truth for "did an order leave the machine or was it
/// blocked?". Every order attempt (success, broker reject, gate reject,
/// circuit-breaker reject) writes exactly ONE row. See
/// infra/cloudflare/worker/migrations/0009_order_audit_log.sql for the
/// receiving schema.
/// <para>
/// Field naming on the Worker side uses snake_case. Serialization to the
/// ingest envelope is responsibility of the sink implementation, which maps
/// these PascalCase properties to the Worker payload contract.
/// </para>
/// </summary>
public sealed record OrderAuditEntry
{
    /// <summary>Unique GUID identifying this audit row. Primary key.</summary>
    public string AuditId { get; init; } = string.Empty;

    /// <summary>Internal order id (GUID from order_tracking). Null for orders blocked before reaching IBKR.</summary>
    public string? OrderId { get; init; }

    /// <summary>ISO-8601 UTC timestamp of the event.</summary>
    public string Ts { get; init; } = string.Empty;

    /// <summary>Who initiated the action: "system", "operator-override", or "campaign:{id}".</summary>
    public string Actor { get; init; } = "system";

    /// <summary>Strategy that triggered this order (from <c>OrderRequest.StrategyName</c>).</summary>
    public string? StrategyId { get; init; }

    /// <summary>OCC-formatted option contract symbol.</summary>
    public string ContractSymbol { get; init; } = string.Empty;

    /// <summary>"BUY" or "SELL" (uppercase canonical form per Worker schema).</summary>
    public string Side { get; init; } = string.Empty;

    /// <summary>Number of contracts.</summary>
    public int Quantity { get; init; }

    /// <summary>Fill price (set once the order has filled). Null for pending/blocked rows.</summary>
    public decimal? Price { get; init; }

    /// <summary>Semaphore value as observed at the time of the decision: "green", "orange", "red".</summary>
    public string SemaphoreStatus { get; init; } = "unknown";

    /// <summary>
    /// Outcome classifier — drives dashboard filters and incident queries.
    /// Allowed values: <c>placed</c>, <c>filled</c>, <c>rejected_semaphore</c>,
    /// <c>rejected_breaker</c>, <c>rejected_max_size</c>, <c>rejected_broker</c>,
    /// <c>rejected_pnl_pause</c>, <c>error</c>.
    /// </summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>Free-text rationale, populated when a semaphore override was active or a manual action was taken.</summary>
    public string? OverrideReason { get; init; }

    /// <summary>Arbitrary JSON metadata (e.g. limit price, stop price, error message).</summary>
    public string? DetailsJson { get; init; }
}
