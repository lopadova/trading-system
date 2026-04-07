using SharedKernel.Domain;
using SharedKernel.Ibkr;

namespace OptionsExecutionService.Tests.Mocks;

/// <summary>
/// Mock IBKR client for testing. Allows simulating connection states and order outcomes.
/// </summary>
public sealed class MockIbkrClient : IIbkrClient
{
    private ConnectionState _state = ConnectionState.Disconnected;
    private bool _isConnected = false;
    private int _nextOrderId = 1;

    // Test configuration
    public bool ShouldPlaceOrderSucceed { get; set; } = true;
    public bool ShouldConnectSucceed { get; set; } = true;
    public List<(int OrderId, OrderRequest Request)> PlacedOrders { get; } = new();
    public List<int> CancelledOrders { get; } = new();

    public ConnectionState State => _state;
    public bool IsConnected => _isConnected;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<(int OrderId, string Status, int Filled, int Remaining, double AvgFillPrice)>? OrderStatusChanged;
    public event EventHandler<(int OrderId, int ErrorCode, string ErrorMessage)>? OrderError;

    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (ShouldConnectSucceed)
        {
            _isConnected = true;
            _state = ConnectionState.Connected;
            ConnectionStateChanged?.Invoke(this, _state);
        }
        else
        {
            _state = ConnectionState.Error;
            ConnectionStateChanged?.Invoke(this, _state);
            throw new InvalidOperationException("Mock connection failed");
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        _state = ConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(this, _state);
        return Task.CompletedTask;
    }

    public void RequestCurrentTime()
    {
        // Mock: no-op
    }

    public void RequestMarketData(
        int requestId,
        string symbol,
        string secType,
        string exchange,
        string currency = "USD",
        string genericTickList = "",
        bool snapshot = false)
    {
        // Mock: no-op
    }

    public void CancelMarketData(int requestId)
    {
        // Mock: no-op
    }

    public bool PlaceOrder(int orderId, OrderRequest request)
    {
        if (!ShouldPlaceOrderSucceed)
        {
            return false;
        }

        PlacedOrders.Add((orderId, request));

        // Simulate immediate order status callback
        Task.Run(() =>
        {
            Thread.Sleep(10); // Small delay to simulate async callback
            OrderStatusChanged?.Invoke(this, (orderId, "Submitted", 0, request.Quantity, 0.0));
        });

        return true;
    }

    public void CancelOrder(int orderId)
    {
        CancelledOrders.Add(orderId);

        // Simulate order cancelled callback
        Task.Run(() =>
        {
            Thread.Sleep(10);
            OrderStatusChanged?.Invoke(this, (orderId, "Cancelled", 0, 0, 0.0));
        });
    }

    public void RequestOpenOrders()
    {
        // Mock: no-op
    }

    public void RequestPositions()
    {
        // Mock: no-op
    }

    public void RequestAccountSummary(int requestId)
    {
        // Mock: no-op
    }

    public int GetNextOrderId()
    {
        return _nextOrderId++;
    }

    public void Dispose()
    {
        // Mock: no-op
    }

    /// <summary>
    /// Test helper: simulate order fill.
    /// </summary>
    public void SimulateOrderFill(int orderId, int quantity, double price)
    {
        OrderStatusChanged?.Invoke(this, (orderId, "Filled", quantity, 0, price));
    }

    /// <summary>
    /// Test helper: simulate order rejection.
    /// </summary>
    public void SimulateOrderRejection(int orderId, int errorCode, string message)
    {
        OrderError?.Invoke(this, (orderId, errorCode, message));
    }
}
