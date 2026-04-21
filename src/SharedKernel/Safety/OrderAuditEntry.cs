using SharedKernel.Domain;

namespace SharedKernel.Safety;

/// <summary>
/// Canonical audit outcomes. Extend this enum rather than scattering magic
/// strings throughout the codebase; it keeps Worker-side filter values and the
/// .NET write-path vocabulary in sync. The <see cref="ToWire"/> extension
/// returns the lowercase snake_case value used in <c>OrderAuditEntry.Outcome</c>
/// and the D1 <c>order_audit_log.outcome</c> column.
/// </summary>
public enum AuditOutcome
{
    /// <summary>Order was submitted to IBKR (pre-fill).</summary>
    Placed,

    /// <summary>Order was confirmed filled by IBKR.</summary>
    Filled,

    /// <summary>Blocked at Gate #1 — semaphore RED.</summary>
    RejectedSemaphore,

    /// <summary>Blocked at Gate #2 — trading-paused flag set by DailyPnLWatcher.</summary>
    RejectedPnlPause,

    /// <summary>Blocked at Gate #3 — position-size validator.</summary>
    RejectedMaxSize,

    /// <summary>Blocked at Gate #3 — position-value validator (USD cap).</summary>
    RejectedMaxValue,

    /// <summary>Blocked at Gate #3 — risk-pct-of-account validator.</summary>
    RejectedMaxRisk,

    /// <summary>Blocked at Gate #3 — account-balance-below-minimum validator.</summary>
    RejectedMinBalance,

    /// <summary>Blocked at Gate #4 — circuit breaker open.</summary>
    RejectedBreaker,

    /// <summary>Rejected by IBKR (insufficient margin, invalid contract, etc.).</summary>
    RejectedBroker,

    /// <summary>Any unclassified error during placement (IO exception, timeout).</summary>
    Error
}

/// <summary>
/// Extension helpers to keep <see cref="AuditOutcome"/> serialization centralized.
/// </summary>
public static class AuditOutcomeExtensions
{
    /// <summary>Returns the snake_case wire value expected by the Worker schema.</summary>
    public static string ToWire(this AuditOutcome outcome) => outcome switch
    {
        AuditOutcome.Placed => "placed",
        AuditOutcome.Filled => "filled",
        AuditOutcome.RejectedSemaphore => "rejected_semaphore",
        AuditOutcome.RejectedPnlPause => "rejected_pnl_pause",
        AuditOutcome.RejectedMaxSize => "rejected_max_size",
        AuditOutcome.RejectedMaxValue => "rejected_max_value",
        AuditOutcome.RejectedMaxRisk => "rejected_max_risk",
        AuditOutcome.RejectedMinBalance => "rejected_min_balance",
        AuditOutcome.RejectedBreaker => "rejected_breaker",
        AuditOutcome.RejectedBroker => "rejected_broker",
        AuditOutcome.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown AuditOutcome")
    };

