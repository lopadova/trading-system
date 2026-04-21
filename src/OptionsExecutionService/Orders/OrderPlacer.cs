using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Services;
using SharedKernel.Configuration;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using SharedKernel.Observability;
using SharedKernel.Safety;

namespace OptionsExecutionService.Orders;

/// <summary>
/// Service for placing and tracking orders with comprehensive safety validations.
/// Implements circuit breaker pattern to prevent cascading failures.
/// <para>
/// Gate pipeline (Phase 7.4):
/// </para>
/// <list type="number">
///   <item><description>Gate #1 — <see cref="SemaphoreGate"/>: fail-closed composite market indicator. Overridable via <c>Safety:OverrideSemaphore</c> (loudly).</description></item>
///   <item><description>Gate #2 — <see cref="ISafetyFlagStore"/> <c>trading_paused</c>: set by DailyPnLWatcher when daily drawdown exceeds the budget.</description></item>
///   <item><description>Gate #3 — Request validation (<see cref="OrderRequest.Validate"/>).</description></item>
///   <item><description>Gate #4 — Per-order safety validators (size, value, account balance, risk-pct).</description></item>
///   <item><description>Gate #5 — Circuit breaker (see <see cref="RecordFailureAsync"/>). Classified via <see cref="IbkrFailureType"/> so network noise doesn't trip the breaker.</description></item>
/// </list>
/// Every gate outcome writes exactly one <see cref="OrderAuditEntry"/> so the
/// dashboard's order-audit view is the single source of truth for "was an
/// order placed".
/// </summary>
public sealed class OrderPlacer : IOrderPlacer
{
    private readonly IIbkrClient _ibkrClient;
    private readonly IOrderTrackingRepository _orderRepo;
    private readonly OrderSafetyConfig _safetyConfig;
    private readonly SemaphoreGate _semaphoreGate;
    private readonly ISafetyFlagStore _flagStore;
    private readonly IOrderAuditSink _auditSink;
    private readonly IAlerter _alerter;
    private readonly SafetyOptions _safetyOptions;
    private readonly ILogger<OrderPlacer> _logger;

    // Circuit breaker state. Lock-protected; only Reject-class failures count.
    private readonly Lock _circuitLock = new();
    private bool _circuitBreakerOpen = false;
    private DateTime? _circuitBreakerTrippedAt = null;

    // Account balance cache (updated periodically by background service)
    private decimal _cachedAccountBalance = 0m;
    private readonly Lock _balanceLock = new();

