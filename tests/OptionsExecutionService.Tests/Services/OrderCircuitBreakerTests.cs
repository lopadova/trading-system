using Microsoft.Extensions.Logging;
using Moq;
using OptionsExecutionService.Services;
using SharedKernel.Domain;
using SharedKernel.Safety;
using Xunit;

namespace OptionsExecutionService.Tests.Services;

/// <summary>
/// Tests for OrderCircuitBreaker - singleton circuit breaker that persists across DI scopes.
/// Phase 2: Shared safety state P1 - Task RM-05
/// </summary>
public sealed class OrderCircuitBreakerTests
{
    /// <summary>
    /// Verifies that circuit breaker opens when threshold is reached.
    /// </summary>
    [Fact]
    public async Task RecordFailureAsync_TripsBreaker_WhenThresholdReached()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OrderCircuitBreaker>>();
        var safetyConfig = new OrderSafetyConfig
        {
            TradingMode = TradingMode.Paper,
            CircuitBreakerFailureThreshold = 3,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 120
        };

        var breaker = new OrderCircuitBreaker(safetyConfig, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        // Record failures below threshold
        await breaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 1, CancellationToken.None);
        Assert.False(breaker.IsOpen(), "Breaker should be closed with 1 failure");

        await breaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 2, CancellationToken.None);
        Assert.False(breaker.IsOpen(), "Breaker should be closed with 2 failures");

        // Record failure that reaches threshold
        await breaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 3, CancellationToken.None);

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.True(breaker.IsOpen(), "Breaker should be open with 3 failures (threshold: 3)");

        CircuitBreakerState state = breaker.GetState();
        Assert.True(state.IsOpen);
        Assert.NotNull(state.TrippedAt);
        Assert.NotNull(state.Reason);
        Assert.Contains("3", state.Reason);
        Assert.Contains("BrokerReject", state.Reason);
    }

    /// <summary>
    /// Verifies that network errors do NOT trip the circuit breaker.
    /// </summary>
    [Fact]
    public async Task RecordFailureAsync_IgnoresNetworkErrors()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OrderCircuitBreaker>>();
        var safetyConfig = new OrderSafetyConfig
        {
            TradingMode = TradingMode.Paper,
            CircuitBreakerFailureThreshold = 3,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 120
        };

        var breaker = new OrderCircuitBreaker(safetyConfig, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        // Record 10 network errors (should all be ignored)
        for (int i = 0; i < 10; i++)
        {
            await breaker.RecordFailureAsync(IbkrFailureType.NetworkError, i + 1, CancellationToken.None);
        }

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.False(breaker.IsOpen(), "Breaker should NOT open for network errors");

        CircuitBreakerState state = breaker.GetState();
        Assert.False(state.IsOpen);
        Assert.Null(state.TrippedAt);
        Assert.Null(state.Reason);
    }

    /// <summary>
    /// Verifies that circuit breaker auto-resets after cooldown period.
    /// </summary>
    [Fact]
    public async Task IsOpen_AutoResets_AfterCooldown()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OrderCircuitBreaker>>();
        var safetyConfig = new OrderSafetyConfig
        {
            TradingMode = TradingMode.Paper,
            CircuitBreakerFailureThreshold = 1,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 0 // 0 minutes = instant reset for testing
        };

        var breaker = new OrderCircuitBreaker(safetyConfig, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        // Trip the breaker
        await breaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 1, CancellationToken.None);

        // Check state WITHOUT calling IsOpen() (which would auto-reset with cooldown=0)
        CircuitBreakerState stateAfterTrip = breaker.GetState();
        Assert.True(stateAfterTrip.IsOpen, "Breaker should be open after trip");
        Assert.NotNull(stateAfterTrip.TrippedAt);

        // Wait for cooldown (1ms to ensure UtcNow advances beyond TrippedAt)
        await Task.Delay(1);

        // Now call IsOpen() - should auto-reset because cooldown=0 has expired
        bool isOpen = breaker.IsOpen();

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.False(isOpen, "Breaker should auto-reset after cooldown period");

        CircuitBreakerState state = breaker.GetState();
        Assert.False(state.IsOpen);
        Assert.Null(state.TrippedAt);
        Assert.Null(state.Reason);
    }

    /// <summary>
    /// Verifies that manual reset clears circuit breaker state.
    /// </summary>
    [Fact]
    public async Task Reset_ClearsState()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OrderCircuitBreaker>>();
        var safetyConfig = new OrderSafetyConfig
        {
            TradingMode = TradingMode.Paper,
            CircuitBreakerFailureThreshold = 1,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 120
        };

        var breaker = new OrderCircuitBreaker(safetyConfig, mockLogger.Object);

        // Trip the breaker
        await breaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 1, CancellationToken.None);
        Assert.True(breaker.IsOpen(), "Breaker should be open after trip");

        // ============================================================
        // ACT
        // ============================================================

        breaker.Reset();

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.False(breaker.IsOpen(), "Breaker should be closed after manual reset");

        CircuitBreakerState state = breaker.GetState();
        Assert.False(state.IsOpen);
        Assert.Null(state.TrippedAt);
        Assert.Null(state.Reason);
    }

    /// <summary>
    /// CRITICAL TEST: Verifies that breaker state persists across multiple DI scopes.
    /// This is the key requirement of RM-05: singleton breaker must persist when
    /// OrderPlacer (scoped) is recreated in each worker cycle.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_PersistsAcrossScopes()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OrderCircuitBreaker>>();
        var safetyConfig = new OrderSafetyConfig
        {
            TradingMode = TradingMode.Paper,
            CircuitBreakerFailureThreshold = 2,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 120
        };

        // Single singleton instance shared across scopes
        var sharedBreaker = new OrderCircuitBreaker(safetyConfig, mockLogger.Object);

        // ============================================================
        // ACT & ASSERT
        // ============================================================

        // Simulate SCOPE 1 (first worker cycle)
        {
            // Record 1 failure
            await sharedBreaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 1, CancellationToken.None);
            Assert.False(sharedBreaker.IsOpen(), "Breaker should be closed after 1 failure (threshold: 2)");
        }
        // End of scope 1 - in real app, scoped OrderPlacer would be disposed here

        // Simulate SCOPE 2 (second worker cycle)
        {
            // Same singleton breaker instance sees previous state
            // Record 2nd failure (should trip)
            await sharedBreaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 2, CancellationToken.None);
            Assert.True(sharedBreaker.IsOpen(), "Breaker should be open after 2 failures (threshold: 2)");

            CircuitBreakerState state = sharedBreaker.GetState();
            Assert.True(state.IsOpen);
            Assert.NotNull(state.TrippedAt);
        }
        // End of scope 2

        // Simulate SCOPE 3 (third worker cycle)
        {
            // Same singleton breaker STILL sees tripped state
            Assert.True(sharedBreaker.IsOpen(), "Breaker should STILL be open in scope 3");

            CircuitBreakerState state = sharedBreaker.GetState();
            Assert.True(state.IsOpen);
            Assert.NotNull(state.TrippedAt);
        }
        // End of scope 3

        // This test verifies the fix for RM-05: before Phase 2, circuit breaker
        // state was in scoped OrderPlacer and reset every worker cycle.
        // After Phase 2, breaker is singleton and persists across all scopes.
    }

    /// <summary>
    /// Verifies that breaker doesn't trip multiple times for same threshold breach.
    /// </summary>
    [Fact]
    public async Task RecordFailureAsync_TripsOnce_NotMultipleTimes()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<OrderCircuitBreaker>>();
        var safetyConfig = new OrderSafetyConfig
        {
            TradingMode = TradingMode.Paper,
            CircuitBreakerFailureThreshold = 3,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 120
        };

        var breaker = new OrderCircuitBreaker(safetyConfig, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        // Trip the breaker
        await breaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 3, CancellationToken.None);
        Assert.True(breaker.IsOpen());

        DateTime? firstTrippedAt = breaker.GetState().TrippedAt;

        // Record more failures while breaker is already open
        await Task.Delay(10); // Ensure time advances
        await breaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 4, CancellationToken.None);
        await breaker.RecordFailureAsync(IbkrFailureType.BrokerReject, 5, CancellationToken.None);

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.True(breaker.IsOpen());

        CircuitBreakerState state = breaker.GetState();
        Assert.Equal(firstTrippedAt, state.TrippedAt);
        // TrippedAt should NOT change - breaker tripped once and stays tripped
    }
}
