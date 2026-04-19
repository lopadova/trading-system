using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OptionsExecutionService.Ibkr;
using SharedKernel.Domain;
using SharedKernel.Ibkr;

namespace OptionsExecutionService.Workers;

/// <summary>
/// TEMPORARY: Test worker to place and cancel a test order.
/// This worker will:
/// 1. Wait for IBKR connection
/// 2. Place a test market order for 1 SPY
/// 3. Wait 10 seconds
/// 4. Cancel the order
/// 5. Stop itself
/// </summary>
public class OrderTestWorker : BackgroundService
{
    private readonly ILogger<OrderTestWorker> _logger;
    private readonly IIbkrClient _ibkrClient;
    private int? _testOrderId;

    public OrderTestWorker(ILogger<OrderTestWorker> logger, IIbkrClient ibkrClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ibkrClient = ibkrClient ?? throw new ArgumentNullException(nameof(ibkrClient));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("🧪 OrderTestWorker started - will test order placement");

            // Wait for IBKR connection (max 30 seconds)
            int waitCount = 0;
            while (!_ibkrClient.IsConnected && waitCount < 30)
            {
                _logger.LogInformation("⏳ Waiting for IBKR connection... ({Seconds}s)", waitCount);
                await Task.Delay(1000, stoppingToken);
                waitCount++;
            }

            if (!_ibkrClient.IsConnected)
            {
                _logger.LogError("❌ IBKR not connected after 30 seconds. Cannot test order.");
                return;
            }

            _logger.LogInformation("✅ IBKR connected! Waiting 3 more seconds before placing order...");
            await Task.Delay(3000, stoppingToken);

            // Get next order ID (use a high number to avoid conflicts)
            _testOrderId = 99999;

            // Create test order: BUY 1 SPY Market
            var testOrder = new OrderRequest
            {
                Symbol = "SPY",
                SecurityType = "STK",
                Exchange = "SMART",
                Side = OrderSide.Buy,
                Quantity = 1,
                Type = OrderType.Market
            };

            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("📤 PLACING TEST ORDER:");
            _logger.LogInformation("   Order ID: {OrderId}", _testOrderId);
            _logger.LogInformation("   Symbol:   {Symbol}", testOrder.Symbol);
            _logger.LogInformation("   Side:     {Side}", testOrder.Side);
            _logger.LogInformation("   Quantity: {Qty}", testOrder.Quantity);
            _logger.LogInformation("   Type:     {Type}", testOrder.Type);
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            bool placed = _ibkrClient.PlaceOrder(_testOrderId.Value, testOrder);

            if (!placed)
            {
                _logger.LogError("❌ Failed to place test order!");
                return;
            }

            _logger.LogInformation("✅ Test order PLACED successfully!");
            _logger.LogInformation("⏳ Waiting 10 seconds to observe order status...");

            // Wait 10 seconds to see order updates
            await Task.Delay(10000, stoppingToken);

            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("🗑️  CANCELING TEST ORDER: {OrderId}", _testOrderId);
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            _ibkrClient.CancelOrder(_testOrderId.Value);

            _logger.LogInformation("✅ Cancel request sent!");
            _logger.LogInformation("⏳ Waiting 5 seconds to confirm cancellation...");

            await Task.Delay(5000, stoppingToken);

            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("✅ Order test COMPLETE!");
            _logger.LogInformation("🛑 OrderTestWorker stopping itself");
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OrderTestWorker cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OrderTestWorker failed with exception");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrderTestWorker stopping");
        return base.StopAsync(cancellationToken);
    }
}