    public OrderPlacer(
        IIbkrClient ibkrClient,
        IOrderTrackingRepository orderRepo,
        OrderSafetyConfig safetyConfig,
        SemaphoreGate semaphoreGate,
        ISafetyFlagStore flagStore,
        IOrderAuditSink auditSink,
        IAlerter alerter,
        IOptions<SafetyOptions> safetyOptions,
        ILogger<OrderPlacer> logger)
    {
        _ibkrClient = ibkrClient ?? throw new ArgumentNullException(nameof(ibkrClient));
        _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
        _safetyConfig = safetyConfig ?? throw new ArgumentNullException(nameof(safetyConfig));
        _semaphoreGate = semaphoreGate ?? throw new ArgumentNullException(nameof(semaphoreGate));
        _flagStore = flagStore ?? throw new ArgumentNullException(nameof(flagStore));
        _auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
        _alerter = alerter ?? throw new ArgumentNullException(nameof(alerter));
        _safetyOptions = safetyOptions?.Value ?? throw new ArgumentNullException(nameof(safetyOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate safety config at startup
        _safetyConfig.Validate();

        _logger.LogInformation(
            "OrderPlacer initialized. Mode: {Mode}, MaxSize: {MaxSize}, CircuitBreaker: {Threshold} failures in {Window}min, SemaphoreOverride: {Override}",
            _safetyConfig.TradingMode, _safetyConfig.MaxPositionSize,
            _safetyConfig.CircuitBreakerFailureThreshold, _safetyConfig.CircuitBreakerWindowMinutes,
            _safetyOptions.OverrideSemaphore);
    }

    public async Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Snapshot semaphore state once per call. All audit rows for this attempt
        // record the SAME semaphore value — otherwise the audit trail would be
        // inconsistent if the state flipped mid-pipeline.
        SemaphoreStatus semaphoreSnapshot = await _semaphoreGate.GetCurrentStatusAsync(ct).ConfigureAwait(false);

        // ===== GATE #1 — Semaphore =====
        // Fail-closed synchronous check. Timeout + exception = true (blocked).
        bool isRed = _semaphoreGate.IsRed();
        if (isRed && !_safetyOptions.OverrideSemaphore)
        {
            _logger.LogWarning(
                "SemaphoreGate blocked order: {Symbol} {Side} {Qty} (strategy={Strategy})",
                request.ContractSymbol, request.Side, request.Quantity, request.StrategyName);

            await _alerter.SendImmediateAsync(
                AlertSeverity.Critical,
                "Order blocked by SemaphoreGate",
                $"{request.ContractSymbol} {request.Side} {request.Quantity} — semaphore RED. Strategy: {request.StrategyName}",
                ct).ConfigureAwait(false);

            await _auditSink.WriteAsync(
                OrderAuditEntry.Rejected(request, SemaphoreStatus.Red, AuditOutcome.RejectedSemaphore, "semaphore-red"),
                ct).ConfigureAwait(false);

            return OrderResult.Fail(OrderStatus.ValidationFailed, "Order blocked by SemaphoreGate (RED)");
        }

        // Surface override visibility in the per-order log — prevents "why did it
        // trade on red?" confusion when the override is intentional.
        if (isRed && _safetyOptions.OverrideSemaphore)
        {
            _logger.LogCritical(
                "SemaphoreGate is RED but Safety:OverrideSemaphore=true — order ALLOWED: {Symbol} {Side} {Qty}",
                request.ContractSymbol, request.Side, request.Quantity);
        }

        // ===== GATE #2 — DailyPnLWatcher pause flag =====
        bool paused = await _flagStore.IsSetAsync("trading_paused", ct).ConfigureAwait(false);
        if (paused)
        {
            _logger.LogWarning(
                "Trading paused by DailyPnLWatcher flag — order rejected: {Symbol} {Side} {Qty}",
                request.ContractSymbol, request.Side, request.Quantity);

            await _auditSink.WriteAsync(
                OrderAuditEntry.Rejected(request, semaphoreSnapshot, AuditOutcome.RejectedPnlPause, "pnl-paused"),
                ct).ConfigureAwait(false);

            return OrderResult.Fail(OrderStatus.ValidationFailed, "Trading paused (pnl-paused)");
        }

        // ===== GATE #3 — Request structural validation =====
        string? validationError = request.Validate();
        if (validationError is not null)
        {
            _logger.LogWarning("Order validation failed: {Error}", validationError);
            // Malformed requests are a programmer error, not a safety concern.
            // Still audit them so a broken strategy surfaces in the audit view.
            await _auditSink.WriteAsync(
                OrderAuditEntry.Rejected(request, semaphoreSnapshot, AuditOutcome.Error, validationError),
                ct).ConfigureAwait(false);
            return OrderResult.Fail(OrderStatus.ValidationFailed, validationError);
        }

        // ===== GATE #4 — Circuit breaker =====
        if (IsCircuitBreakerOpen())
        {
            string error = $"Circuit breaker is open. Tripped at {_circuitBreakerTrippedAt:O}";
            _logger.LogError("Order rejected by circuit breaker: {CampaignId}", request.CampaignId);
            await _auditSink.WriteAsync(
                OrderAuditEntry.Rejected(request, semaphoreSnapshot, AuditOutcome.RejectedBreaker, "breaker-open"),
                ct).ConfigureAwait(false);
            return OrderResult.Fail(OrderStatus.ValidationFailed, error);
        }

        // ===== GATE #5 — Per-order safety validators =====
        (OrderResult? safetyResult, AuditOutcome? safetyOutcome) = await ValidateSafetyRulesAsync(request, ct).ConfigureAwait(false);
        if (safetyResult is not null && safetyOutcome is not null)
        {
            await _auditSink.WriteAsync(
                OrderAuditEntry.Rejected(request, semaphoreSnapshot, safetyOutcome.Value, safetyResult.Error ?? "safety-reject"),
                ct).ConfigureAwait(false);
            return safetyResult;
        }

        // Connection check — IBKR must be live.
        if (!_ibkrClient.IsConnected)
        {
            string error = "IBKR client is not connected";
            _logger.LogError("Cannot place order: {Error}", error);
            await _auditSink.WriteAsync(
                OrderAuditEntry.Rejected(request, semaphoreSnapshot, AuditOutcome.Error, error),
                ct).ConfigureAwait(false);
            return OrderResult.Fail(OrderStatus.Failed, error);
        }

        // ===== Submission =====
        string orderId = Guid.NewGuid().ToString();
        int ibkrOrderId = _ibkrClient.GetNextOrderId();

        try
        {
            // Log order as PendingSubmit (internal order_tracking table)
            await _orderRepo.LogOrderAsync(orderId, ibkrOrderId, request, OrderStatus.PendingSubmit, ct).ConfigureAwait(false);

            // Submit order to IBKR
            bool submitted = _ibkrClient.PlaceOrder(ibkrOrderId, request);

            if (!submitted)
            {
                // Submission rejected by broker — classify as BrokerReject
                // (deliberate refusal of our request, not a transport fault).
                await _orderRepo.UpdateOrderStatusAsync(orderId, OrderStatus.Failed, 0, 0m, ct).ConfigureAwait(false);
                await RecordFailureAsync(IbkrFailureType.BrokerReject, ct).ConfigureAwait(false);

                await _auditSink.WriteAsync(
                    OrderAuditEntry.BrokerRejected(request, semaphoreSnapshot, orderId, "IBKR PlaceOrder returned false"),
                    ct).ConfigureAwait(false);

                return OrderResult.Fail(orderId, OrderStatus.Failed, "IBKR PlaceOrder returned false");
            }

            // Submitted OK — flip status + write "placed" audit row.
            await _orderRepo.UpdateOrderStatusAsync(orderId, OrderStatus.Submitted, 0, 0m, ct).ConfigureAwait(false);
            await _auditSink.WriteAsync(
                OrderAuditEntry.Placed(request, semaphoreSnapshot, orderId),
                ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Order submitted: {OrderId} (IBKR: {IbkrOrderId}) - {Side} {Quantity} {Symbol}",
                orderId, ibkrOrderId, request.Side, request.Quantity, request.ContractSymbol);

            return OrderResult.Ok(orderId, ibkrOrderId, OrderStatus.Submitted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order submission failed: {OrderId}", orderId);

            // Best-effort status update.
            try
            {
                await _orderRepo.UpdateOrderStatusAsync(orderId, OrderStatus.Failed, 0, 0m, ct).ConfigureAwait(false);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update failed order status: {OrderId}", orderId);
            }

            // Exception during IBKR call → classify as BrokerReject (we don't know
            // if the other side received it; better to count and trip early).
            await RecordFailureAsync(IbkrFailureType.BrokerReject, ct).ConfigureAwait(false);

            await _auditSink.WriteAsync(
                OrderAuditEntry.ErrorDuring(request, semaphoreSnapshot, orderId, ex.Message),
                ct).ConfigureAwait(false);

            return OrderResult.Fail(OrderStatus.Failed, ex.Message);
        }
    }

    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken ct = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        }

        // Get order from database
        OrderRecord? order = await _orderRepo.GetOrderAsync(orderId, ct);
        if (order is null)
        {
            _logger.LogWarning("Cannot cancel order {OrderId}: not found", orderId);
            return false;
        }

        // Check if order can be cancelled
        if (order.Status is not (OrderStatus.PendingSubmit or OrderStatus.Submitted or OrderStatus.Active or OrderStatus.PartiallyFilled))
        {
            _logger.LogWarning(
                "Cannot cancel order {OrderId}: status is {Status}",
                orderId, order.Status);
            return false;
        }

        if (order.IbkrOrderId is null)
        {
            _logger.LogWarning("Cannot cancel order {OrderId}: no IBKR order ID", orderId);
            return false;
        }

        try
        {
            // Cancel via IBKR
            _ibkrClient.CancelOrder(order.IbkrOrderId.Value);

            _logger.LogInformation(
                "Cancellation requested for order {OrderId} (IBKR: {IbkrOrderId})",
                orderId, order.IbkrOrderId.Value);

            // Note: Status will be updated to Cancelled when IBKR sends the orderStatus callback
            // This is handled by the OrderStatusHandler (not implemented in this task)

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", orderId);
            return false;
        }
    }

