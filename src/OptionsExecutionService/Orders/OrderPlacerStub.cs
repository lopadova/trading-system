namespace OptionsExecutionService.Orders;

using Microsoft.Extensions.Logging;
using SharedKernel.Domain;

/// <summary>
/// Stub implementation of IOrderPlacer for testing.
/// Returns mock data without actually placing orders to IBKR.
/// </summary>
public sealed class OrderPlacerStub : IOrderPlacer
{
    private readonly ILogger<OrderPlacerStub> _logger;
    private bool _circuitBreakerOpen;

    public OrderPlacerStub(ILogger<OrderPlacerStub> logger)
    {
        _logger = logger;
    }

    public Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[STUB] PlaceOrderAsync: symbol={Symbol}, side={Side}, quantity={Quantity}",
            request.Symbol, request.Side, request.Quantity);

        if (_circuitBreakerOpen)
        {
            return Task.FromResult(OrderResult.Fail(OrderStatus.Failed, "Circuit breaker is open"));
        }

        // Return mock successful order
        return Task.FromResult(OrderResult.Ok(
            Guid.NewGuid().ToString("N"),
            Random.Shared.Next(10000, 99999),
            OrderStatus.Submitted));
    }

    public Task<bool> CancelOrderAsync(string orderId, CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB] CancelOrderAsync: orderId={OrderId}", orderId);
        return Task.FromResult(true);
    }

    public bool IsCircuitBreakerOpen()
    {
        return _circuitBreakerOpen;
    }

    public void ResetCircuitBreaker()
    {
        _logger.LogInformation("[STUB] ResetCircuitBreaker");
        _circuitBreakerOpen = false;
    }

    public Task<OrderStats> GetOrderStatsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB] GetOrderStatsAsync");

        return Task.FromResult(new OrderStats
        {
            TotalOrders = 10,
            FilledOrders = 7,
            FailedOrders = 1,
            ActiveOrders = 2,
            CancelledOrders = 0,
            CircuitBreakerOpen = _circuitBreakerOpen,
            FailuresInWindow = 0,
            CircuitBreakerTrippedAt = null
        });
    }

    // Legacy methods for Campaign Manager
    public Task<IReadOnlyList<string>> PlaceEntryOrdersAsync(string campaignId, StrategyDefinition strategy, CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB] PlaceEntryOrdersAsync: campaign={CampaignId}, strategy={StrategyName}", campaignId, strategy.StrategyName);
        List<string> positionIds = new() { $"pos_{Guid.NewGuid():N}", $"pos_{Guid.NewGuid():N}" };
        return Task.FromResult<IReadOnlyList<string>>(positionIds);
    }

    public Task<decimal> GetUnrealizedPnLAsync(string campaignId, CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB] GetUnrealizedPnLAsync: campaign={CampaignId}", campaignId);
        decimal mockPnL = (decimal)(Random.Shared.NextDouble() * 400 - 200);
        return Task.FromResult(mockPnL);
    }

    public Task<decimal> ClosePositionsAsync(string campaignId, CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB] ClosePositionsAsync: campaign={CampaignId}", campaignId);
        decimal mockPnL = (decimal)(Random.Shared.NextDouble() * 1000 - 500);
        return Task.FromResult(mockPnL);
    }
}
