namespace TradingSupervisorService.Repositories;

/// <summary>
/// Constants for OutboxEntry.EventType values.
/// EventType is stored as a plain string in sync_outbox (no enum schema constraint),
/// and these constants represent the canonical set of known event types.
/// Values are snake_case to match the JSON serialization contract consumed by the
/// Cloudflare Worker ingest endpoint (see infra/cloudflare/worker/src/routes/ingest.ts).
/// </summary>
public static class OutboxEventTypes
{
    // ---------- Pre-existing event types (already emitted by workers) ----------

    /// <summary>Service heartbeat snapshot (CPU, RAM, disk, uptime). Emitted by HeartbeatWorker.</summary>
    public const string Heartbeat = "heartbeat";

    /// <summary>Alert record raised by any monitoring worker (IVTS, Greeks, log-scrape, etc.).</summary>
    public const string Alert = "alert";

    // ---------- Phase 7.1 Market-data ingestion event types (new) ----------

    /// <summary>Daily / intraday IBKR account equity snapshot (account_value, cash, buying_power, margin_used).</summary>
    public const string AccountEquity = "account_equity";

    /// <summary>OHLCV market quote for a symbol (SPX, VIX, VIX3M, benchmarks). Emitted by MarketDataCollector.</summary>
    public const string MarketQuote = "market_quote";

    /// <summary>Denormalized VIX term-structure snapshot (vix, vix3m, optionally vix1d/vix6m).</summary>
    public const string VixSnapshot = "vix_snapshot";

    /// <summary>Daily benchmark close (S&amp;P 500, SWDA) from Stooq / Yahoo. Emitted by BenchmarkCollector.</summary>
    public const string BenchmarkClose = "benchmark_close";

    /// <summary>Live per-position Greeks update (delta, gamma, theta, vega, iv, underlying_price).</summary>
    public const string PositionGreeks = "position_greeks";

    /// <summary>
    /// Known event-type strings. Used for validation / documentation.
    /// Keep in sync with the Zod discriminator in the Cloudflare Worker's ingest route.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Heartbeat,
        Alert,
        AccountEquity,
        MarketQuote,
        VixSnapshot,
        BenchmarkClose,
        PositionGreeks
    };
}
