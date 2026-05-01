using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Services;
using SharedKernel.Configuration;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using SharedKernel.Observability;
using SharedKernel.Safety;
using System.Globalization;

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
///   <item><description>Gate #4 — Circuit breaker (see <see cref="RecordFailureAsync"/>). Classified via <see cref="IbkrFailureType"/> so network noise doesn't trip the breaker.</description></item>
///   <item><description>Gate #5 — Per-order safety validators (size, value, account balance, risk-pct).</description></item>
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
    private readonly IOrderCircuitBreaker _circuitBreaker;
    private readonly IAccountEquityProvider _equityProvider;
    private readonly ILogger<OrderPlacer> _logger;

    public OrderPlacer(
        IIbkrClient ibkrClient,
        IOrderTrackingRepository orderRepo,
        OrderSafetyConfig safetyConfig,
        SemaphoreGate semaphoreGate,
        ISafetyFlagStore flagStore,
        IOrderAuditSink auditSink,
        IAlerter alerter,
        IOrderCircuitBreaker circuitBreaker,
        IAccountEquityProvider equityProvider,
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
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        _equityProvider = equityProvider ?? throw new ArgumentNullException(nameof(equityProvider));
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

        // Snapshot semaphore state ONCE per call — both the gate decision AND
        // the audit entry use the same value. Previously we called
        // GetCurrentStatusAsync() and then IsRed() separately; IsRed() can
        // re-fetch (or time out fail-closed) and produce a decision that
        // disagrees with the audit's recorded snapshot — worst-case a Green
        // snapshot + Red rejection.
        SemaphoreStatus semaphoreSnapshot = await _semaphoreGate.GetCurrentStatusAsync(ct).ConfigureAwait(false);

        // ===== GATE #1 — Semaphore =====
        // ORANGE is the documented fail-cautious state for Worker/network
        // failures (see SemaphoreGate + SemaphoreStatus docstrings): it must
        // block NEW entries the same way RED does. Otherwise a Worker outage
        // (which maps to ORANGE) would silently allow trades — defeating the
        // entire fail-cautious posture.
        bool isBlockedBySemaphore =
            semaphoreSnapshot == SemaphoreStatus.Red ||
            semaphoreSnapshot == SemaphoreStatus.Orange;

        if (isBlockedBySemaphore && !_safetyOptions.OverrideSemaphore)
        {
            _logger.LogWarning(
                "SemaphoreGate blocked order: {Symbol} {Side} {Qty} (strategy={Strategy}, status={SemaphoreStatus})",
                request.ContractSymbol, request.Side, request.Quantity, request.StrategyName, semaphoreSnapshot);

            await _alerter.SendImmediateAsync(
                AlertSeverity.Critical,
                "Order blocked by SemaphoreGate",
                $"{request.ContractSymbol} {request.Side} {request.Quantity} — semaphore {semaphoreSnapshot}. Strategy: {request.StrategyName}",
                ct).ConfigureAwait(false);

            await _auditSink.WriteAsync(
                OrderAuditEntry.Rejected(
                    request,
                    semaphoreSnapshot,
                    AuditOutcome.RejectedSemaphore,
                    $"semaphore-{semaphoreSnapshot.ToString().ToLowerInvariant()}"),
                ct).ConfigureAwait(false);

            return OrderResult.Fail(OrderStatus.ValidationFailed, $"Order blocked by SemaphoreGate ({semaphoreSnapshot})");
        }

        // Surface override visibility in the per-order log — prevents "why did it
        // trade on a blocked semaphore?" confusion when the override is intentional.
        if (isBlockedBySemaphore && _safetyOptions.OverrideSemaphore)
        {
            _logger.LogCritical(
                "SemaphoreGate is {SemaphoreStatus} but Safety:OverrideSemaphore=true — order ALLOWED: {Symbol} {Side} {Qty}",
                semaphoreSnapshot, request.ContractSymbol, request.Side, request.Quantity);
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
        if (_circuitBreaker.IsOpen())
        {
            CircuitBreakerState breakerState = _circuitBreaker.GetState();
            string error = string.Format(CultureInfo.InvariantCulture,
                "Circuit breaker is open. Tripped at {0}. Reason: {1}",
                breakerState.TrippedAt?.ToString("O", CultureInfo.InvariantCulture) ?? "unknown",
                breakerState.Reason ?? "unknown");
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

                // Query failure count and delegate to circuit breaker
                List<OrderRecord> recentFailures = await _orderRepo.GetFailedOrdersInWindowAsync(
                    _safetyConfig.CircuitBreakerWindowMinutes, ct).ConfigureAwait(false);
                await _circuitBreaker.RecordFailureAsync(IbkrFailureType.BrokerReject, recentFailures.Count, ct).ConfigureAwait(false);

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
            List<OrderRecord> recentFailures = await _orderRepo.GetFailedOrdersInWindowAsync(
                _safetyConfig.CircuitBreakerWindowMinutes, ct).ConfigureAwait(false);
            await _circuitBreaker.RecordFailureAsync(IbkrFailureType.BrokerReject, recentFailures.Count, ct).ConfigureAwait(false);

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
        return _circuitBreaker.IsOpen();
    }

    public void ResetCircuitBreaker()
    {
        _circuitBreaker.Reset();
    }

    public async Task<OrderStats> GetOrderStatsAsync(CancellationToken ct = default)
    {
        OrderStats stats = await _orderRepo.GetOrderStatsAsync(ct);

        // Add circuit breaker state from singleton
        CircuitBreakerState breakerState = _circuitBreaker.GetState();
        return stats with
        {
            CircuitBreakerOpen = breakerState.IsOpen,
            CircuitBreakerTrippedAt = breakerState.TrippedAt
        };
    }

    /// <summary>
    /// Updates the account equity (legacy method for backward compatibility with tests).
    /// New code should update via IAccountEquityProvider directly.
    /// </summary>
    public void UpdateAccountBalance(decimal balance)
    {
        _equityProvider.UpdateEquity(balance, DateTime.UtcNow);
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

        // SAFETY 4: Account equity must be available and fresh.
        AccountEquitySnapshot? equity = _equityProvider.GetEquity();

        if (equity is null)
        {
            string error = "Account equity unavailable - cannot verify safety limits";
            _logger.LogError("Order rejected: {Error}", error);
            return Task.FromResult<(OrderResult?, AuditOutcome?)>(
                (OrderResult.Fail(OrderStatus.ValidationFailed, error), AuditOutcome.Error));
        }

        if (equity.IsStale)
        {
            string error = string.Format(CultureInfo.InvariantCulture,
                "Account equity is stale (age: {0:F0}s) - refusing order for safety",
                equity.Age.TotalSeconds);
            _logger.LogError("Order rejected: {Error}", error);
            return Task.FromResult<(OrderResult?, AuditOutcome?)>(
                (OrderResult.Fail(OrderStatus.ValidationFailed, error), AuditOutcome.Error));
        }

        decimal accountBalance = equity.NetLiquidation;

        if (accountBalance < _safetyConfig.MinAccountBalanceUsd)
        {
            string error = string.Format(CultureInfo.InvariantCulture,
                "Account balance ${0:N0} below minimum ${1:N0}",
                accountBalance,
                _safetyConfig.MinAccountBalanceUsd);
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
    /// Public hook for external callers (e.g. connection watcher) to classify
    /// IBKR-side failures against the breaker.
    /// </summary>
    public async Task RecordIbkrFailureAsync(IbkrFailureType failureType, CancellationToken ct = default)
    {
        try
        {
            List<OrderRecord> recentFailures = await _orderRepo.GetFailedOrdersInWindowAsync(
                _safetyConfig.CircuitBreakerWindowMinutes, ct).ConfigureAwait(false);
            await _circuitBreaker.RecordFailureAsync(failureType, recentFailures.Count, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record IBKR failure for circuit breaker");
            // Don't throw — breaker accounting failure must not block order processing.
        }
    }

    // Legacy methods for Campaign Manager (TODO: Refactor CampaignManager to use new interface)
    public async Task<IReadOnlyList<string>> PlaceEntryOrdersAsync(string campaignId, StrategyDefinition strategy, CancellationToken ct = default)
    {
        // Phase 4: Campaign execution P1 - Task RM-03
        // Replace stub with real order placement for each leg.

        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("Campaign ID cannot be empty", nameof(campaignId));
        }

        ArgumentNullException.ThrowIfNull(strategy);

        if (strategy.Position.Legs.Length == 0)
        {
            throw new InvalidOperationException($"Campaign {campaignId}: Strategy {strategy.StrategyName} has no legs defined");
        }

        _logger.LogInformation(
            "Placing entry orders for campaign {CampaignId}, strategy {StrategyName}, {LegCount} legs",
            campaignId, strategy.StrategyName, strategy.Position.Legs.Length);

        List<string> orderIds = new();

        try
        {
            // Place order for each leg sequentially
            for (int legIndex = 0; legIndex < strategy.Position.Legs.Length; legIndex++)
            {
                OptionLeg leg = strategy.Position.Legs[legIndex];

                // NOTE: This implementation requires legs to have pre-resolved contract symbol.
                // Strike selection (DELTA/OFFSET) requires market data engine which is not
                // implemented yet. For now, campaign configs must specify explicit strike/expiry
                // or use a contract symbol resolver before calling this method.

                // Validate leg has minimum required fields
                if (string.IsNullOrWhiteSpace(leg.Action))
                {
                    throw new InvalidOperationException(
                        $"Campaign {campaignId} leg {legIndex}: Action (BUY/SELL) is required");
                }

                if (string.IsNullOrWhiteSpace(leg.Right))
                {
                    throw new InvalidOperationException(
                        $"Campaign {campaignId} leg {legIndex}: Right (CALL/PUT) is required");
                }

                // Determine order side from leg action
                OrderSide side = leg.Action.ToUpperInvariant() switch
                {
                    "BUY" => OrderSide.Buy,
                    "SELL" => OrderSide.Sell,
                    _ => throw new InvalidOperationException(
                        $"Campaign {campaignId} leg {legIndex}: Invalid action '{leg.Action}'. Must be BUY or SELL")
                };

                // For Phase 4, we require a contract symbol to be pre-resolved.
                // Future enhancement: implement strike selection engine to resolve DELTA/OFFSET.
                // For now, throw if strike selection method requires resolution.
                if (leg.StrikeSelectionMethod != "ABSOLUTE" && leg.StrikeValue == null)
                {
                    throw new NotImplementedException(
                        $"Campaign {campaignId} leg {legIndex}: Strike selection method '{leg.StrikeSelectionMethod}' " +
                        $"requires market data resolution (DELTA/OFFSET not yet implemented). " +
                        $"Use StrikeSelectionMethod=ABSOLUTE with explicit StrikeValue for now.");
                }

                // Build contract symbol (simplified - assumes OCC format or explicit symbol)
                // Future: use proper contract builder with symbol/expiry/strike/right/exchange/currency/multiplier
                string contractSymbol = BuildContractSymbol(strategy.Underlying.Symbol, leg);

                // Create order request for this leg
                // TODO: calculate appropriate limit price from bid/ask/mid market data
                // For now, use a conservative placeholder price for Phase 4
                decimal placeholderPrice = leg.Action.ToUpperInvariant() == "BUY" ? 0.10m : 0.05m;

                OrderRequest request = new()
                {
                    CampaignId = campaignId,
                    Symbol = strategy.Underlying.Symbol,
                    ContractSymbol = contractSymbol,
                    Side = side,
                    Type = OrderType.Limit,
                    Quantity = leg.Quantity,
                    LimitPrice = placeholderPrice,
                    StrategyName = strategy.StrategyName
                };

                _logger.LogInformation(
                    "Placing entry order for campaign {CampaignId} leg {LegIndex}: {Side} {Quantity} {ContractSymbol}",
                    campaignId, legIndex, side, leg.Quantity, contractSymbol);

                // Place order
                OrderResult result = await PlaceOrderAsync(request, ct);

                if (!result.Success)
                {
                    // Entry failed - cleanup any partial orders placed
                    _logger.LogError(
                        "Entry order failed for campaign {CampaignId} leg {LegIndex}: {Error}. " +
                        "Campaign will NOT transition to Active. Already placed orders: {PlacedOrders}",
                        campaignId, legIndex, result.Error, string.Join(", ", orderIds));

                    throw new InvalidOperationException(
                        $"Campaign {campaignId} entry failed at leg {legIndex}: {result.Error}. " +
                        $"Campaign remains in Open state. Placed orders so far: {string.Join(", ", orderIds)}");
                }

                orderIds.Add(result.OrderId!);

                _logger.LogInformation(
                    "Entry order placed for campaign {CampaignId} leg {LegIndex}: OrderId={OrderId}",
                    campaignId, legIndex, result.OrderId);
            }

            _logger.LogInformation(
                "All entry orders placed successfully for campaign {CampaignId}. OrderIds: {OrderIds}",
                campaignId, string.Join(", ", orderIds));

            return orderIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to place entry orders for campaign {CampaignId}. Placed orders: {PlacedOrders}",
                campaignId, string.Join(", ", orderIds));
            throw;
        }
    }

    /// <summary>
    /// Builds OCC-style contract symbol from underlying + leg definition.
    /// Phase 4: Simplified version - requires ABSOLUTE strike selection.
    /// Future: enhance with proper contract resolution from market data.
    /// </summary>
    private static string BuildContractSymbol(string underlying, OptionLeg leg)
    {
        // Simplified: assume leg has explicit strike value and we build OCC-like symbol
        // Real implementation would query IBKR for contract details or use option chain data

        if (leg.StrikeValue == null)
        {
            throw new InvalidOperationException(
                "Cannot build contract symbol without explicit strike value. " +
                "DELTA/OFFSET selection requires market data engine.");
        }

        // OCC format example: "SPX   250321P05000000"
        // underlying (6 chars padded) + expiry YYMMDD + C/P + strike (8 digits, 3 decimals)

        // For Phase 4, use simplified placeholder format: "underlying-strike-right"
        // This will fail IBKR validation, but demonstrates the flow.
        // TODO RM-02: implement proper option contract validation
        string right = leg.Right.ToUpperInvariant() == "CALL" ? "C" : "P";
        return $"{underlying}-{leg.StrikeValue:F2}-{right}";
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
