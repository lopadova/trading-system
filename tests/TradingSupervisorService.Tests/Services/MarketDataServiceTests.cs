using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Ibkr;
using SharedKernel.MarketData;
using TradingSupervisorService.Services;
using Xunit;

namespace TradingSupervisorService.Tests.Services;

/// <summary>
/// Unit tests for MarketDataService.
/// Tests subscription management, snapshot caching, DTE calculation, and callback integration.
/// </summary>
public sealed class MarketDataServiceTests : IDisposable
{
    private readonly FakeIbkrClient _fakeIbkrClient;
    private readonly MarketDataService _service;

    public MarketDataServiceTests()
    {
        _fakeIbkrClient = new FakeIbkrClient();
        _service = new MarketDataService(NullLogger<MarketDataService>.Instance, _fakeIbkrClient);
    }

    public void Dispose()
    {
        _service.Dispose();
        _fakeIbkrClient.Dispose();
    }

    [Fact]
    [Trait("TestId", "TEST-18-01")]
    public void Subscribe_ValidSymbol_ReturnsRequestId()
    {
        // Arrange & Act
        int reqId = _service.Subscribe("SPX", "IND", "CBOE", "USD", includeGreeks: false);

        // Assert
        Assert.True(reqId > 0, "Request ID should be positive");

        // Verify IBKR client was called
        Assert.Single(_fakeIbkrClient.MarketDataRequests);
        FakeIbkrClient.MarketDataRequest request = _fakeIbkrClient.MarketDataRequests[0];
        Assert.Equal("SPX", request.Symbol);
        Assert.Equal("IND", request.SecType);
        Assert.Equal("CBOE", request.Exchange);
        Assert.Equal("", request.GenericTickList);
    }

    [Fact]
    [Trait("TestId", "TEST-18-02")]
    public void Subscribe_WithGreeks_RequestsGreeksTickType()
    {
        // Arrange & Act
        int reqId = _service.Subscribe("SPX", "IND", "CBOE", "USD", includeGreeks: true);

        // Assert
        FakeIbkrClient.MarketDataRequest request = _fakeIbkrClient.MarketDataRequests[0];
        Assert.Equal("106", request.GenericTickList);
    }

    [Fact]
    [Trait("TestId", "TEST-18-03")]
    public void Subscribe_SameSymbolTwice_ReturnsSameRequestId()
    {
        // Arrange
        int reqId1 = _service.Subscribe("SPX", "IND", "CBOE");

        // Act
        int reqId2 = _service.Subscribe("SPX", "IND", "CBOE");

        // Assert
        Assert.Equal(reqId1, reqId2);
        Assert.Single(_fakeIbkrClient.MarketDataRequests); // Only one request sent
    }

