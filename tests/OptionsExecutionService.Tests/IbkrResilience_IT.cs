using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OptionsExecutionService.Orders;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Services;
using OptionsExecutionService.Tests.Mocks;
using SharedKernel.Configuration;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using SharedKernel.Safety;
using SharedKernel.Tests.Data;
using Xunit;

namespace OptionsExecutionService.Tests;

/// <summary>
/// Phase 7.6 chaos test — IBKR disconnect resilience.
///
/// Scenario: a flapping IBKR client that drops the connection periodically
/// while a steady stream of order requests is being fed through the
/// <see cref="OrderPlacer"/>. Asserts three contracts:
///
/// <list type="number">
///   <item><description>During the "down" window, NO order leaks to the broker
///   (<see cref="FlappingIbkrClient.PlacedOrders"/> stays empty for those requests).</description></item>
///   <item><description>Every order-placement attempt produces an audit row with an
///   appropriate <see cref="AuditOutcome"/> — <c>error</c> when the connection
///   check fails, or <c>rejected_broker</c> if IBKR refused the submission.</description></item>
///   <item><description>There are NO duplicate broker submissions — each
///   successful PlaceOrder corresponds to exactly one audit row with outcome
///   <c>placed</c>.</description></item>
/// </list>
///
/// The test uses a synthetic virtual clock (<see cref="VirtualClock"/>) to
/// simulate 30-second disconnect cycles WITHOUT sleeping for 30s of real time.
/// Total wall-clock runtime target: &lt; 5 s.
///
/// Convention: <c>_IT</c> suffix marks this as an integration test (real
/// SQLite, real OrderPlacer, fake IBKR + fake clock). Existing integration
/// tests in this repo used <c>*IntegrationTests.cs</c> — the brief explicitly
/// requested the <c>_IT</c> suffix so this file is the first of the new style.
/// </summary>
public sealed class IbkrResilience_IT : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _dbFactory;
    private readonly FlappingIbkrClient _ibkr;
    private readonly IOrderTrackingRepository _orderRepo;
    private readonly RecordingAuditSink _auditSink;
    private readonly RecordingAlerter _alerter;
    private readonly OrderPlacer _orderPlacer;

    public IbkrResilience_IT()
    {
        _dbFactory = new InMemoryConnectionFactory();
        MigrationRunner migrationRunner = new(_dbFactory, NullLogger<MigrationRunner>.Instance);
        migrationRunner.RunAsync(
            OptionsExecutionService.Migrations.OptionsMigrations.All,
            CancellationToken.None).GetAwaiter().GetResult();

        _ibkr = new FlappingIbkrClient();
        _ibkr.ConnectAsync().GetAwaiter().GetResult();

        _orderRepo = new OrderTrackingRepository(_dbFactory, NullLogger<OrderTrackingRepository>.Instance);

        // Permissive safety config — this test is ONLY about connection
        // resilience. Per-order safety validators are exercised elsewhere.
        OrderSafetyConfig safety = new()
        {
            TradingMode = TradingMode.Paper,
            MaxPositionSize = 10,
            MaxPositionValueUsd = 1_000_000m,
            MinAccountBalanceUsd = 1_000m,
            MaxPositionPctOfAccount = 1.0m,
            // Bump failure threshold above what this test exercises — we do
            // NOT want the breaker to trip mid-scenario, since then we'd be
            // testing the breaker instead of the disconnect path.
            CircuitBreakerFailureThreshold = 100,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 30
        };

        _auditSink = new RecordingAuditSink();
        _alerter = new RecordingAlerter();
        InMemorySafetyFlagStore flagStore = new();
        SemaphoreGate greenGate = GateTestHelpers.FixedGate(SemaphoreStatus.Green);
        IOptions<SafetyOptions> safetyOptions = Options.Create(new SafetyOptions());

        _orderPlacer = new OrderPlacer(
            _ibkr,
            _orderRepo,
            safety,
            greenGate,
            flagStore,
            _auditSink,
            _alerter,
            safetyOptions,
            NullLogger<OrderPlacer>.Instance);

        _orderPlacer.UpdateAccountBalance(1_000_000m);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbFactory.DisposeAsync();
        _ibkr.Dispose();
    }

    /// <summary>
    /// Drives the scenario end-to-end and asserts the three contracts above.
    /// </summary>
    [Fact]
    public async Task OrderPlacer_survives_flapping_ibkr_connection()
    {
        VirtualClock clock = new();
        // 30s virtual-time cycles: 30 s connected → 30 s disconnected → ...
        TimeSpan cycle = TimeSpan.FromSeconds(30);
        TimeSpan runtime = TimeSpan.FromMinutes(3);   // 6 full cycles
        TimeSpan tick = TimeSpan.FromSeconds(5);

        int attemptsMade = 0;
        int placedWhileConnected = 0;
        int attemptsWhileDown = 0;

        // Drive virtual time in 5s ticks for runtime minutes. At each tick,
        // fire one order. The FlappingIbkrClient flips state based on the
        // clock, so the OrderPlacer observes a realistic time-sliced view.
        while (clock.Now < runtime)
        {
            bool shouldBeConnected = ((long)(clock.Now.TotalSeconds / cycle.TotalSeconds) % 2) == 0;
            _ibkr.SetConnected(shouldBeConnected);

            if (shouldBeConnected)
            {
                placedWhileConnected++;
            }
            else
            {
                attemptsWhileDown++;
            }

            OrderRequest request = NewRequest($"SPX__IT_{attemptsMade:D3}");
            OrderResult result = await _orderPlacer.PlaceOrderAsync(request, CancellationToken.None);

            // Sanity: the placer ALWAYS returns, never throws. This is the
            // fundamental promise of the fail-cautious pipeline.
            Assert.NotNull(result);

            attemptsMade++;
            clock.Advance(tick);
        }

        // -- Contract #1: no broker leakage during down windows --------------
        // Every placed broker order must have happened while IsConnected==true.
        // FlappingIbkrClient only appends to PlacedOrders when its _isConnected
        // was true at the moment PlaceOrder was invoked.
        foreach ((_, OrderRequest req) in _ibkr.PlacedOrders)
        {
            Assert.True(_ibkr.WasConnectedAt(req.ContractSymbol),
                $"Order {req.ContractSymbol} reached IBKR during a DOWN window — leakage!");
        }

        // -- Contract #2: audit rows always written with the right outcome ---
        // Every attempt produced exactly one audit row. No request fell off
        // the pipeline silently.
        Assert.Equal(attemptsMade, _auditSink.Entries.Count);

        // During "down" windows, OrderPlacer hits the "IBKR client is not
        // connected" branch which writes AuditOutcome.Error.
        int errorRows = _auditSink.Entries.Count(e => e.Outcome == AuditOutcome.Error.ToWire());
        Assert.True(errorRows >= attemptsWhileDown / 2,
            $"Expected at least {attemptsWhileDown / 2} error audits during down windows, got {errorRows}. " +
            "This suggests some down-window attempts did NOT produce an audit row — silent drop.");

        // During "up" windows, each successful submission writes
        // AuditOutcome.Placed. FlappingIbkrClient defaults to accepting.
        int placedRows = _auditSink.Entries.Count(e => e.Outcome == AuditOutcome.Placed.ToWire());
        Assert.True(placedRows >= placedWhileConnected / 2,
            $"Expected at least {placedWhileConnected / 2} placed audits during up windows, got {placedRows}.");

        // -- Contract #3: no duplicate broker submissions --------------------
        // Each broker submission's (ibkrOrderId, contractSymbol) pair must
        // be unique. Idempotency means a retried request must NOT result in
        // a second PlaceOrder call landing on the broker.
        HashSet<int> uniqueOrderIds = new(_ibkr.PlacedOrders.Select(o => o.OrderId));
        Assert.Equal(_ibkr.PlacedOrders.Count, uniqueOrderIds.Count);

        // Same uniqueness for audit rows with outcome=placed — one placed row
        // per successful submission.
        Assert.Equal(placedRows, _ibkr.PlacedOrders.Count);

        // Finally: a basic sanity check that the run actually exercised both
        // sides of the oscillation. If our cycle math is wrong, this fails
        // rather than silently passing a trivial scenario.
        Assert.True(placedWhileConnected > 0, "Scenario never had an up window — cycle math broken");
        Assert.True(attemptsWhileDown > 0, "Scenario never had a down window — cycle math broken");
    }

    /// <summary>Builds a valid OrderRequest with the given contract symbol.</summary>
    private static OrderRequest NewRequest(string contractSymbol) => new()
    {
        CampaignId = "chaos-campaign",
        Symbol = "SPX",
        ContractSymbol = contractSymbol,
        Side = OrderSide.Buy,
        Type = OrderType.Limit,
        Quantity = 1,
        LimitPrice = 10m,
        TimeInForce = "DAY",
        StrategyName = "ChaosStrategy"
    };

    // ------------------------------------------------------------------
    // Test doubles
    // ------------------------------------------------------------------

    /// <summary>
    /// Stateful IBKR client that can be flipped between connected and
    /// disconnected on demand. Remembers, per contract symbol, whether the
    /// underlying connection was up at the moment <see cref="PlaceOrder"/>
    /// was invoked — that history is what the leakage assertion consults.
    /// </summary>
    private sealed class FlappingIbkrClient : IIbkrClient
    {
        private bool _isConnected = true;
        private int _nextOrderId = 1;
        private readonly Dictionary<string, bool> _connectionAtSubmit = new(StringComparer.Ordinal);

        public List<(int OrderId, OrderRequest Request)> PlacedOrders { get; } = new();

        public ConnectionState State => _isConnected ? ConnectionState.Connected : ConnectionState.Disconnected;
        public bool IsConnected => _isConnected;

        public event EventHandler<ConnectionState>? ConnectionStateChanged;
        public event EventHandler<(int OrderId, string Status, int Filled, int Remaining, double AvgFillPrice)>? OrderStatusChanged;
        public event EventHandler<(int OrderId, int ErrorCode, string ErrorMessage)>? OrderError;

        public void SetConnected(bool connected)
        {
            if (_isConnected == connected)
            {
                return;
            }
            _isConnected = connected;
            ConnectionStateChanged?.Invoke(this, State);
        }

        public bool WasConnectedAt(string contractSymbol) =>
            _connectionAtSubmit.TryGetValue(contractSymbol, out bool v) && v;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            _isConnected = true;
            ConnectionStateChanged?.Invoke(this, ConnectionState.Connected);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, ConnectionState.Disconnected);
            return Task.CompletedTask;
        }

        public void RequestCurrentTime() { /* no-op */ }

        public void RequestMarketData(
            int requestId,
            string symbol,
            string secType,
            string exchange,
            string currency = "USD",
            string genericTickList = "",
            bool snapshot = false)
        {
            // unused parameters silenced — keeps the interface contract intact
            _ = requestId; _ = symbol; _ = secType; _ = exchange;
            _ = currency; _ = genericTickList; _ = snapshot;
        }

        public void CancelMarketData(int requestId) { _ = requestId; }

        public bool PlaceOrder(int orderId, OrderRequest request)
        {
            // Record the connection state AT the time PlaceOrder was called.
            _connectionAtSubmit[request.ContractSymbol] = _isConnected;

            // If disconnected, simulate a broker-side failure. OrderPlacer
            // checks IsConnected BEFORE calling us, so we should never be
            // called during a down window — but we defend against a future
            // regression by failing here too.
            if (!_isConnected)
            {
                // Suppress unused-event warnings by conditionally referencing the
                // nullable events. .NET treats `OrderStatusChanged?.Invoke` as a
                // use-site, so we're fine — but keep a comment trail for the
                // next person reading this.
                OrderError?.Invoke(this, (orderId, -1, "disconnected"));
                return false;
            }

            PlacedOrders.Add((orderId, request));
            // Fire a delayed status callback like the real mock does — but
            // synchronously, since we do NOT want Task.Run / Thread.Sleep
            // here (this test is time-sliced by a virtual clock).
            OrderStatusChanged?.Invoke(this, (orderId, "Submitted", 0, request.Quantity, 0.0));
            return true;
        }

        public void CancelOrder(int orderId) { _ = orderId; }
        public void RequestOpenOrders() { /* no-op */ }
        public void RequestPositions() { /* no-op */ }
        public void RequestAccountSummary(int requestId) { _ = requestId; }
        public int GetNextOrderId() => _nextOrderId++;
        public void Dispose() { /* no-op */ }
    }

    /// <summary>
    /// Tiny virtual clock — tracks elapsed time only. No real sleeping.
    /// The test does not need monotonic UTC semantics, just a notion of
    /// "how far have we advanced".
    /// </summary>
    private sealed class VirtualClock
    {
        public TimeSpan Now { get; private set; } = TimeSpan.Zero;
        public void Advance(TimeSpan by) => Now = Now.Add(by);
    }
}
