using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OptionsExecutionService.Orders;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Services;
using OptionsExecutionService.Tests.Mocks;
using SharedKernel.Configuration;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Safety;
using SharedKernel.Tests.Data;
using Xunit;

namespace OptionsExecutionService.Tests.Orders;

/// <summary>
/// Tests for the Phase 7.4 gate pipeline on <see cref="OrderPlacer"/>:
/// <list type="bullet">
///   <item><description>Gate #1 — Semaphore RED blocks; override bypasses.</description></item>
///   <item><description>Gate #2 — trading_paused flag blocks.</description></item>
///   <item><description>Audit sink captures one row per outcome (placed/rejected_*).</description></item>
///   <item><description>IbkrFailureType.NetworkError does NOT count toward the breaker.</description></item>
/// </list>
/// </summary>
public sealed class OrderPlacerSafetyGateTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _db;
    private readonly MockIbkrClient _ibkr;
    private readonly IOrderTrackingRepository _orderRepo;
    private readonly OrderSafetyConfig _safetyConfig;
    private readonly RecordingAlerter _alerter;
    private readonly InMemorySafetyFlagStore _flagStore;
    private readonly RecordingAuditSink _auditSink;

    public OrderPlacerSafetyGateTests()
    {
        _db = new InMemoryConnectionFactory();
        MigrationRunner runner = new(_db, NullLogger<MigrationRunner>.Instance);
        runner.RunAsync(OptionsExecutionService.Migrations.OptionsMigrations.All, CancellationToken.None)
            .GetAwaiter().GetResult();

        _ibkr = new MockIbkrClient();
        _ibkr.ConnectAsync().GetAwaiter().GetResult();

        _orderRepo = new OrderTrackingRepository(_db, NullLogger<OrderTrackingRepository>.Instance);
        _safetyConfig = new OrderSafetyConfig
        {
            TradingMode = TradingMode.Paper,
            MaxPositionSize = 10,
            MaxPositionValueUsd = 50000m,
            MinAccountBalanceUsd = 10000m,
            MaxPositionPctOfAccount = 0.2m,
            CircuitBreakerFailureThreshold = 3,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 30
        };
        _alerter = new RecordingAlerter();
        _flagStore = new InMemorySafetyFlagStore();
        _auditSink = new RecordingAuditSink();
    }

    public async ValueTask DisposeAsync()
    {
        _ibkr.Dispose();
        await _db.DisposeAsync();
    }

    private OrderPlacer BuildPlacer(SemaphoreStatus gateStatus, SafetyOptions? options = null)
    {
        SemaphoreGate gate = GateTestHelpers.FixedGate(gateStatus);
        OrderPlacer placer = new(
            _ibkr,
            _orderRepo,
            _safetyConfig,
            gate,
            _flagStore,
            _auditSink,
            _alerter,
            Options.Create(options ?? new SafetyOptions()),
            NullLogger<OrderPlacer>.Instance);
        placer.UpdateAccountBalance(100000m);
        return placer;
    }

    private static OrderRequest ValidRequest() => new()
    {
        CampaignId = "c1",
        Symbol = "SPX",
        ContractSymbol = "SPX   250321P05000000",
        Side = OrderSide.Buy,
        Type = OrderType.Limit,
        Quantity = 1,
        LimitPrice = 12.5m,
        StrategyName = "TestStrategy"
    };

    // ---------------------------------------------------------------
    // Gate #1: Semaphore
    // ---------------------------------------------------------------

    [Fact]
    public async Task RedSemaphore_NoOverride_BlocksWithAudit()
    {
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Red);

        OrderResult result = await placer.PlaceOrderAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Contains("SemaphoreGate", result.Error);
        Assert.Empty(_ibkr.PlacedOrders); // never reached IBKR

        // Exactly one audit row, with outcome=rejected_semaphore.
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.RejectedSemaphore.ToWire(), _auditSink.Entries[0].Outcome);
        Assert.Equal("semaphore-red", _auditSink.Entries[0].OverrideReason);

        // Critical alert fired.
        Assert.Single(_alerter.Sent);
        Assert.Equal(AlertSeverity.Critical, _alerter.Sent[0].Severity);
    }

    [Fact]
    public async Task RedSemaphore_WithOverride_AllowsOrder()
    {
        OrderPlacer placer = BuildPlacer(
            SemaphoreStatus.Red,
            new SafetyOptions { OverrideSemaphore = true });

        OrderResult result = await placer.PlaceOrderAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.Single(_ibkr.PlacedOrders);
        // Audit row with outcome=placed
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.Placed.ToWire(), _auditSink.Entries[0].Outcome);
    }

    [Fact]
    public async Task GreenSemaphore_AllowsOrder()
    {
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);

        OrderResult result = await placer.PlaceOrderAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.Placed.ToWire(), _auditSink.Entries[0].Outcome);
        Assert.Equal(SemaphoreStatus.Green.ToWire(), _auditSink.Entries[0].SemaphoreStatus);
    }

    // ---------------------------------------------------------------
    // Gate #2: DailyPnLWatcher pause flag
    // ---------------------------------------------------------------

    [Fact]
    public async Task PauseFlag_BlocksWithPnlPauseAudit()
    {
        await _flagStore.SetAsync("trading_paused", "1", CancellationToken.None);
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);

        OrderResult result = await placer.PlaceOrderAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Contains("pnl-paused", result.Error);
        Assert.Empty(_ibkr.PlacedOrders);
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.RejectedPnlPause.ToWire(), _auditSink.Entries[0].Outcome);
    }

    [Fact]
    public async Task PauseFlag_ValueZero_AllowsOrder()
    {
        // Strict IsSet: only "1" blocks. "0" is treated as unset.
        await _flagStore.SetAsync("trading_paused", "0", CancellationToken.None);
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);

        OrderResult result = await placer.PlaceOrderAsync(ValidRequest());

        Assert.True(result.Success);
    }

    // ---------------------------------------------------------------
    // Validator audit mapping
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExceedsMaxSize_AuditedAsMaxSize()
    {
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);

        OrderRequest req = ValidRequest() with { Quantity = 99 };
        OrderResult result = await placer.PlaceOrderAsync(req);

        Assert.False(result.Success);
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.RejectedMaxSize.ToWire(), _auditSink.Entries[0].Outcome);
    }

    [Fact]
    public async Task BelowMinBalance_AuditedAsMinBalance()
    {
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);
        placer.UpdateAccountBalance(5000m); // below the 10k minimum

        OrderResult result = await placer.PlaceOrderAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.RejectedMinBalance.ToWire(), _auditSink.Entries[0].Outcome);
    }

    [Fact]
    public async Task ExceedsMaxValue_AuditedAsMaxValue()
    {
        OrderSafetyConfig strictValue = _safetyConfig with { MaxPositionValueUsd = 100m };
        SemaphoreGate gate = GateTestHelpers.FixedGate(SemaphoreStatus.Green);
        OrderPlacer placer = new(
            _ibkr,
            _orderRepo,
            strictValue,
            gate,
            _flagStore,
            _auditSink,
            _alerter,
            Options.Create(new SafetyOptions()),
            NullLogger<OrderPlacer>.Instance);
        placer.UpdateAccountBalance(100000m);

        OrderResult result = await placer.PlaceOrderAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.RejectedMaxValue.ToWire(), _auditSink.Entries[0].Outcome);
    }

    [Fact]
    public async Task ExceedsMaxRisk_AuditedAsMaxRisk()
    {
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);
        // With balance=20000 and 20% cap, max value is 4000. 1 * 100 * 100 = 10,000 > 4000.
        placer.UpdateAccountBalance(20000m);

        OrderRequest req = ValidRequest() with { LimitPrice = 100m };
        OrderResult result = await placer.PlaceOrderAsync(req);

        Assert.False(result.Success);
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.RejectedMaxRisk.ToWire(), _auditSink.Entries[0].Outcome);
    }

    // ---------------------------------------------------------------
    // IbkrFailureType classification
    // ---------------------------------------------------------------

    [Fact]
    public async Task RecordIbkrFailure_NetworkError_DoesNotTripBreaker()
    {
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);

        for (int i = 0; i < 10; i++)
        {
            await placer.RecordIbkrFailureAsync(IbkrFailureType.NetworkError, CancellationToken.None);
        }

        Assert.False(placer.IsCircuitBreakerOpen());
    }

    [Fact]
    public async Task BrokerReject_CountsThreeTimes_TripsBreaker()
    {
        _ibkr.ShouldPlaceOrderSucceed = false;
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);

        // 3 failing order attempts → breaker opens (threshold=3).
        await placer.PlaceOrderAsync(ValidRequest());
        await placer.PlaceOrderAsync(ValidRequest());
        await placer.PlaceOrderAsync(ValidRequest());

        Assert.True(placer.IsCircuitBreakerOpen());

        // 4th attempt → rejected_breaker audit row.
        _auditSink.Entries.Clear();
        OrderResult result = await placer.PlaceOrderAsync(ValidRequest());
        Assert.False(result.Success);
        Assert.Single(_auditSink.Entries);
        Assert.Equal(AuditOutcome.RejectedBreaker.ToWire(), _auditSink.Entries[0].Outcome);
    }

    [Fact]
    public async Task TwoNetworkErrors_PlusTwoRejects_BreakerStillClosed()
    {
        _ibkr.ShouldPlaceOrderSucceed = false;
        OrderPlacer placer = BuildPlacer(SemaphoreStatus.Green);

        // Two network-class failures (should be ignored by breaker math).
        await placer.RecordIbkrFailureAsync(IbkrFailureType.NetworkError, CancellationToken.None);
        await placer.RecordIbkrFailureAsync(IbkrFailureType.NetworkError, CancellationToken.None);

        // Two actual broker rejects.
        await placer.PlaceOrderAsync(ValidRequest());
        await placer.PlaceOrderAsync(ValidRequest());

        // Only 2 real failures in the DB → below threshold of 3 → breaker closed.
        Assert.False(placer.IsCircuitBreakerOpen());
    }
}