    public bool IsCircuitBreakerOpen()
    {
        lock (_circuitLock)
        {
            // If circuit breaker is open, check if cooldown has expired
            if (_circuitBreakerOpen && _circuitBreakerTrippedAt is not null)
            {
                DateTime cooldownEnd = _circuitBreakerTrippedAt.Value
                    .AddMinutes(_safetyConfig.CircuitBreakerCooldownMinutes);

                if (DateTime.UtcNow >= cooldownEnd)
                {
                    _logger.LogInformation("Circuit breaker cooldown expired. Resetting circuit.");
                    _circuitBreakerOpen = false;
                    _circuitBreakerTrippedAt = null;
                }
            }

            return _circuitBreakerOpen;
        }
    }

    public void ResetCircuitBreaker()
    {
        lock (_circuitLock)
        {
            _circuitBreakerOpen = false;
            _circuitBreakerTrippedAt = null;
            _logger.LogWarning("Circuit breaker manually reset");
        }
    }

    public async Task<OrderStats> GetOrderStatsAsync(CancellationToken ct = default)
    {
        OrderStats stats = await _orderRepo.GetOrderStatsAsync(ct);

        // Add circuit breaker state
        lock (_circuitLock)
        {
            return stats with
            {
                CircuitBreakerOpen = _circuitBreakerOpen,
                CircuitBreakerTrippedAt = _circuitBreakerTrippedAt
            };
        }
    }

