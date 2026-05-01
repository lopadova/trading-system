using SharedKernel.Safety;

namespace OptionsExecutionService.Services;

/// <summary>
/// Circuit breaker for order placement failures.
/// Singleton service that persists state across worker cycles and DI scopes.
/// Phase 2: Shared safety state P1 - Task RM-05
/// </summary>
public interface IOrderCircuitBreaker
{
    /// <summary>
    /// Checks if the circuit breaker is currently open.
    /// Auto-resets if cooldown period has expired.
    /// </summary>
    bool IsOpen();

    /// <summary>
    /// Records a broker failure and potentially trips the breaker.
    /// Network errors are logged but don't count toward threshold.
    /// </summary>
    Task RecordFailureAsync(IbkrFailureType failureType, int failureCount, CancellationToken ct = default);

    /// <summary>
    /// Manually resets the circuit breaker (admin operation).
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets the current circuit breaker state for observability.
    /// </summary>
    CircuitBreakerState GetState();
}

/// <summary>
/// Circuit breaker state snapshot for observability.
/// </summary>
public sealed record CircuitBreakerState
{
    public bool IsOpen { get; init; }
    public DateTime? TrippedAt { get; init; }
    public string? Reason { get; init; }
}
