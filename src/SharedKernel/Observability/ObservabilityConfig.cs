using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Http;

namespace SharedKernel.Observability;

/// <summary>
/// Options for the Serilog HTTP sink used to ship logs from the .NET services to the
/// Cloudflare Worker's /api/v1/logs endpoint.
/// </summary>
public sealed record HttpSinkOptions
{
    /// <summary>Cloudflare Worker base URL (same as OutboxSync).</summary>
    public string WorkerUrl { get; init; } = string.Empty;

    /// <summary>X-Api-Key value; empty disables shipping (sink still registers but drops).</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Logical service name embedded in every log record — stable short string.</summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>Max events per batch — plan spec says 100.</summary>
    public int BatchSizeLimit { get; init; } = 100;

    /// <summary>Flush period regardless of batch fill — plan spec says 5s.</summary>
    public TimeSpan BatchPeriod { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Minimum level for the HTTP sink — plan spec says Warning (Info/Debug stay local).</summary>
    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Warning;

    /// <summary>Max in-memory queue size before the oldest events are dropped. 5MB is generous.</summary>
    public long QueueLimitBytes { get; init; } = 5 * 1024 * 1024;
}

/// <summary>
/// Shared wiring for the Serilog HTTP sink. Both services call <see cref="AddLogShipping"/>
/// from their Serilog configuration pipeline so the behavior is identical.
/// </summary>
public static class ObservabilityConfig
{
    /// <summary>
    /// Reads HTTP-sink options from the standard Cloudflare:* section. Missing keys
    /// yield an options record with empty strings — the caller decides whether to
    /// wire the sink (we skip it when WorkerUrl is blank to avoid noise on local dev).
    /// </summary>
    public static HttpSinkOptions ReadOptions(IConfiguration configuration, string serviceName)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("serviceName required", nameof(serviceName));
        }

        // Shape the config read defensively — all keys optional, WorkerUrl gate controls activation.
        string workerUrl = configuration.GetValue<string>("Cloudflare:WorkerUrl") ?? string.Empty;
        string apiKey = configuration.GetValue<string>("Cloudflare:ApiKey") ?? string.Empty;
        int batchSize = configuration.GetValue<int>("Observability:LogShipping:BatchSize", 100);
        int batchPeriodSec = configuration.GetValue<int>("Observability:LogShipping:BatchPeriodSeconds", 5);
        string minLevelStr = configuration.GetValue<string>("Observability:LogShipping:MinimumLevel") ?? "Warning";

        if (!Enum.TryParse<LogEventLevel>(minLevelStr, ignoreCase: true, out LogEventLevel level))
        {
            level = LogEventLevel.Warning;
        }

        return new HttpSinkOptions
        {
            WorkerUrl = workerUrl,
            ApiKey = apiKey,
            ServiceName = serviceName,
            BatchSizeLimit = batchSize > 0 ? batchSize : 100,
            BatchPeriod = TimeSpan.FromSeconds(batchPeriodSec > 0 ? batchPeriodSec : 5),
            MinimumLevel = level
        };
    }

    /// <summary>
    /// Adds the HTTP log-shipping sink to a Serilog configuration. Safe to call always —
    /// becomes a no-op when <see cref="HttpSinkOptions.WorkerUrl"/> is blank.
    /// </summary>
    /// <param name="loggerConfig">The Serilog LoggerConfiguration being built.</param>
    /// <param name="options">Resolved options from <see cref="ReadOptions"/>.</param>
    public static LoggerConfiguration AddLogShipping(this LoggerConfiguration loggerConfig, HttpSinkOptions options)
    {
        if (loggerConfig == null)
        {
            throw new ArgumentNullException(nameof(loggerConfig));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Skip wiring if the Worker URL isn't configured — keeps the service usable
        // on dev machines without a running Worker.
        if (string.IsNullOrWhiteSpace(options.WorkerUrl))
        {
            return loggerConfig;
        }

        // Build the full endpoint URL; the Worker expects POST /api/v1/logs.
        string endpoint = $"{options.WorkerUrl.TrimEnd('/')}/api/v1/logs";

        // Custom batch formatter → Worker's { "batch": [...] } shape.
        LogShippingBatchFormatter batchFormatter = new(options.ServiceName);

        // Custom HTTP client → injects X-Api-Key.
        IHttpClient httpClient = new ApiKeyAuthHttpClient(options.ApiKey);

        // NormalRenderedTextFormatter is the default text formatter — we rely on its
        // JSON shape (Timestamp/Level/RenderedMessage/Exception/Properties) inside
        // LogShippingBatchFormatter.ReshapeEvent. Do NOT override it here.
        return loggerConfig.WriteTo.Http(
            requestUri: endpoint,
            queueLimitBytes: options.QueueLimitBytes,
            logEventsInBatchLimit: options.BatchSizeLimit,
            period: options.BatchPeriod,
            batchFormatter: batchFormatter,
            httpClient: httpClient,
            restrictedToMinimumLevel: options.MinimumLevel);
    }
}
