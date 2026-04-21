using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Domain;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Services;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Phase 7.1 BenchmarkCollector.
///
/// Fetches daily closing prices for benchmark indices (S&amp;P 500, SWDA) once per UTC day,
/// shortly after US market close (default 22:30 UTC), and queues one
/// <see cref="OutboxEventTypes.BenchmarkClose"/> Outbox event per new row observed.
///
/// Fallback chain per symbol:
///   1) Stooq CSV (https://stooq.com/q/d/l/?s={sym}&amp;i=d) — primary, no API key.
///   2) Yahoo Finance v8 chart JSON (query1.finance.yahoo.com) — fallback on Stooq failure.
///   3) Log WARNING + Telegram alert on total failure (no crash; retry next day).
///
/// Local dedupe state: supervisor.db.benchmark_fetch_log (Migration 003).
/// Each row tracks the most recent ISO-date we have queued for a given symbol; we only
/// insert Outbox events for rows strictly newer than <c>last_fetched_date</c> (up to
/// <c>MaxBackfillRows</c> newest rows on cold start to catch up gracefully).
/// </summary>
public sealed class BenchmarkCollector : BackgroundService
{
    private readonly ILogger<BenchmarkCollector> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOutboxRepository _outboxRepo;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ITelegramAlerter _telegramAlerter;

    private readonly bool _enabled;
    private readonly TimeSpan _dailyRunTimeUtc;
    private readonly int _checkIntervalMinutes;
    private readonly IReadOnlyList<string> _symbols;
    private readonly string _stooqUrlTemplate;
    private readonly string _yahooFallbackUrlTemplate;
    private readonly int _httpTimeoutSeconds;
    private readonly int _maxBackfillRows;

    // When we last ran a fetch cycle (to avoid running twice in the same calendar UTC day).
    private DateOnly _lastRunDateUtc = DateOnly.MinValue;

    public BenchmarkCollector(
        ILogger<BenchmarkCollector> logger,
        IHttpClientFactory httpClientFactory,
        IOutboxRepository outboxRepo,
        IDbConnectionFactory dbFactory,
        ITelegramAlerter telegramAlerter,
        IConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _outboxRepo = outboxRepo ?? throw new ArgumentNullException(nameof(outboxRepo));
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _telegramAlerter = telegramAlerter ?? throw new ArgumentNullException(nameof(telegramAlerter));

        _enabled = config.GetValue<bool>("BenchmarkCollector:Enabled", true);

        string runTimeStr = config.GetValue<string>("BenchmarkCollector:DailyRunTimeUtc", "22:30") ?? "22:30";
        if (!TimeSpan.TryParseExact(runTimeStr, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan runTime))
        {
            throw new ArgumentException(
                $"Invalid BenchmarkCollector:DailyRunTimeUtc='{runTimeStr}'. Expected HH:mm.");
        }
        _dailyRunTimeUtc = runTime;

        _checkIntervalMinutes = config.GetValue<int>("BenchmarkCollector:CheckIntervalMinutes", 30);
        if (_checkIntervalMinutes <= 0)
        {
            throw new ArgumentException(
                $"Invalid BenchmarkCollector:CheckIntervalMinutes={_checkIntervalMinutes}. Must be > 0.");
        }

        string[]? syms = config.GetSection("BenchmarkCollector:Symbols").Get<string[]>();
        _symbols = (syms is { Length: > 0 } ? syms : new[] { "SPX", "SWDA" }).ToList();

        _stooqUrlTemplate = config.GetValue<string>(
            "BenchmarkCollector:StooqUrlTemplate",
            "https://stooq.com/q/d/l/?s={0}&i=d") ?? "https://stooq.com/q/d/l/?s={0}&i=d";

        _yahooFallbackUrlTemplate = config.GetValue<string>(
            "BenchmarkCollector:YahooFallbackUrlTemplate",
            "https://query1.finance.yahoo.com/v8/finance/chart/{0}?range=5d&interval=1d")
            ?? "https://query1.finance.yahoo.com/v8/finance/chart/{0}?range=5d&interval=1d";

        _httpTimeoutSeconds = config.GetValue<int>("BenchmarkCollector:HttpTimeoutSeconds", 30);
        _maxBackfillRows = config.GetValue<int>("BenchmarkCollector:MaxBackfillRows", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation(
                "{Worker} is disabled in configuration. Not starting.", nameof(BenchmarkCollector));
            return;
        }

        _logger.LogInformation(
            "{Worker} started. DailyRunTimeUtc={RunTime} Symbols={Symbols} CheckEveryMin={CheckMin}",
            nameof(BenchmarkCollector), _dailyRunTimeUtc, string.Join(",", _symbols), _checkIntervalMinutes);

        // Main loop: wake up every _checkIntervalMinutes, check if we are past the daily
        // run time AND haven't already fetched for today's UTC calendar day.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (ShouldRunNow())
                {
                    await RunFetchCycleAsync(stoppingToken);
                    _lastRunDateUtc = DateOnly.FromDateTime(DateTime.UtcNow);
                }
            }
            catch (OperationCanceledException)
            {
                break; // cooperative shutdown
            }
            catch (Exception ex)
            {
                // NEVER crash the worker — log + continue. Next cycle will retry.
                _logger.LogError(ex, "{Worker} fetch cycle failed unexpectedly", nameof(BenchmarkCollector));
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_checkIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("{Worker} stopped", nameof(BenchmarkCollector));
    }

