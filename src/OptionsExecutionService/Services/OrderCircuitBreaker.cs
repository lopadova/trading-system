using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using SharedKernel.Safety;

namespace OptionsExecutionService.Services;

/// <summary>
/// Thread-safe singleton circuit breaker for order placement.
/// Persists state across worker cycles to prevent cascading failures.
/// Phase 2: Shared safety state P1 - Task RM-05
/// </summary>
public sealed class OrderCircuitBreaker : IOrderCircuitBreaker
{
    private readonly OrderSafetyConfig _safetyConfig;
    private readonly ILogger<OrderCircuitBreaker> _logger;

    // Circuit breaker state - lock-protected for thread safety
    private readonly Lock _lock = new();
    private bool _isOpen = false;
    private DateTime? _trippedAt = null;
    private string? _tripReason = null;

    public OrderCircuitBreaker(
        OrderSafetyConfig safetyConfig,
        ILogger<OrderCircuitBreaker> logger)
    {
        _safetyConfig = safetyConfig ?? throw new ArgumentNullException(nameof(safetyConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsOpen()
    {
        lock (_lock)
        {
            // If circuit breaker is open, check if cooldown has expired
            if (_isOpen && _trippedAt is not null)
            {
                DateTime cooldownEnd = _trippedAt.Value
                    .AddMinutes(_safetyConfig.CircuitBreakerCooldownMinutes);

                if (DateTime.UtcNow >= cooldownEnd)
                {
                    _logger.LogInformation(
                        "Circuit breaker cooldown expired (tripped at {TrippedAt}, cooldown {Cooldown}min). Auto-resetting.",
                        _trippedAt.Value.ToString("O"),
                        _safetyConfig.CircuitBreakerCooldownMinutes);

                    _isOpen = false;
                    _trippedAt = null;
                    _tripReason = null;
                }
            }

            return _isOpen;
        }
    }

    public Task RecordFailureAsync(IbkrFailureType failureType, int failureCount, CancellationToken ct = default)
    {
        _ = ct; // Method is synchronous but kept async for interface consistency

        if (failureType == IbkrFailureType.NetworkError)
        {
            // Transport-level fault: intentionally ignored for breaker math
            _logger.LogInformation("Network-class IBKR failure observed — NOT counted toward circuit breaker");
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Failure recorded (type={Type}). {Count} failures in last {Minutes} minutes",
            failureType, failureCount, _safetyConfig.CircuitBreakerWindowMinutes);

        if (failureCount >= _safetyConfig.CircuitBreakerFailureThreshold)
        {
            lock (_lock)
            {
                if (!_isOpen)
                {
                    _isOpen = true;
                    _trippedAt = DateTime.UtcNow;
                    _tripReason = $"{failureCount} {failureType} failures in {_safetyConfig.CircuitBreakerWindowMinutes}min (threshold: {_safetyConfig.CircuitBreakerFailureThreshold})";

                    _logger.LogCritical(
                        "CIRCUIT BREAKER TRIPPED: {Reason}. All order placement blocked for {Cooldown} minutes.",
                        _tripReason,
                        _safetyConfig.CircuitBreakerCooldownMinutes);
                }
            }
        }

        return Task.CompletedTask;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _isOpen = false;
            _trippedAt = null;
            _tripReason = null;
            _logger.LogWarning("Circuit breaker manually reset");
        }
    }

    public CircuitBreakerState GetState()
    {
        lock (_lock)
        {
            return new CircuitBreakerState
            {
                IsOpen = _isOpen,
                TrippedAt = _trippedAt,
                Reason = _tripReason
            };
        }
    }
}
