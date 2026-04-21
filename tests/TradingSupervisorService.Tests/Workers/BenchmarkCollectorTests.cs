using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using SharedKernel.Data;
using SharedKernel.Tests.Data;
using TradingSupervisorService.Migrations;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Services;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests.Workers;

/// <summary>
/// Unit tests for BenchmarkCollector (Phase 7.1).
/// Uses:
///   * InMemoryConnectionFactory for supervisor.db (benchmark_fetch_log)
///   * a Moq-based HttpMessageHandler to return canned Stooq CSV / Yahoo JSON
///   * a real OutboxRepository to inspect queued events via SQL
/// </summary>
public sealed class BenchmarkCollectorTests : IAsyncLifetime
{
    private InMemoryConnectionFactory _dbFactory = default!;
    private OutboxRepository _outbox = default!;
    private Mock<ITelegramAlerter> _mockAlerter = default!;

    public async Task InitializeAsync()
    {
        _dbFactory = new InMemoryConnectionFactory();
        MigrationRunner runner = new(_dbFactory, NullLogger<MigrationRunner>.Instance);
        await runner.RunAsync(SupervisorMigrations.All, CancellationToken.None);

        _outbox = new OutboxRepository(_dbFactory, NullLogger<OutboxRepository>.Instance);
        _mockAlerter = new Mock<ITelegramAlerter>();
    }

    public async Task DisposeAsync()
    {
        await _dbFactory.DisposeAsync();
    }

    [Fact]
    public void Constructor_WithValidConfig_Succeeds()
    {
        BenchmarkCollector worker = BuildWorker(BuildHttpClientFactory(new Mock<HttpMessageHandler>()));
        Assert.NotNull(worker);
    }

    [Fact]
    public void Constructor_InvalidRunTime_Throws()
    {
        IConfiguration config = BuildConfig("99:99");
        IHttpClientFactory factory = BuildHttpClientFactory(new Mock<HttpMessageHandler>());

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new BenchmarkCollector(
            NullLogger<BenchmarkCollector>.Instance,
            factory,
            _outbox,
            _dbFactory,
            _mockAlerter.Object,
            config));

