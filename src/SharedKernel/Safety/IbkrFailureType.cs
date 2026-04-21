namespace SharedKernel.Safety;

/// <summary>
/// Classification of an IBKR-side failure. Used by the circuit breaker to
/// distinguish signal from noise:
/// <list type="bullet">
///   <item><description><see cref="NetworkError"/> — transient transport issue (socket timeout, disconnect,
///   "not connected"). Does NOT count toward the circuit-breaker budget —
///   reconnect/backoff handles it; tripping on these would create a DoS loop.</description></item>
///   <item><description><see cref="BrokerReject"/> — broker rejected the order (insufficient margin,
///   invalid contract, outside RTH, etc.). DOES count — this is a sign of a
///   misbehaving strategy and is exactly what the breaker exists to contain.</description></item>
///   <item><description><see cref="Unknown"/> — reserved for cases where classification is ambiguous.
///   Treated as a broker reject by the breaker (fail-closed) to avoid a
///   silent "infinite retries" regression if upstream callers forget to
///   classify an error.</description></item>
/// </list>
/// </summary>
public enum IbkrFailureType
{
    /// <summary>
    /// Transient transport-level problem (disconnect, timeout, connection refused).
    /// Does NOT count toward the circuit-breaker failure budget.
    /// </summary>
    NetworkError = 0,

    /// <summary>
    /// Broker explicitly rejected the order (insufficient margin, invalid
    /// contract, outside market hours, etc.). Counts toward the breaker.
    /// </summary>
    BrokerReject = 1,

    /// <summary>
    /// Ambiguous / unclassified failure. Counted against the breaker budget
    /// by default so forgetting to classify an error can never silently bypass
    /// the safety net.
    /// </summary>
    Unknown = 2,
}