    [Fact]
    [Trait("TestId", "TEST-18-04")]
    public void GetSnapshot_AfterSubscribe_ReturnsEmptySnapshot()
    {
        // Arrange
        int reqId = _service.Subscribe("SPX", "IND", "CBOE");

        // Act
        MarketDataSnapshot? snapshot = _service.GetSnapshot(reqId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal("SPX", snapshot.Symbol);
        Assert.Equal("IND", snapshot.SecType);
        Assert.Equal(reqId, snapshot.RequestId);
        Assert.Null(snapshot.LastPrice);
        Assert.Null(snapshot.BidPrice);
        Assert.Null(snapshot.AskPrice);
    }

    [Fact]
    [Trait("TestId", "TEST-18-05")]
    public void OnTickPrice_UpdatesSnapshot()
    {
        // Arrange
        int reqId = _service.Subscribe("SPX", "IND", "CBOE");

        // Act
        _service.OnTickPrice(reqId, field: 4, price: 4500.0); // Field 4 = LAST

        // Assert
        MarketDataSnapshot? snapshot = _service.GetSnapshot(reqId);
        Assert.NotNull(snapshot);
        Assert.Equal(4500.0, snapshot.LastPrice);
    }

    [Fact]
    [Trait("TestId", "TEST-18-06")]
    public void OnTickPrice_BidAndAsk_CalculatesSpread()
    {
        // Arrange
        int reqId = _service.Subscribe("SPX", "IND", "CBOE");

        // Act
        _service.OnTickPrice(reqId, field: 1, price: 4499.50); // BID
        _service.OnTickPrice(reqId, field: 2, price: 4500.50); // ASK

        // Assert
        MarketDataSnapshot? snapshot = _service.GetSnapshot(reqId);
        Assert.NotNull(snapshot);
        Assert.Equal(4499.50, snapshot.BidPrice);
        Assert.Equal(4500.50, snapshot.AskPrice);
        Assert.Equal(1.0, snapshot.Spread);
        Assert.Equal(4500.0, snapshot.MidPrice);
    }

    [Fact]
    [Trait("TestId", "TEST-18-07")]
    public void OnTickSize_UpdatesBidAskSize()
    {
        // Arrange
        int reqId = _service.Subscribe("SPX", "IND", "CBOE");

        // Act
        _service.OnTickSize(reqId, field: 0, size: 10); // BID_SIZE
        _service.OnTickSize(reqId, field: 3, size: 20); // ASK_SIZE

        // Assert
        MarketDataSnapshot? snapshot = _service.GetSnapshot(reqId);
        Assert.NotNull(snapshot);
        Assert.Equal(10, snapshot.BidSize);
        Assert.Equal(20, snapshot.AskSize);
    }

    [Fact]
    [Trait("TestId", "TEST-18-08")]
    public void OnTickOptionComputation_UpdatesGreeks()
    {
        // Arrange
        int reqId = _service.Subscribe("SPX", "OPT", "SMART", includeGreeks: true);

        // Act
        _service.OnTickOptionComputation(
            requestId: reqId,
            field: 10,
            impliedVolatility: 0.25,
            delta: 0.50,
            optPrice: 10.0,
            gamma: 0.05,
            vega: 0.15,
            theta: -0.10,
            undPrice: 4500.0);

        // Assert
        MarketDataSnapshot? snapshot = _service.GetSnapshot(reqId);
        Assert.NotNull(snapshot);
        Assert.Equal(0.25, snapshot.ImpliedVolatility);
        Assert.Equal(0.50, snapshot.Delta);
        Assert.Equal(0.05, snapshot.Gamma);
        Assert.Equal(0.15, snapshot.Vega);
        Assert.Equal(-0.10, snapshot.Theta);
        Assert.Equal(4500.0, snapshot.UnderlyingPrice);
        Assert.True(snapshot.HasGreeks);
    }

    [Fact]
    [Trait("TestId", "TEST-18-09")]
    public void OnTickOptionComputation_InvalidValues_IgnoresNegativeGreeks()
    {
        // Arrange
        int reqId = _service.Subscribe("SPX", "OPT", "SMART", includeGreeks: true);

        // Act - IBKR sends -1 or -2 for unavailable data
        _service.OnTickOptionComputation(
            requestId: reqId,
            field: 10,
            impliedVolatility: -1.0,
            delta: -10.0,
            optPrice: -1.0,
            gamma: -1.0,
            vega: -1.0,
            theta: -999.0,
            undPrice: -1.0);

        // Assert
        MarketDataSnapshot? snapshot = _service.GetSnapshot(reqId);
        Assert.NotNull(snapshot);
        Assert.Null(snapshot.ImpliedVolatility);
        Assert.Null(snapshot.Delta);
        Assert.Null(snapshot.Gamma);
        Assert.Null(snapshot.Vega);
        Assert.Null(snapshot.Theta);
        Assert.Null(snapshot.UnderlyingPrice);
    }

    [Fact]
    [Trait("TestId", "TEST-18-10")]
    public void CalculateDTE_ValidExpirationDate_ReturnsCorrectDays()
    {
        // Arrange
        DateTime tomorrow = DateTime.UtcNow.Date.AddDays(1);
        string expirationDate = tomorrow.ToString("yyyyMMdd");

        // Act
        int? dte = _service.CalculateDTE(expirationDate);

        // Assert
        Assert.Equal(1, dte);
    }

    [Fact]
    [Trait("TestId", "TEST-18-11")]
    public void CalculateDTE_Today_ReturnsZero()
    {
        // Arrange
        string expirationDate = DateTime.UtcNow.Date.ToString("yyyyMMdd");

        // Act
        int? dte = _service.CalculateDTE(expirationDate);

        // Assert
        Assert.Equal(0, dte);
    }

    [Fact]
    [Trait("TestId", "TEST-18-12")]
    public void CalculateDTE_PastDate_ReturnsZero()
    {
        // Arrange
        DateTime yesterday = DateTime.UtcNow.Date.AddDays(-1);
        string expirationDate = yesterday.ToString("yyyyMMdd");

        // Act
        int? dte = _service.CalculateDTE(expirationDate);

        // Assert
        Assert.Equal(0, dte); // DTE is clamped to 0 minimum
    }

    [Fact]
    [Trait("TestId", "TEST-18-13")]
    public void CalculateDTE_InvalidFormat_ReturnsNull()
    {
        // Arrange & Act
        int? dte = _service.CalculateDTE("invalid");

        // Assert
        Assert.Null(dte);
    }

    [Fact]
    [Trait("TestId", "TEST-18-14")]
    public void SubscribeOption_ValidParameters_CalculatesDTE()
    {
        // Arrange
        DateTime futureDate = DateTime.UtcNow.Date.AddDays(30);
        string expirationDate = futureDate.ToString("yyyyMMdd");

        // Act
        int reqId = _service.SubscribeOption("SPX", "SMART", "P", 4500.0, expirationDate, includeGreeks: true);

        // Assert
        MarketDataSnapshot? snapshot = _service.GetSnapshot(reqId);
        Assert.NotNull(snapshot);
        Assert.Equal(30, snapshot.DaysToExpiration);
        Assert.Equal(futureDate, snapshot.ExpirationDate);
    }

    [Fact]
    [Trait("TestId", "TEST-18-15")]
    public void Unsubscribe_ValidRequestId_RemovesSubscription()
    {
        // Arrange
        int reqId = _service.Subscribe("SPX", "IND", "CBOE");

        // Act
        _service.Unsubscribe(reqId);

        // Assert
        MarketDataSnapshot? snapshot = _service.GetSnapshot(reqId);
        Assert.Null(snapshot);
        Assert.Empty(_service.GetActiveSubscriptions());
        Assert.Single(_fakeIbkrClient.CanceledRequests);
        Assert.Equal(reqId, _fakeIbkrClient.CanceledRequests[0]);
    }

    [Fact]
    [Trait("TestId", "TEST-18-16")]
    public void GetSnapshotBySymbol_SubscribedSymbol_ReturnsSnapshot()
    {
        // Arrange
        _service.Subscribe("SPX", "IND", "CBOE");
        _service.GetSnapshotBySymbol("SPX")!.RequestId.Let(reqId =>
            _service.OnTickPrice(reqId, field: 4, price: 4500.0));

        // Act
        MarketDataSnapshot? snapshot = _service.GetSnapshotBySymbol("SPX");

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal("SPX", snapshot.Symbol);
        Assert.Equal(4500.0, snapshot.LastPrice);
    }

    [Fact]
    [Trait("TestId", "TEST-18-17")]
    public void GetSnapshotBySymbol_UnsubscribedSymbol_ReturnsNull()
    {
        // Arrange & Act
        MarketDataSnapshot? snapshot = _service.GetSnapshotBySymbol("UNKNOWN");

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("TestId", "TEST-18-18")]
    public void MarketDataUpdated_Event_RaisedOnPriceUpdate()
    {
        // Arrange
        int reqId = _service.Subscribe("SPX", "IND", "CBOE");
        bool eventRaised = false;
        MarketDataSnapshot? eventSnapshot = null;

        _service.MarketDataUpdated += (sender, args) =>
        {
            eventRaised = true;
            eventSnapshot = args.Snapshot;
        };

        // Act
        _service.OnTickPrice(reqId, field: 4, price: 4500.0);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(eventSnapshot);
        Assert.Equal(4500.0, eventSnapshot.LastPrice);
    }

    [Fact]
    [Trait("TestId", "TEST-18-19")]
    public void GetActiveSubscriptions_MultipleSubscriptions_ReturnsAll()
    {
        // Arrange
        int reqId1 = _service.Subscribe("SPX", "IND", "CBOE");
        int reqId2 = _service.Subscribe("VIX3M", "IND", "CBOE");

        // Act
        IReadOnlyDictionary<int, string> subscriptions = _service.GetActiveSubscriptions();

        // Assert
        Assert.Equal(2, subscriptions.Count);
        Assert.Equal("SPX", subscriptions[reqId1]);
        Assert.Equal("VIX3M", subscriptions[reqId2]);
    }

    [Fact]
    [Trait("TestId", "TEST-18-20")]
    public void Dispose_CancelsAllSubscriptions()
    {
        // Arrange
        int reqId1 = _service.Subscribe("SPX", "IND", "CBOE");
        int reqId2 = _service.Subscribe("VIX3M", "IND", "CBOE");

        // Act
        _service.Dispose();

        // Assert
        Assert.Equal(2, _fakeIbkrClient.CanceledRequests.Count);
        Assert.Contains(reqId1, _fakeIbkrClient.CanceledRequests);
        Assert.Contains(reqId2, _fakeIbkrClient.CanceledRequests);
    }
}

/// <summary>
/// Extension method for functional-style let binding (to avoid intermediate variables).
/// </summary>
internal static class Extensions
{
    public static void Let<T>(this T value, Action<T> action) => action(value);
}

/// <summary>
/// Fake IIbkrClient for testing without connecting to real IBKR.
/// </summary>
internal sealed class FakeIbkrClient : IIbkrClient
{
    public ConnectionState State => ConnectionState.Connected;
    public bool IsConnected => true;