    private bool ShouldRunNow()
    {
        DateTime nowUtc = DateTime.UtcNow;
        DateOnly todayUtc = DateOnly.FromDateTime(nowUtc);

        // Negative-first: already ran today
        if (_lastRunDateUtc == todayUtc)
        {
            return false;
        }

        // Ran on a previous day — only fire once today's clock has passed the configured run time.
        TimeSpan nowTime = nowUtc.TimeOfDay;
        return nowTime >= _dailyRunTimeUtc;
    }

    private async Task RunFetchCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("{Worker} running daily fetch cycle", nameof(BenchmarkCollector));

        foreach (string symbol in _symbols)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await FetchSymbolAsync(symbol, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "{Worker} fetch for symbol {Symbol} failed; will retry next cycle",
                    nameof(BenchmarkCollector), symbol);
                await UpdateFetchLogFailureAsync(symbol, ex.Message, ct);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Per-symbol fetch pipeline: Stooq → Yahoo → Telegram alert
    // ---------------------------------------------------------------------
    private async Task FetchSymbolAsync(string symbol, CancellationToken ct)
    {
        DateOnly? lastSeen = await GetLastFetchedDateAsync(symbol, ct);

        IReadOnlyList<BenchmarkRow> rows;
        string source;

        try
        {
            rows = await FetchFromStooqAsync(symbol, ct);
            source = "stooq";
        }
        catch (Exception stooqEx)
        {
            _logger.LogWarning(stooqEx,
                "{Worker} Stooq fetch failed for {Symbol}; falling back to Yahoo",
                nameof(BenchmarkCollector), symbol);

            try
            {
                rows = await FetchFromYahooAsync(symbol, ct);
                source = "yahoo";
            }
            catch (Exception yahooEx)
            {
                _logger.LogWarning(yahooEx,
                    "{Worker} Yahoo fallback also failed for {Symbol}", nameof(BenchmarkCollector), symbol);
                await AlertFetchFailureAsync(symbol, stooqEx, yahooEx, ct);
                await UpdateFetchLogFailureAsync(symbol, $"stooq: {stooqEx.Message} | yahoo: {yahooEx.Message}", ct);
                return;
            }
        }

        // Filter to only new rows (strictly newer than lastSeen), at most _maxBackfillRows
        IEnumerable<BenchmarkRow> newRows = rows
            .Where(r => !lastSeen.HasValue || r.Date > lastSeen.Value)
            .OrderBy(r => r.Date)
            .TakeLast(_maxBackfillRows)
            .ToList();

        int queued = 0;
        DateOnly? maxDate = lastSeen;

        foreach (BenchmarkRow row in newRows)
        {
            await QueueBenchmarkCloseEventAsync(symbol, row, ct);
            queued++;
            if (!maxDate.HasValue || row.Date > maxDate.Value)
            {
                maxDate = row.Date;
            }
        }

        _logger.LogInformation(
            "{Worker} {Symbol}: fetched {Total} rows from {Source}, queued {New} new benchmark_close events (maxDate={Max})",
            nameof(BenchmarkCollector), symbol, rows.Count, source, queued, maxDate);

        await UpdateFetchLogSuccessAsync(symbol, maxDate, source, ct);
    }

    // ---------------------------------------------------------------------
    // Stooq CSV fetcher (format: Date,Open,High,Low,Close,Volume)
    // ---------------------------------------------------------------------
    private async Task<IReadOnlyList<BenchmarkRow>> FetchFromStooqAsync(string symbol, CancellationToken ct)
    {
        string stooqSymbol = MapSymbolToStooq(symbol);
        string url = string.Format(CultureInfo.InvariantCulture, _stooqUrlTemplate, stooqSymbol);

        HttpClient client = _httpClientFactory.CreateClient("BenchmarkCollector");
        client.Timeout = TimeSpan.FromSeconds(_httpTimeoutSeconds);

        using HttpResponseMessage resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Stooq returned HTTP {(int)resp.StatusCode} for {stooqSymbol}");
        }