    /// <summary>Returns the snake_case wire value for a <see cref="SemaphoreStatus"/>.</summary>
    public static string ToWire(this SemaphoreStatus status) => status switch
    {
        SemaphoreStatus.Green => "green",
        SemaphoreStatus.Orange => "orange",
        SemaphoreStatus.Red => "red",
        _ => "unknown"
    };
}

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

    // ------------------------------------------------------------------
    // Factory helpers — keep the call-sites inside OrderPlacer small and
    // readable. Each factory fills the fields that are always-known at the
    // point of the decision (from OrderRequest + the gate observation) and
    // leaves per-outcome specifics to the caller (OrderId, Price, DetailsJson).
    // ------------------------------------------------------------------

    /// <summary>
    /// Build an audit row for an order that was blocked BEFORE it reached IBKR
    /// (gate/validator/breaker rejection). <paramref name="overrideReason"/>
    /// doubles as a free-text rejection rationale (e.g. "semaphore-red",
    /// "pnl-paused"). <see cref="OrderId"/> stays null since no IBKR call was made.
    /// </summary>
    public static OrderAuditEntry Rejected(
        OrderRequest request,
        SemaphoreStatus semaphore,
        AuditOutcome outcome,
        string rejectionReason)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        return new OrderAuditEntry
        {
            AuditId = Guid.NewGuid().ToString(),
            OrderId = null,
            Ts = DateTimeOffset.UtcNow.ToString("O"),
            Actor = "system",
            StrategyId = request.StrategyName,
            ContractSymbol = request.ContractSymbol,
            Side = request.Side.ToString().ToUpperInvariant(),
            Quantity = request.Quantity,
            Price = null,
            SemaphoreStatus = semaphore.ToWire(),
            Outcome = outcome.ToWire(),
            OverrideReason = rejectionReason,
            DetailsJson = null
        };
    }

    /// <summary>
    /// Build an audit row for an order that was accepted and submitted to IBKR
    /// (pre-fill). Caller provides <paramref name="orderId"/> so later fill/
    /// error rows can be correlated.
    /// </summary>
    public static OrderAuditEntry Placed(
        OrderRequest request,
        SemaphoreStatus semaphore,
        string orderId)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("orderId required", nameof(orderId));
        }
        // NOTE on Price: the field is documented + filtered by consumers as a FILL
        // price. An order that has only been placed has no fill yet, so we leave
        // Price null here. The submission/limit price lives in DetailsJson under
        // "limitPrice" for diagnostics without polluting the fill-price semantics.
        string? detailsJson = request.LimitPrice.HasValue
            ? $"{{\"limitPrice\":{request.LimitPrice.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture)}}}"
            : null;
        return new OrderAuditEntry
        {
            AuditId = Guid.NewGuid().ToString(),
            OrderId = orderId,
            Ts = DateTimeOffset.UtcNow.ToString("O"),
            Actor = "system",
            StrategyId = request.StrategyName,
            ContractSymbol = request.ContractSymbol,
            Side = request.Side.ToString().ToUpperInvariant(),
            Quantity = request.Quantity,
            Price = null,
            SemaphoreStatus = semaphore.ToWire(),
            Outcome = AuditOutcome.Placed.ToWire(),
            OverrideReason = null,
            DetailsJson = detailsJson
        };
    }

    /// <summary>
    /// Build an audit row for a broker-side rejection (IBKR returned false
    /// or threw). <paramref name="errorMessage"/> is stored under OverrideReason
    /// for searchability.
    /// </summary>
    public static OrderAuditEntry BrokerRejected(
        OrderRequest request,
        SemaphoreStatus semaphore,
        string? orderId,
        string errorMessage)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        return new OrderAuditEntry
        {
            AuditId = Guid.NewGuid().ToString(),
            OrderId = orderId,
            Ts = DateTimeOffset.UtcNow.ToString("O"),
            Actor = "system",
            StrategyId = request.StrategyName,
            ContractSymbol = request.ContractSymbol,
            Side = request.Side.ToString().ToUpperInvariant(),
            Quantity = request.Quantity,
            Price = null,
            SemaphoreStatus = semaphore.ToWire(),
            Outcome = AuditOutcome.RejectedBroker.ToWire(),
            OverrideReason = errorMessage,
            DetailsJson = null
        };
    }

    /// <summary>
    /// Build an audit row for an unclassified error (exception in the pipeline).
    /// </summary>
    public static OrderAuditEntry ErrorDuring(
        OrderRequest request,
        SemaphoreStatus semaphore,
        string? orderId,
        string errorMessage)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        return new OrderAuditEntry
        {
            AuditId = Guid.NewGuid().ToString(),
            OrderId = orderId,
            Ts = DateTimeOffset.UtcNow.ToString("O"),
            Actor = "system",
            StrategyId = request.StrategyName,
            ContractSymbol = request.ContractSymbol,
            Side = request.Side.ToString().ToUpperInvariant(),
            Quantity = request.Quantity,
            Price = null,
            SemaphoreStatus = semaphore.ToWire(),
            Outcome = AuditOutcome.Error.ToWire(),
            OverrideReason = errorMessage,
            DetailsJson = null
        };
    }
}
