using System.Globalization;
using System.Text.Json;
using TradingSupervisorService.Repositories;
using Xunit;

namespace TradingSupervisorService.ContractTests;

/// <summary>
/// Phase 7.6 — Contract tests between .NET (producer) and Worker (consumer)
/// for each outbox event type. For every <see cref="OutboxEventTypes"/> entry,
/// we build a canonical DTO using the same serialization options the real
/// producer uses, then assert it matches the fixture under
/// <c>tests/Contract/fixtures/outbox-events/&lt;type&gt;.json</c> via
/// order-independent structural comparison.
///
/// If a test fails, EITHER the code drifted from the contract OR the fixture
/// needs updating. Both sides of the contract MUST be updated together —
/// see <c>tests/Contract/README.md</c>.
/// </summary>
public sealed class OutboxEventContractTests
{
    // Producer-side serialization options, mirrored from each emitter.
    // If an emitter changes its options, this copy MUST be updated too.
    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ---------------------------------------------------------------------
    // heartbeat — PayloadJson is a serialized ServiceHeartbeat with
    //             JsonNamingPolicy.CamelCase. See HeartbeatWorker.cs L124.
    // ---------------------------------------------------------------------
    [Fact]
    public void Heartbeat_payload_matches_fixture()
    {
        ServiceHeartbeat heartbeat = new()
        {
            ServiceName = "TradingSupervisorService",
            Hostname = "padosoft-prod-01",
            LastSeenAt = "2026-04-20T14:30:00.0000000Z",
            UptimeSeconds = 86400,
            CpuPercent = 4.2,
            RamPercent = 18.1,
            DiskFreeGb = 112.5,
            DiskTotalGb = 256.0,
            NetworkKbps = 42.5,
            TradingMode = "paper",
            Version = "0.1.0",
            CreatedAt = "2026-04-20T14:30:00.0000000Z",
            UpdatedAt = "2026-04-20T14:30:00.0000000Z"
        };

        string actual = JsonSerializer.Serialize(heartbeat, CamelCase);
        string expected = FixtureLoader.ReadOutboxEventFixture("heartbeat");

        AssertStructurallyEqual(expected, actual);
    }

    // ---------------------------------------------------------------------
    // alert — AlertRecord with CamelCase. GreeksMonitorWorker.cs L325.
    // ---------------------------------------------------------------------
    [Fact]
    public void Alert_payload_matches_fixture()
    {
        AlertRecord alert = new()
        {
            AlertId = "alert-20260420-1001",
            AlertType = "greeks_high_delta",
            Severity = "warning",
            Message = "High delta risk: position SPY has delta 0.85 (threshold 0.80)",
            DetailsJson = "{\"positionId\":\"p-123\",\"delta\":0.85,\"threshold\":0.80}",
            SourceService = "TradingSupervisorService",
            CreatedAt = "2026-04-20T14:30:00.0000000Z",
            ResolvedAt = null,
            ResolvedBy = null
        };

        string actual = JsonSerializer.Serialize(alert, CamelCase);
        string expected = FixtureLoader.ReadOutboxEventFixture("alert");

        AssertStructurallyEqual(expected, actual);
    }

    // ---------------------------------------------------------------------
    // account_equity — real producer payload from MarketDataCollector
    // (EmitAccountEquityAsync). Anonymous object, SnakeCaseLower policy.
    // Producer emits: date, account_id, account_value, cash, buying_power,
    // margin_used. It does NOT emit margin_used_pct — the Worker computes
    // that ratio downstream (see handleAccountEquity in ingest.ts).
    // ---------------------------------------------------------------------
    [Fact]
    public void AccountEquity_producer_payload_matches_fixture()
    {
        object payload = new
        {
            date = "2026-04-20",
            account_id = "DU1234567",
            account_value = 125400.50,
            cash = 54200.00,
            buying_power = 98000.00,
            margin_used = 27200.50
        };

        string actual = JsonSerializer.Serialize(payload, SnakeCase);
        string expected = FixtureLoader.ReadOutboxEventFixture("account_equity");

        AssertStructurallyEqual(expected, actual);
    }

    // ---------------------------------------------------------------------
    // market_quote — real producer payload from MarketDataCollector
    // (L441). Producer emits symbol/date/OHLC + prev_close + volume.
    // ---------------------------------------------------------------------
    [Fact]
    public void MarketQuote_producer_payload_matches_fixture()
    {
        object payload = new
        {
            symbol = "SPX",
            date = "2026-04-20",
            open = 5402.10,
            high = 5415.88,
            low = 5398.22,
            close = 5410.25,
            prev_close = 5395.40,
            volume = 2543211
        };

        string actual = JsonSerializer.Serialize(payload, SnakeCase);
        string expected = FixtureLoader.ReadOutboxEventFixture("market_quote");

        AssertStructurallyEqual(expected, actual);
    }