        string csv = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(csv))
        {
            throw new InvalidOperationException($"Stooq returned empty body for {stooqSymbol}");
        }

        // Stooq sometimes returns an HTML "no data" page or a single 'No data' line.
        if (csv.StartsWith("<", StringComparison.Ordinal) || csv.Contains("No data", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Stooq returned no data for {stooqSymbol}");
        }

        List<BenchmarkRow> rows = new();
        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Expected header: Date,Open,High,Low,Close,Volume
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim('\r', ' ', '\t');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] parts = line.Split(',');
            if (parts.Length < 5)
            {
                continue;
            }

            if (!DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly d))
            {
                continue;
            }

            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double close))
            {
                continue;
            }

            rows.Add(new BenchmarkRow(d, close));
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"Stooq CSV parsed 0 rows for {stooqSymbol}");
        }

        return rows;
    }

    // ---------------------------------------------------------------------
    // Yahoo Finance v8 chart fallback
    // ---------------------------------------------------------------------
    private async Task<IReadOnlyList<BenchmarkRow>> FetchFromYahooAsync(string symbol, CancellationToken ct)
    {
        string yahooSymbol = MapSymbolToYahoo(symbol);
        string url = string.Format(CultureInfo.InvariantCulture, _yahooFallbackUrlTemplate, Uri.EscapeDataString(yahooSymbol));

        HttpClient client = _httpClientFactory.CreateClient("BenchmarkCollector");
        client.Timeout = TimeSpan.FromSeconds(_httpTimeoutSeconds);
        // Yahoo tends to reject the default C# user-agent; set a browser-like UA.
        if (!client.DefaultRequestHeaders.UserAgent.Any())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (TradingSystem BenchmarkCollector)");
        }

        using HttpResponseMessage resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Yahoo returned HTTP {(int)resp.StatusCode} for {yahooSymbol}");
        }

        string body = await resp.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(body);

        // Navigate: chart.result[0].timestamp[] + chart.result[0].indicators.quote[0].close[]
        if (!doc.RootElement.TryGetProperty("chart", out JsonElement chart) ||
            !chart.TryGetProperty("result", out JsonElement resultArr) ||
            resultArr.ValueKind != JsonValueKind.Array ||
            resultArr.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Yahoo response missing chart.result for {yahooSymbol}");
        }

        JsonElement result0 = resultArr[0];
        if (!result0.TryGetProperty("timestamp", out JsonElement tsArr) ||
            !result0.TryGetProperty("indicators", out JsonElement indicators) ||
            !indicators.TryGetProperty("quote", out JsonElement quoteArr) ||
            quoteArr.ValueKind != JsonValueKind.Array ||
            quoteArr.GetArrayLength() == 0 ||
            !quoteArr[0].TryGetProperty("close", out JsonElement closeArr))
        {
            throw new InvalidOperationException($"Yahoo response missing timestamp/close for {yahooSymbol}");
        }

        int n = tsArr.GetArrayLength();
        List<BenchmarkRow> rows = new(n);
        for (int i = 0; i < n; i++)
        {
            JsonElement tsEl = tsArr[i];
            JsonElement closeEl = closeArr[i];

            if (tsEl.ValueKind != JsonValueKind.Number || closeEl.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            long epochSec = tsEl.GetInt64();
            DateTime utc = DateTimeOffset.FromUnixTimeSeconds(epochSec).UtcDateTime;
            DateOnly d = DateOnly.FromDateTime(utc);
            double close = closeEl.GetDouble();
            rows.Add(new BenchmarkRow(d, close));
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"Yahoo returned 0 parseable rows for {yahooSymbol}");
        }

        return rows;
    }

    // ---------------------------------------------------------------------
    // Symbol mapping
    // ---------------------------------------------------------------------
    private static string MapSymbolToStooq(string symbol) => symbol.ToUpperInvariant() switch
    {
        // Stooq uses lowercase tickers and ^prefix for indices (URL-encoded as %5E in template sometimes).
        // Users typically pass "SPX" meaning S&P 500 cash index.
        "SPX" => "^spx",
        "SWDA" => "swda.uk",  // iShares MSCI World UCITS ETF (LSE ticker)
        _ => symbol.ToLowerInvariant()
    };

    private static string MapSymbolToYahoo(string symbol) => symbol.ToUpperInvariant() switch
    {
        "SPX" => "^GSPC",     // Yahoo S&P 500 cash index
        "SWDA" => "SWDA.L",   // London SWDA
        _ => symbol
    };

    // ---------------------------------------------------------------------
    // Outbox emission per fetched row
    // ---------------------------------------------------------------------
    private async Task QueueBenchmarkCloseEventAsync(string symbol, BenchmarkRow row, CancellationToken ct)
    {
        string dateIso = row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        object payload = new
        {
            symbol,
            date = dateIso,
            close = row.Close
        };

        string payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        OutboxEntry entry = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = OutboxEventTypes.BenchmarkClose,
            PayloadJson = payloadJson,
            // Idempotent per (symbol, date) — replay-safe.
            DedupeKey = string.Format(CultureInfo.InvariantCulture, "benchmark_close:{0}:{1}", symbol, dateIso),
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _outboxRepo.InsertAsync(entry, ct);
    }

    // ---------------------------------------------------------------------
    // Telegram alert on dual-source failure
    // ---------------------------------------------------------------------
    private async Task AlertFetchFailureAsync(string symbol, Exception stooqEx, Exception yahooEx, CancellationToken ct)
    {
        TelegramAlert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            Severity = AlertSeverity.Warning,
            Type = AlertType.Configuration,
            Message = string.Format(CultureInfo.InvariantCulture,
                "BenchmarkCollector: {0} fetch failed on BOTH Stooq and Yahoo.", symbol),
            Details = string.Format(CultureInfo.InvariantCulture,
                "Stooq: {0}\nYahoo: {1}",
                stooqEx.Message, yahooEx.Message),
            SourceService = "TradingSupervisorService/BenchmarkCollector",
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _telegramAlerter.QueueAlertAsync(alert);
        }
        catch (Exception alertEx)
        {
            // Alerter failing is non-fatal; we've already logged upstream.
            _logger.LogError(alertEx, "{Worker} failed to queue Telegram alert for {Symbol}",
                nameof(BenchmarkCollector), symbol);
        }
    }

    // ---------------------------------------------------------------------
    // benchmark_fetch_log persistence (SQLite)
    // ---------------------------------------------------------------------
    private async Task<DateOnly?> GetLastFetchedDateAsync(string symbol, CancellationToken ct)
    {
        const string sql = """
            SELECT last_fetched_date
            FROM benchmark_fetch_log
            WHERE symbol = @Symbol
            LIMIT 1
            """;

        try
        {
            await using SqliteConnection conn = await _dbFactory.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { Symbol = symbol }, cancellationToken: ct);
            string? iso = await conn.ExecuteScalarAsync<string?>(cmd);
            if (string.IsNullOrWhiteSpace(iso))
            {
                return null;
            }
            return DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly d)
                ? d
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} failed to read benchmark_fetch_log for {Symbol}; assuming cold start",
                nameof(BenchmarkCollector), symbol);
            return null;
        }
    }

    private async Task UpdateFetchLogSuccessAsync(string symbol, DateOnly? maxDate, string source, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO benchmark_fetch_log (symbol, last_fetched_date, last_success_ts, last_error, last_error_ts, source)
            VALUES (@Symbol, @LastDate, @SuccessTs, NULL, NULL, @Source)
            ON CONFLICT(symbol) DO UPDATE SET
                last_fetched_date = COALESCE(excluded.last_fetched_date, benchmark_fetch_log.last_fetched_date),
                last_success_ts   = excluded.last_success_ts,
                last_error        = NULL,
                last_error_ts     = NULL,
                source            = excluded.source
            """;

        try
        {
            await using SqliteConnection conn = await _dbFactory.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new
            {
                Symbol = symbol,
                LastDate = maxDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                SuccessTs = DateTime.UtcNow.ToString("O"),
                Source = source
            }, cancellationToken: ct);
            await conn.ExecuteAsync(cmd);
        }
        catch (Exception ex)
        {
            // Logging only — DB failure should not crash the worker.
            _logger.LogError(ex, "{Worker} failed to update benchmark_fetch_log success row for {Symbol}",
                nameof(BenchmarkCollector), symbol);
        }
    }

    private async Task UpdateFetchLogFailureAsync(string symbol, string errorMessage, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO benchmark_fetch_log (symbol, last_fetched_date, last_success_ts, last_error, last_error_ts, source)
            VALUES (@Symbol, NULL, NULL, @Err, @ErrTs, NULL)
            ON CONFLICT(symbol) DO UPDATE SET
                last_error    = excluded.last_error,
                last_error_ts = excluded.last_error_ts
            """;

        try
        {
            await using SqliteConnection conn = await _dbFactory.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new
            {
                Symbol = symbol,
                Err = errorMessage,
                ErrTs = DateTime.UtcNow.ToString("O")
            }, cancellationToken: ct);
            await conn.ExecuteAsync(cmd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} failed to update benchmark_fetch_log failure row for {Symbol}",
                nameof(BenchmarkCollector), symbol);
        }
    }

    // ---------------------------------------------------------------------
    // Types
    // ---------------------------------------------------------------------
    private readonly record struct BenchmarkRow(DateOnly Date, double Close);
}
