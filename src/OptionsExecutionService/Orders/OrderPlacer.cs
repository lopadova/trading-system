using Microsoft.Extensions.Logging;
using OptionsExecutionService.Repositories;
using SharedKernel.Domain;
using SharedKernel.Ibkr;

namespace OptionsExecutionService.Orders;

/// <summary>
/// Service for placing and tracking orders with comprehensive safety validations.
/// Implements circuit breaker pattern to prevent cascading failures.
/// </summary>
public sealed class OrderPlacer : IOrderPlacer
{
    private readonly IIbkrClient _ibkrClient;
    private readonly IOrderTrackingRepository _orderRepo;
    private readonly OrderSafetyConfig _safetyConfig;
    private readonly ILogger<OrderPlacer> _logger;

    // Circuit breaker state
    private readonly object _circuitLock = new();
    private bool _circuitBreakerOpen = false;
    private DateTime? _circuitBreakerTrippedAt = null;

    // Account balance cache (updated periodically by background service)
    private decimal _cachedAccountBalance = 0m;
    private readonly object _balanceLock = new();

    public OrderPlacer(
        IIbkrClient ibkrClient,
        IOrderTrackingRepository orderRepo,
        OrderSafetyConfig safetyConfig,
        ILogger<OrderPlacer> logger)
    {
        _ibkrClient = ibkrClient ?? throw new ArgumentNullException(nameof(ibkrClient));
        _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
        _safetyConfig = safetyConfig ?? throw new ArgumentNullException(nameof(safetyConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate safety config at startup
        _safetyConfig.Validate();

        _logger.LogInformation(
            "OrderPlacer initialized. Mode: {Mode}, MaxSize: {MaxSize}, CircuitBreaker: {Threshold} failures in {Window}min",
            _safetyConfig.TradingMode, _safetyConfig.MaxPositionSize,
            _safetyConfig.CircuitBreakerFailureThreshold, _safetyConfig.CircuitBreakerWindowMinutes);
    }

    public async Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct = default)
    {
        // STEP 1: Validate order request
        string? validationError = request.Validate();
        if (validationError is not null)
        {
            _logger.LogWarning("Order validation failed: {Error}", validationError);
            return OrderResult.Fail(OrderStatus.ValidationFailed, validationError);
        }

        // STEP 2: Check circuit breaker
        if (IsCircuitBreakerOpen())
        {
            string error = $"Circuit breaker is open. Tripped at {_circuitBreakerTrippedAt:O}";
            _logger.LogError("Order rejected by circuit breaker: {CampaignId}", request.CampaignId);
            return OrderResult.Fail(OrderStatus.ValidationFailed, error);
        }

        // STEP 3: Safety validations
        OrderResult? safetyResult = await ValidateSafetyRulesAsync(request, ct);
        if (safetyResult is not null)
        {
            return safetyResult; // Safety check failed
        }

        // STEP 4: Check IBKR connection
        if (!_ibkrClient.IsConnected)
        {
            string error = "IBKR client is not connected";
            _logger.LogError("Cannot place order: {Error}", error);
            return OrderResult.Fail(OrderStatus.Failed, error);
        }

        // STEP 5: Generate order ID and log BEFORE submission (audit trail)
        string orderId = Guid.NewGuid().ToString();
        int ibkrOrderId = _ibkrClient.GetNextOrderId();

        try
        {
            // Log order as PendingSubmit
            await _orderRepo.LogOrderAsync(
                orderId,
                ibkrOrderId,
                request,
                OrderStatus.PendingSubmit,
                ct);

            // STEP 6: Submit order to IBKR
            bool submitted = _ibkrClient.PlaceOrder(ibkrOrderId, request);

            if (!submitted)
            {
                // Submission failed immediately
                await _orderRepo.UpdateOrderStatusAsync(
                    orderId,
                    OrderStatus.Failed,
                    0,
                    0m,
                    ct);

                await RecordFailureAsync(ct);

                return OrderResult.Fail(OrderStatus.Failed, "IBKR PlaceOrder returned false");
            }

            // STEP 7: Update status to Submitted
            await _orderRepo.UpdateOrderStatusAsync(
                orderId,
                OrderStatus.Submitted,
                0,
                0m,
                ct);

            _logger.LogInformation(
                "Order submitted: {OrderId} (IBKR: {IbkrOrderId}) - {Side} {Quantity} {Symbol}",
                orderId, ibkrOrderId, request.Side, request.Quantity, request.ContractSymbol);

            return OrderResult.Ok(orderId, ibkrOrderId, OrderStatus.Submitted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order submission failed: {OrderId}", orderId);

            // Update order status to Failed
            try
            {
                await _orderRepo.UpdateOrderStatusAsync(
                    orderId,
                    OrderStatus.Failed,
                    0,
                    0m,
                    ct);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update failed order status: {OrderId}", orderId);
            }

            await RecordFailureAsync(ct);

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
    /// Validates all safety rules. Returns error result if any check fails, null if all pass.
    /// </summary>
    private async Task<OrderResult?> ValidateSafetyRulesAsync(OrderRequest request, CancellationToken ct)
    {
        // SAFETY 1: Trading mode must be Paper
        if (_safetyConfig.TradingMode != TradingMode.Paper)
        {
            string error = "Live trading is not allowed. Only Paper mode is permitted.";
            _logger.LogCritical("SAFETY VIOLATION: {Error}", error);
            return OrderResult.Fail(OrderStatus.ValidationFailed, error);
        }

        // SAFETY 2: Position size limit
        if (request.Quantity > _safetyConfig.MaxPositionSize)
        {
            string error = $"Position size {request.Quantity} exceeds max {_safetyConfig.MaxPositionSize}";
            _logger.LogWarning("Order rejected: {Error}", error);
            return OrderResult.Fail(OrderStatus.ValidationFailed, error);
        }

        // SAFETY 3: Estimate position value (for market orders, use conservative estimate)
        // For real implementation, would query current market price via IBKR
        // For now, use limit price if available, otherwise conservative estimate
        decimal estimatedPrice = request.LimitPrice ?? 100m; // Conservative fallback
        decimal positionValue = request.Quantity * estimatedPrice * 100m; // Options are 100 multiplier

        if (positionValue > _safetyConfig.MaxPositionValueUsd)
        {
            string error = $"Position value ${positionValue:N0} exceeds max ${_safetyConfig.MaxPositionValueUsd:N0}";
            _logger.LogWarning("Order rejected: {Error}", error);
            return OrderResult.Fail(OrderStatus.ValidationFailed, error);
        }

        // SAFETY 4: Account balance check
        decimal accountBalance;
        lock (_balanceLock)
        {
            accountBalance = _cachedAccountBalance;
        }

        if (accountBalance < _safetyConfig.MinAccountBalanceUsd)
        {
            string error = $"Account balance ${accountBalance:N0} below minimum ${_safetyConfig.MinAccountBalanceUsd:N0}";
            _logger.LogError("Order rejected: {Error}", error);
            return OrderResult.Fail(OrderStatus.ValidationFailed, error);
        }

        // SAFETY 5: Position size as percentage of account
        decimal maxPositionValue = accountBalance * _safetyConfig.MaxPositionPctOfAccount;
        if (positionValue > maxPositionValue)
        {
            string error = $"Position value ${positionValue:N0} exceeds {_safetyConfig.MaxPositionPctOfAccount:P0} " +
                          $"of account (${maxPositionValue:N0})";
            _logger.LogWarning("Order rejected: {Error}", error);
            return OrderResult.Fail(OrderStatus.ValidationFailed, error);
        }

        // All safety checks passed
        return null;
    }

    /// <summary>
    /// Records a failure and checks if circuit breaker should trip.
    /// </summary>
    private async Task RecordFailureAsync(CancellationToken ct)
    {
        try
        {
            // Get recent failures from database
            List<OrderRecord> recentFailures = await _orderRepo.GetFailedOrdersInWindowAsync(
                _safetyConfig.CircuitBreakerWindowMinutes,
                ct);

            int failureCount = recentFailures.Count;

            _logger.LogWarning(
                "Failure recorded. {Count} failures in last {Minutes} minutes",
                failureCount, _safetyConfig.CircuitBreakerWindowMinutes);

            // Check if we should trip the circuit breaker
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
            // Don't throw - circuit breaker failure shouldn't block order processing completely
        }
    }

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