    public List<MarketDataRequest> MarketDataRequests { get; } = new();
    public List<int> CanceledRequests { get; } = new();

#pragma warning disable CS0067 // Event never used (required by IIbkrClient interface but not needed in fake)
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<(int OrderId, string Status, int Filled, int Remaining, double AvgFillPrice)>? OrderStatusChanged;
    public event EventHandler<(int OrderId, int ErrorCode, string ErrorMessage)>? OrderError;
#pragma warning restore CS0067

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;
    public void RequestCurrentTime() { }

    public void RequestMarketData(
        int requestId,
        string symbol,
        string secType,
        string exchange,
        string currency = "USD",
        string genericTickList = "",
        bool snapshot = false)
    {
        MarketDataRequests.Add(new MarketDataRequest
        {
            RequestId = requestId,
            Symbol = symbol,
            SecType = secType,
            Exchange = exchange,
            Currency = currency,
            GenericTickList = genericTickList,
            Snapshot = snapshot
        });
    }

    public void CancelMarketData(int requestId)
    {
        CanceledRequests.Add(requestId);
    }

    public bool PlaceOrder(int orderId, SharedKernel.Domain.OrderRequest request) => false;
    public void CancelOrder(int orderId) { }
    public void RequestOpenOrders() { }
    public void RequestPositions() { }
    public void RequestAccountSummary(int requestId) { }
    public int ReserveOrderId() => 1; // RM-01: Renamed from GetNextOrderId
    public void Dispose() { }

    public sealed record MarketDataRequest
    {
        public required int RequestId { get; init; }
        public required string Symbol { get; init; }
        public required string SecType { get; init; }
        public required string Exchange { get; init; }
        public required string Currency { get; init; }
        public required string GenericTickList { get; init; }
        public required bool Snapshot { get; init; }
    }
}
