using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel.Ibkr;
using TradingSupervisorService.Ibkr;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests.Workers;

/// <summary>
/// Unit tests for MarketDataCollector (Phase 7.1).
/// Focuses on:
///   * Constructor validation (negative-first guards).
///   * Callback-wiring correctness: once TickPriceReceived fires for a subscribed
///     reqId, the collector's internal state reflects it.
///   * Skipping emission when no tick has arrived (graceful degradation).
/// We do NOT start the BackgroundService loop here (that's covered by
/// WorkerLifecycleIntegrationTests). Event-driven code is exercised via direct
/// invocation of TwsCallbackHandler + a short ExecuteAsync cycle.
/// </summary>
public sealed class MarketDataCollectorTests
{
    [Fact]
    public void Constructor_WithValidConfig_Succeeds()
    {
        MarketDataCollector worker = BuildWorker();
        Assert.NotNull(worker);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_InvalidQuoteInterval_Throws(int quoteSeconds)
    {
        Dictionary<string, string?> settings = new()
        {
            ["MarketDataCollector:Enabled"] = "true",
            ["MarketDataCollector:QuoteIntervalSeconds"] = quoteSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["MarketDataCollector:AccountIntervalSeconds"] = "60"
        };

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IIbkrClient> ibkr = new();
        Mock<IOutboxRepository> outbox = new();
        TwsCallbackHandler handler = new(NullLogger<TwsCallbackHandler>.Instance, _ => { });

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new MarketDataCollector(
            NullLogger<MarketDataCollector>.Instance,
            ibkr.Object,
            outbox.Object,
            handler,
            config));

        Assert.Contains("QuoteIntervalSeconds", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidAccountInterval_Throws(int accountSeconds)
    {
        Dictionary<string, string?> settings = new()
        {
            ["MarketDataCollector:Enabled"] = "true",
            ["MarketDataCollector:QuoteIntervalSeconds"] = "15",
            ["MarketDataCollector:AccountIntervalSeconds"] = accountSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IIbkrClient> ibkr = new();
        Mock<IOutboxRepository> outbox = new();
        TwsCallbackHandler handler = new(NullLogger<TwsCallbackHandler>.Instance, _ => { });

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new MarketDataCollector(
            NullLogger<MarketDataCollector>.Instance,
            ibkr.Object,
            outbox.Object,
            handler,
            config));

        Assert.Contains("AccountIntervalSeconds", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ExitsImmediatelyAndDoesNotSubscribe()
    {
        Dictionary<string, string?> settings = new()
        {
            ["MarketDataCollector:Enabled"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IIbkrClient> ibkr = new();
        Mock<IOutboxRepository> outbox = new();
        TwsCallbackHandler handler = new(NullLogger<TwsCallbackHandler>.Instance, _ => { });

        MarketDataCollector worker = new(
            NullLogger<MarketDataCollector>.Instance,
            ibkr.Object, outbox.Object, handler, config);

        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await worker.StartAsync(cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Disabled → never subscribed
        ibkr.Verify(
            c => c.RequestMarketData(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);

        outbox.Verify(
            o => o.InsertAsync(It.IsAny<OutboxEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnected_SubscribesToIndicesAndEmitsQuotesAfterTick()
    {
        // Very short cadence so the first cycle fires during the test window
        Dictionary<string, string?> settings = new()
        {
            ["MarketDataCollector:Enabled"] = "true",
            ["MarketDataCollector:QuoteIntervalSeconds"] = "1",
            ["MarketDataCollector:AccountIntervalSeconds"] = "60"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IIbkrClient> ibkr = new();
        ibkr.SetupGet(c => c.IsConnected).Returns(true);

        Mock<IOutboxRepository> outbox = new();
        TwsCallbackHandler handler = new(NullLogger<TwsCallbackHandler>.Instance, _ => { });

        // We'll capture the reqIds the collector passes to RequestMarketData so we can
        // simulate TWS ticks back through the callback handler.
        List<(int ReqId, string Symbol)> subscriptions = new();
        ibkr
            .Setup(c => c.RequestMarketData(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<int, string, string, string, string, string, bool>(
                (reqId, symbol, _, _, _, _, _) => subscriptions.Add((reqId, symbol)));

        MarketDataCollector worker = new(
            NullLogger<MarketDataCollector>.Instance,
            ibkr.Object, outbox.Object, handler, config);

        using CancellationTokenSource cts = new();

        await worker.StartAsync(cts.Token);

        // Give the worker a beat to subscribe (it waits for IsConnected then calls RequestMarketData)
        await Task.Delay(300);

        // Simulate a LAST price tick (field=4) for each subscribed symbol
        foreach ((int reqId, _) in subscriptions)
        {
            // Invoke the TwsCallbackHandler's tickPrice — this raises TickPriceReceived
            handler.tickPrice(reqId, 4, price: 100.0 + reqId, attribs: new IBApi.TickAttrib());
        }

        // Let at least one quote-cycle tick fire (cadence = 1s)
        await Task.Delay(1500);

        await worker.StopAsync(CancellationToken.None);

        // Assert: subscribed to 3 indices (SPX, VIX, VIX3M)
        Assert.Equal(3, subscriptions.Count);
        Assert.Contains(subscriptions, s => s.Symbol == "SPX");
        Assert.Contains(subscriptions, s => s.Symbol == "VIX");
        Assert.Contains(subscriptions, s => s.Symbol == "VIX3M");

        // Assert: at least one market_quote and one vix_snapshot Outbox entry queued
        outbox.Verify(
            o => o.InsertAsync(
                It.Is<OutboxEntry>(e => e.EventType == OutboxEventTypes.MarketQuote),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        outbox.Verify(
            o => o.InsertAsync(
                It.Is<OutboxEntry>(e => e.EventType == OutboxEventTypes.VixSnapshot),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAccountSummaryCallbackFires_EmitsAccountEquityEvent()
    {
        Dictionary<string, string?> settings = new()
        {
            ["MarketDataCollector:Enabled"] = "true",
            ["MarketDataCollector:QuoteIntervalSeconds"] = "60",
            ["MarketDataCollector:AccountIntervalSeconds"] = "1"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IIbkrClient> ibkr = new();
        ibkr.SetupGet(c => c.IsConnected).Returns(true);
        Mock<IOutboxRepository> outbox = new();
        TwsCallbackHandler handler = new(NullLogger<TwsCallbackHandler>.Instance, _ => { });

        MarketDataCollector worker = new(
            NullLogger<MarketDataCollector>.Instance,
            ibkr.Object, outbox.Object, handler, config);

        using CancellationTokenSource cts = new();
        await worker.StartAsync(cts.Token);
        await Task.Delay(300);  // allow subscription phase

        // Simulate account summary rows landing from TWS for reqId=6100 (AccountSummaryReqId)
        handler.accountSummary(6100, "DU12345", "NetLiquidation", "125000.00", "USD");
        handler.accountSummary(6100, "DU12345", "TotalCashValue", "50000.00", "USD");
        handler.accountSummary(6100, "DU12345", "BuyingPower", "250000.00", "USD");
        handler.accountSummary(6100, "DU12345", "MaintMarginReq", "15000.00", "USD");

        // Let the account cycle fire (cadence = 1s)
        await Task.Delay(1500);

        await worker.StopAsync(CancellationToken.None);

        // Outbox should have at least one account_equity event
        outbox.Verify(
            o => o.InsertAsync(
                It.Is<OutboxEntry>(e => e.EventType == OutboxEventTypes.AccountEquity),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Confirm the payload contains the expected net liq value (125000)
        outbox.Verify(
            o => o.InsertAsync(
                It.Is<OutboxEntry>(e =>
                    e.EventType == OutboxEventTypes.AccountEquity &&
                    e.PayloadJson.Contains("125000")),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_NoTicksReceived_SkipsMarketQuoteEmission()
    {
        Dictionary<string, string?> settings = new()
        {
            ["MarketDataCollector:Enabled"] = "true",
            ["MarketDataCollector:QuoteIntervalSeconds"] = "1",
            ["MarketDataCollector:AccountIntervalSeconds"] = "60"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IIbkrClient> ibkr = new();
        ibkr.SetupGet(c => c.IsConnected).Returns(true);
        Mock<IOutboxRepository> outbox = new();
        TwsCallbackHandler handler = new(NullLogger<TwsCallbackHandler>.Instance, _ => { });

        MarketDataCollector worker = new(
            NullLogger<MarketDataCollector>.Instance,
            ibkr.Object, outbox.Object, handler, config);

        using CancellationTokenSource cts = new();
        await worker.StartAsync(cts.Token);

        // Do NOT simulate any tick. Let a couple of quote cycles run.
        await Task.Delay(1800);

        await worker.StopAsync(CancellationToken.None);

        // With no ticks, the collector must not queue market_quote nor vix_snapshot.
        outbox.Verify(
            o => o.InsertAsync(
                It.Is<OutboxEntry>(e => e.EventType == OutboxEventTypes.MarketQuote),
                It.IsAny<CancellationToken>()),
            Times.Never);

        outbox.Verify(
            o => o.InsertAsync(
                It.Is<OutboxEntry>(e => e.EventType == OutboxEventTypes.VixSnapshot),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static MarketDataCollector BuildWorker()
    {
        Dictionary<string, string?> settings = new()
        {
            ["MarketDataCollector:Enabled"] = "true",
            ["MarketDataCollector:QuoteIntervalSeconds"] = "15",
            ["MarketDataCollector:AccountIntervalSeconds"] = "60"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        Mock<IIbkrClient> ibkr = new();
        Mock<IOutboxRepository> outbox = new();
        TwsCallbackHandler handler = new(NullLogger<TwsCallbackHandler>.Instance, _ => { });

        return new MarketDataCollector(
            NullLogger<MarketDataCollector>.Instance,
            ibkr.Object, outbox.Object, handler, config);
    }
}