    // ---------------------------------------------------------------------
    // vix_snapshot — real producer payload from MarketDataCollector
    // (EmitVixSnapshotAsync, L502). Producer ONLY emits date/vix/vix3m.
    // The Worker schema optionally accepts vix1d/vix6m but the producer
    // doesn't subscribe to those contracts today.
    // ---------------------------------------------------------------------
    [Fact]
    public void VixSnapshot_producer_payload_matches_fixture()
    {
        object payload = new
        {
            date = "2026-04-20",
            vix = 14.22,
            vix3m = 15.10
        };

        string actual = JsonSerializer.Serialize(payload, SnakeCase);
        string expected = FixtureLoader.ReadOutboxEventFixture("vix_snapshot");

        AssertStructurallyEqual(expected, actual);
    }

    // ---------------------------------------------------------------------
    // benchmark_close — real producer payload from BenchmarkCollector
    // (QueueBenchmarkCloseEventAsync, L419). Producer emits symbol, date,
    // close only. Worker schema optionally accepts close_normalized for
    // pre-normalized (base-100) series, but the collector defers that
    // math to D1 (there's no normalization step in the .NET side yet).
    // ---------------------------------------------------------------------
    [Fact]
    public void BenchmarkClose_producer_payload_matches_fixture()
    {
        object payload = new
        {
            symbol = "SPX",
            date = "2026-04-20",
            close = 5410.25
        };

        string actual = JsonSerializer.Serialize(payload, SnakeCase);
        string expected = FixtureLoader.ReadOutboxEventFixture("benchmark_close");

        AssertStructurallyEqual(expected, actual);
    }

    // ---------------------------------------------------------------------
    // position_greeks — GreeksMonitorWorker.cs L732, explicit SnakeCase.
    // ---------------------------------------------------------------------
    [Fact]
    public void PositionGreeks_payload_matches_fixture()
    {
        object payload = new
        {
            position_id = "p-abc123",
            snapshot_ts = "2026-04-20T14:30:00.0000000Z",
            delta = 0.35,
            gamma = 0.015,
            theta = -12.50,
            vega = 8.20,
            iv = 0.22,
            underlying_price = 5410.25
        };

        string actual = JsonSerializer.Serialize(payload, SnakeCase);
        string expected = FixtureLoader.ReadOutboxEventFixture("position_greeks");

        AssertStructurallyEqual(expected, actual);
    }

    // ---------------------------------------------------------------------
    // order_audit — NOT ingested via OutboxRepository (has its own
    // OutboxOrderAuditSink with explicit snake_case [JsonPropertyName] on
    // each field). We mimic that exact payload here.
    // ---------------------------------------------------------------------
    [Fact]
    public void OrderAudit_payload_matches_fixture()
    {
        object payload = new
        {
            audit_id = "aud-00001111-2222-3333-4444-555566667777",
            order_id = "ord-abcdef",
            ts = "2026-04-20T14:30:00.0000000+00:00",
            actor = "system",
            strategy_id = "test-strategy",
            contract_symbol = "SPX   250321P05000000",
            side = "BUY",
            quantity = 2,
            price = (decimal?)null,
            semaphore_status = "green",
            outcome = "placed",
            override_reason = (string?)null,
            details_json = string.Format(CultureInfo.InvariantCulture, "{{\"limitPrice\":{0}}}", 15.5m)
        };

        // OrderAudit uses default naming (no policy) because each field has
        // an explicit [JsonPropertyName] — so we serialize with no naming
        // policy, matching OutboxOrderAuditSink.s_jsonOptions.
        string actual = JsonSerializer.Serialize(payload);
        string expected = FixtureLoader.ReadOutboxEventFixture("order_audit");

        AssertStructurallyEqual(expected, actual);
    }

    // ---------------------------------------------------------------------
    // Sentinel test — if a new event type is added to OutboxEventTypes but
    // no fixture is created, this fails loudly rather than silently
    // letting the contract drift.
    // ---------------------------------------------------------------------
    [Fact]
    public void Every_known_event_type_has_a_fixture()
    {
        List<string> missing = new();
        foreach (string eventType in OutboxEventTypes.All)
        {
            try
            {
                string _ = FixtureLoader.ReadOutboxEventFixture(eventType);
            }
            catch (FileNotFoundException)
            {
                missing.Add(eventType);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"Missing contract fixtures for event types: {string.Join(", ", missing)}. " +
            "Add a file under tests/Contract/fixtures/outbox-events/<type>.json and a " +
            "test in this class. See tests/Contract/README.md.");
    }

    // ---------------------------------------------------------------------
    // Test helper
    // ---------------------------------------------------------------------
    private static void AssertStructurallyEqual(string expectedJson, string actualJson)
    {
        List<string> diffs = JsonStructuralCompare.Diff(expectedJson, actualJson);
        if (diffs.Count == 0)
        {
            return;
        }

        string joined = string.Join("\n  - ", diffs);
        Assert.Fail(
            $"Contract drift detected between fixture and producer.\n" +
            $"Differences:\n  - {joined}\n\n" +
            $"Expected (fixture):\n{expectedJson}\n\n" +
            $"Actual (serialized):\n{actualJson}\n\n" +
            $"See tests/Contract/README.md for the procedure.");
    }
}