    /// <summary>
    /// Updates the cached account balance (called by background service).
    /// </summary>
    public void UpdateAccountBalance(decimal balance)
    {
        lock (_balanceLock)
        {
            _cachedAccountBalance = balance;
            _logger.LogDebug("Account balance updated: {Balance:C}", balance);
        }
    }

    /// <summary>
    /// Validates all per-order safety rules. Returns <c>(result, outcome)</c>:
    /// <list type="bullet">
    ///   <item><description>Both null ⇒ all checks passed.</description></item>
    ///   <item><description>Both non-null ⇒ a check rejected; the <see cref="AuditOutcome"/>
    ///   identifies WHICH check so the audit row is filed under the correct bucket.</description></item>
    /// </list>
    /// </summary>
    private Task<(OrderResult? Result, AuditOutcome? Outcome)> ValidateSafetyRulesAsync(
        OrderRequest request,
        CancellationToken ct)
    {
        _ = ct; // validators are fully synchronous; kept async signature for call-site symmetry.

        // SAFETY 1: Trading mode must be Paper. Live-trading guard.
        if (_safetyConfig.TradingMode != TradingMode.Paper)
        {
            string error = "Live trading is not allowed. Only Paper mode is permitted.";
            _logger.LogCritical("SAFETY VIOLATION: {Error}", error);
            return Task.FromResult<(OrderResult?, AuditOutcome?)>(
                (OrderResult.Fail(OrderStatus.ValidationFailed, error), AuditOutcome.Error));
        }

        // SAFETY 2: Position size limit
        if (request.Quantity > _safetyConfig.MaxPositionSize)
        {
            string error = $"Position size {request.Quantity} exceeds max {_safetyConfig.MaxPositionSize}";
            _logger.LogWarning("Order rejected: {Error}", error);
            return Task.FromResult<(OrderResult?, AuditOutcome?)>(
                (OrderResult.Fail(OrderStatus.ValidationFailed, error), AuditOutcome.RejectedMaxSize));
        }

        // SAFETY 3: Estimate position value. Limit price when provided, else a
        // conservative default of $100/contract. The 100x multiplier is the
        // standard equity-option multiplier.
        decimal estimatedPrice = request.LimitPrice ?? 100m;
        decimal positionValue = request.Quantity * estimatedPrice * 100m;

        if (positionValue > _safetyConfig.MaxPositionValueUsd)
        {
            string error = $"Position value ${positionValue:N0} exceeds max ${_safetyConfig.MaxPositionValueUsd:N0}";
            _logger.LogWarning("Order rejected: {Error}", error);
            return Task.FromResult<(OrderResult?, AuditOutcome?)>(
                (OrderResult.Fail(OrderStatus.ValidationFailed, error), AuditOutcome.RejectedMaxValue));
        }

        // SAFETY 4: Account balance must be above the floor.
        decimal accountBalance;
        lock (_balanceLock)
        {
            accountBalance = _cachedAccountBalance;
        }

        if (accountBalance < _safetyConfig.MinAccountBalanceUsd)
        {
            string error = $"Account balance ${accountBalance:N0} below minimum ${_safetyConfig.MinAccountBalanceUsd:N0}";
            _logger.LogError("Order rejected: {Error}", error);
            return Task.FromResult<(OrderResult?, AuditOutcome?)>(
                (OrderResult.Fail(OrderStatus.ValidationFailed, error), AuditOutcome.RejectedMinBalance));
        }

        // SAFETY 5: Position size as percentage of account (the "risk-pct" check).
        decimal maxPositionValue = accountBalance * _safetyConfig.MaxPositionPctOfAccount;
        if (positionValue > maxPositionValue)
        {
            string error = $"Position value ${positionValue:N0} exceeds {_safetyConfig.MaxPositionPctOfAccount:P0} " +
                          $"of account (${maxPositionValue:N0})";
            _logger.LogWarning("Order rejected: {Error}", error);
            return Task.FromResult<(OrderResult?, AuditOutcome?)>(
                (OrderResult.Fail(OrderStatus.ValidationFailed, error), AuditOutcome.RejectedMaxRisk));
        }

        // All safety checks passed.
        return Task.FromResult<(OrderResult?, AuditOutcome?)>((null, null));
    }