        Assert.Contains("DailyRunTimeUtc", ex.Message);
    }

    [Fact]
    public async Task RunFetchCycle_StooqSuccess_QueuesBenchmarkCloseEvents()
    {
        // Canned Stooq CSV for both SPX and SWDA (two rows each)
        string spxCsv = "Date,Open,High,Low,Close,Volume\n" +
                        "2026-04-17,4000.00,4100.00,3950.00,4055.12,1000000\n" +
                        "2026-04-18,4050.00,4120.00,4040.00,4090.55,1500000\n";
        string swdaCsv = "Date,Open,High,Low,Close,Volume\n" +
                         "2026-04-17,82.50,83.10,82.40,82.95,250000\n" +
                         "2026-04-18,83.00,83.50,82.90,83.33,300000\n";

        Mock<HttpMessageHandler> handler = BuildHandler(routeMap: url =>
        {
            if (url.Contains("%5Espx", StringComparison.OrdinalIgnoreCase) || url.Contains("^spx", StringComparison.OrdinalIgnoreCase))
            {
                return CsvResponse(spxCsv);
            }
            if (url.Contains("swda.uk", StringComparison.OrdinalIgnoreCase))
            {
                return CsvResponse(swdaCsv);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        IHttpClientFactory factory = BuildHttpClientFactory(handler);
        BenchmarkCollector worker = BuildWorker(factory);

        // Drive the private fetch path directly via reflection (simpler + deterministic
        // than time-warping the daily scheduler for a unit test).
        await InvokeRunFetchCycleAsync(worker);

        // Assert: 4 benchmark_close events queued (2 symbols × 2 rows)
        int count = await CountOutboxAsync(OutboxEventTypes.BenchmarkClose);
        Assert.Equal(4, count);

        // Spot-check payload content: expect the exact close values to appear
        IReadOnlyList<string> payloads = await ReadOutboxPayloadsAsync(OutboxEventTypes.BenchmarkClose);
        Assert.Contains(payloads, p => p.Contains("4055.12"));
        Assert.Contains(payloads, p => p.Contains("83.33"));
    }

    [Fact]
    public async Task RunFetchCycle_StooqFails_YahooFallbackSucceeds()
    {
        // Yahoo JSON fallback response: one data point
        string yahooJson = """
            {
              "chart": {
                "result": [{
                  "timestamp": [1713398400],
                  "indicators": { "quote": [{ "close": [4200.77] }] }
                }]
              }
            }
            """;

        Mock<HttpMessageHandler> handler = BuildHandler(routeMap: url =>
        {
            if (url.Contains("stooq", StringComparison.OrdinalIgnoreCase))
            {
                // Stooq failure: empty body
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("", Encoding.UTF8, "text/plain")
                };
            }
            if (url.Contains("yahoo", StringComparison.OrdinalIgnoreCase) || url.Contains("query1.finance", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(yahooJson, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        IHttpClientFactory factory = BuildHttpClientFactory(handler);

        // Restrict to one symbol for simpler assertion
        IConfiguration config = BuildConfig("22:30", new[] { "SPX" });
        BenchmarkCollector worker = new(
            NullLogger<BenchmarkCollector>.Instance,
            factory,
            _outbox,
            _dbFactory,
            _mockAlerter.Object,
            config);

        await InvokeRunFetchCycleAsync(worker);

        int count = await CountOutboxAsync(OutboxEventTypes.BenchmarkClose);
        Assert.Equal(1, count);

        IReadOnlyList<string> payloads = await ReadOutboxPayloadsAsync(OutboxEventTypes.BenchmarkClose);
        Assert.Contains(payloads, p => p.Contains("4200.77"));
    }

    [Fact]
    public async Task RunFetchCycle_BothSourcesFail_QueuesTelegramAlert()
    {
        Mock<HttpMessageHandler> handler = BuildHandler(routeMap: _ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        IHttpClientFactory factory = BuildHttpClientFactory(handler);
        IConfiguration config = BuildConfig("22:30", new[] { "SPX" });
        BenchmarkCollector worker = new(
            NullLogger<BenchmarkCollector>.Instance,
            factory,
            _outbox,
            _dbFactory,
            _mockAlerter.Object,
            config);

        await InvokeRunFetchCycleAsync(worker);

        int count = await CountOutboxAsync(OutboxEventTypes.BenchmarkClose);
        Assert.Equal(0, count);

        _mockAlerter.Verify(
            a => a.QueueAlertAsync(It.IsAny<SharedKernel.Domain.TelegramAlert>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunFetchCycle_TwoRuns_SecondRunSkipsAlreadySeenRows()
    {
        string csvFirst = "Date,Open,High,Low,Close,Volume\n" +
                          "2026-04-17,4000.00,4100.00,3950.00,4055.12,1000000\n";
        string csvSecond = "Date,Open,High,Low,Close,Volume\n" +
                           "2026-04-17,4000.00,4100.00,3950.00,4055.12,1000000\n" +
                           "2026-04-18,4050.00,4120.00,4040.00,4090.55,1500000\n";

        int call = 0;
        Mock<HttpMessageHandler> handler = BuildHandler(routeMap: _ =>
        {
            call++;
            return CsvResponse(call == 1 ? csvFirst : csvSecond);
        });

        IHttpClientFactory factory = BuildHttpClientFactory(handler);
        IConfiguration config = BuildConfig("22:30", new[] { "SPX" });
        BenchmarkCollector worker = new(
            NullLogger<BenchmarkCollector>.Instance,
            factory,
            _outbox,
            _dbFactory,
            _mockAlerter.Object,
            config);

        await InvokeRunFetchCycleAsync(worker);
        await InvokeRunFetchCycleAsync(worker);

        // First run queues 1 row, second run queues only the new row → 2 total
        int count = await CountOutboxAsync(OutboxEventTypes.BenchmarkClose);
        Assert.Equal(2, count);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private BenchmarkCollector BuildWorker(IHttpClientFactory httpClientFactory, string[]? symbols = null)
    {
        IConfiguration config = BuildConfig("22:30", symbols);
        return new BenchmarkCollector(
            NullLogger<BenchmarkCollector>.Instance,
            httpClientFactory,
            _outbox,
            _dbFactory,
            _mockAlerter.Object,
            config);
    }

    private static IConfiguration BuildConfig(string dailyRunTime, string[]? symbols = null)
    {
        Dictionary<string, string?> dict = new()
        {
            ["BenchmarkCollector:Enabled"] = "true",
            ["BenchmarkCollector:DailyRunTimeUtc"] = dailyRunTime,
            ["BenchmarkCollector:CheckIntervalMinutes"] = "30",
            ["BenchmarkCollector:HttpTimeoutSeconds"] = "5",
            ["BenchmarkCollector:MaxBackfillRows"] = "30",
            ["BenchmarkCollector:StooqUrlTemplate"] = "https://stooq.com/q/d/l/?s={0}&i=d",
            ["BenchmarkCollector:YahooFallbackUrlTemplate"] = "https://query1.finance.yahoo.com/v8/finance/chart/{0}?range=5d&interval=1d"
        };

        symbols ??= new[] { "SPX", "SWDA" };
        for (int i = 0; i < symbols.Length; i++)
        {
            dict[$"BenchmarkCollector:Symbols:{i}"] = symbols[i];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static Mock<HttpMessageHandler> BuildHandler(Func<string, HttpResponseMessage> routeMap)
    {
        Mock<HttpMessageHandler> handler = new(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
                Task.FromResult(routeMap(req.RequestUri!.ToString())));
        return handler;
    }

    private static IHttpClientFactory BuildHttpClientFactory(Mock<HttpMessageHandler> handler)
    {
        Mock<IHttpClientFactory> factory = new();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler.Object, disposeHandler: false));
        return factory.Object;
    }

    private static HttpResponseMessage CsvResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/csv")
    };

    private static async Task InvokeRunFetchCycleAsync(BenchmarkCollector worker)
    {
        // Call the private RunFetchCycleAsync method via reflection. Keeps tests
        // deterministic (no wall-clock scheduling involved).
        System.Reflection.MethodInfo? method = typeof(BenchmarkCollector)
            .GetMethod("RunFetchCycleAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        Task t = (Task)method!.Invoke(worker, new object[] { CancellationToken.None })!;
        await t;
    }

    private async Task<int> CountOutboxAsync(string eventType)
    {
        await using Microsoft.Data.Sqlite.SqliteConnection conn = await _dbFactory.OpenAsync(CancellationToken.None);
        Dapper.CommandDefinition cmd = new(
            "SELECT COUNT(*) FROM sync_outbox WHERE event_type = @t",
            new { t = eventType });
        return await Dapper.SqlMapper.ExecuteScalarAsync<int>(conn, cmd);
    }

    private async Task<IReadOnlyList<string>> ReadOutboxPayloadsAsync(string eventType)
    {
        await using Microsoft.Data.Sqlite.SqliteConnection conn = await _dbFactory.OpenAsync(CancellationToken.None);
        Dapper.CommandDefinition cmd = new(
            "SELECT payload_json FROM sync_outbox WHERE event_type = @t",
            new { t = eventType });
        IEnumerable<string> rows = await Dapper.SqlMapper.QueryAsync<string>(conn, cmd);
        return rows.ToList();
    }

    // Reference CultureInfo to avoid unused-using warnings in certain build configs
    private static void _reserved() => _ = CultureInfo.InvariantCulture;
}