    /// <summary>
    /// Records a failure and checks if the circuit breaker should trip. The
    /// classification matters:
    /// <list type="bullet">
    ///   <item><description><see cref="IbkrFailureType.NetworkError"/>: does NOT count — transport noise shouldn't open the breaker.</description></item>
    ///   <item><description><see cref="IbkrFailureType.BrokerReject"/>: counts — a deliberate rejection is the signal we want to contain.</description></item>
    ///   <item><description><see cref="IbkrFailureType.Unknown"/>: counts (fail-closed).</description></item>
    /// </list>
    /// Callers who want to explicitly report a transport fault should pass
    /// <see cref="IbkrFailureType.NetworkError"/>; the breaker will silently
    /// skip it but still log for observability.
    /// </summary>
    private async Task RecordFailureAsync(IbkrFailureType failureType, CancellationToken ct)
    {
        if (failureType == IbkrFailureType.NetworkError)
        {
            // Transport-level fault: intentionally ignored for breaker math. Log
            // it so dashboards still see the event, but don't escalate.
            _logger.LogInformation("Network-class IBKR failure observed — NOT counted toward circuit breaker");
            return;
        }

        try
        {
            // We still use the persistent failed-orders table as the source of
            // truth for "how many true failures in the window" — consistent with
            // pre-7.4 behavior and resilient across restarts.
            List<OrderRecord> recentFailures = await _orderRepo.GetFailedOrdersInWindowAsync(
                _safetyConfig.CircuitBreakerWindowMinutes,
                ct).ConfigureAwait(false);

            int failureCount = recentFailures.Count;

            _logger.LogWarning(
                "Failure recorded (type={Type}). {Count} failures in last {Minutes} minutes",
                failureType, failureCount, _safetyConfig.CircuitBreakerWindowMinutes);

            if (failureCount >= _safetyConfig.CircuitBreakerFailureThreshold)
            {
                lock (_circuitLock)
                {
                    if (!_circuitBreakerOpen)
                    {
                        _circuitBreakerOpen = true;
                        _circuitBreakerTrippedAt = DateTime.UtcNow;

                        _logger.LogCritical(
                            "CIRCUIT BREAKER TRIPPED: {Count} failures in {Minutes} minutes. " +
                            "All order placement blocked for {Cooldown} minutes.",
                            failureCount,
                            _safetyConfig.CircuitBreakerWindowMinutes,
                            _safetyConfig.CircuitBreakerCooldownMinutes);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record failure for circuit breaker");
            // Don't throw — breaker accounting failure must not block order processing.
        }
    }

    /// <summary>
    /// Public hook for external callers (e.g. connection watcher) to classify
    /// IBKR-side failures against the breaker. Mirrors the internal helper so
    /// we don't have to expose <c>RecordFailureAsync</c> directly.
    /// </summary>
    public Task RecordIbkrFailureAsync(IbkrFailureType failureType, CancellationToken ct = default)
        => RecordFailureAsync(failureType, ct);

    // Legacy methods for Campaign Manager (TODO: Refactor CampaignManager to use new interface)
    public Task<IReadOnlyList<string>> PlaceEntryOrdersAsync(string campaignId, StrategyDefinition strategy, CancellationToken ct = default)
    {
        _logger.LogWarning("[STUB] PlaceEntryOrdersAsync called with legacy interface - needs refactoring");
        List<string> positionIds = new() { $"pos_{Guid.NewGuid():N}", $"pos_{Guid.NewGuid():N}" };
        return Task.FromResult<IReadOnlyList<string>>(positionIds);
    }

    public Task<decimal> GetUnrealizedPnLAsync(string campaignId, CancellationToken ct = default)
    {
        _logger.LogWarning("[STUB] GetUnrealizedPnLAsync called with legacy interface - needs refactoring");
        return Task.FromResult(0m);
    }

    public Task<decimal> ClosePositionsAsync(string campaignId, CancellationToken ct = default)
    {
        _logger.LogWarning("[STUB] ClosePositionsAsync called with legacy interface - needs refactoring");
        return Task.FromResult(0m);
    }
}
