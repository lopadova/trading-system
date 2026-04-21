using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Configuration;
using SharedKernel.Data;
using SharedKernel.Safety;

namespace OptionsExecutionService.Services;

/// <summary>
/// Persists <see cref="OrderAuditEntry"/> rows using a local-first, ship-second
/// outbox pattern:
/// <list type="number">
///   <item><description>Write to the local SQLite mirror (<c>order_audit_log_local</c>, migration 004)
///   with <c>shipped=0</c>. This is the authoritative record; if the Worker is
///   unreachable we still have a complete local history for post-incident reconciliation.</description></item>
///   <item><description>Attempt immediate ship to the Cloudflare Worker (<c>POST /api/v1/ingest</c>
///   with <c>event_type=order_audit</c>). On success, flip <c>shipped=1</c>.
///   On failure, swallow + log; the row stays <c>shipped=0</c> and a future
///   audit-reconciliation worker can retry.</description></item>
/// </list>
/// <para>
/// <b>Idempotency</b>: the PK is <c>audit_id</c> (a GUID produced upstream), and
/// the Worker's side mirrors the same PK in <c>order_audit_log</c>. Replaying
/// a row is a no-op on both ends.
/// </para>
/// <para>
/// <b>Never throws</b>: contract from <see cref="IOrderAuditSink"/>. Audit writes
/// live inside the order-placement hot path; an audit blowing up must not
/// defeat the very safety gate that produced the row. All errors are logged
/// and swallowed. The safety guarantee is: "if we returned, either the local
/// write succeeded or we logged a CRITICAL on why it didn't".
/// </para>
/// </summary>
public sealed class OutboxOrderAuditSink : IOrderAuditSink
{
    private readonly IDbConnectionFactory _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CloudflareOptions _cloudflare;
    private readonly ILogger<OutboxOrderAuditSink> _logger;

    public OutboxOrderAuditSink(
        IDbConnectionFactory db,
        IHttpClientFactory httpClientFactory,
        IOptions<CloudflareOptions> cloudflare,
        ILogger<OutboxOrderAuditSink> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _cloudflare = cloudflare?.Value ?? throw new ArgumentNullException(nameof(cloudflare));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WriteAsync(OrderAuditEntry entry, CancellationToken ct)
    {
        if (entry is null)
        {
            _logger.LogError("OutboxOrderAuditSink.WriteAsync called with null entry — ignored");
            return;
        }
        if (string.IsNullOrWhiteSpace(entry.AuditId))
        {
            _logger.LogError("OutboxOrderAuditSink.WriteAsync called with blank AuditId — ignored. Entry={@Entry}", entry);
            return;
        }

        // Step 1: local-first write. If this fails, we've lost the audit row;
        // log CRITICAL so the operator knows the paper trail is broken.
        bool persistedLocally = await PersistLocallyAsync(entry, ct).ConfigureAwait(false);

        // Step 2: best-effort ship to Worker. If the local write failed, we still
        // try to ship (better than losing it entirely) but we won't flip shipped=1
        // on success since no local row exists to update.
        bool shipped = await ShipToWorkerAsync(entry, ct).ConfigureAwait(false);

        // Step 3: flip the shipped flag on the local row if both succeeded.
        if (persistedLocally && shipped)
        {
            await MarkShippedAsync(entry.AuditId, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// INSERT the audit row with shipped=0. Uses INSERT OR IGNORE on the audit_id
    /// PK so duplicate writes of the same row (e.g. retry of a partially-shipped
    /// audit) don't blow up.
    /// </summary>
    private async Task<bool> PersistLocallyAsync(OrderAuditEntry entry, CancellationToken ct)
    {
        const string sql = """
            INSERT OR IGNORE INTO order_audit_log_local
                (audit_id, order_id, ts, actor, strategy_id, contract_symbol,
                 side, quantity, price, semaphore_status, outcome, override_reason,
                 details_json, shipped)
            VALUES
                (@AuditId, @OrderId, @Ts, @Actor, @StrategyId, @ContractSymbol,
                 @Side, @Quantity, @Price, @SemaphoreStatus, @Outcome, @OverrideReason,
                 @DetailsJson, 0);
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct).ConfigureAwait(false);
            CommandDefinition cmd = new(sql, new
            {
                entry.AuditId,
                entry.OrderId,
                entry.Ts,
                entry.Actor,
                entry.StrategyId,
                entry.ContractSymbol,
                entry.Side,
                entry.Quantity,
                entry.Price,
                entry.SemaphoreStatus,
                entry.Outcome,
                entry.OverrideReason,
                entry.DetailsJson
            }, cancellationToken: ct);
            await conn.ExecuteAsync(cmd).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "OutboxOrderAuditSink: LOCAL PERSIST FAILED — audit trail compromised! AuditId={AuditId}",
                entry.AuditId);
            return false;
        }
    }

    /// <summary>
    /// UPDATE shipped=1 for a successfully-shipped row.
    /// </summary>
    private async Task MarkShippedAsync(string auditId, CancellationToken ct)
    {
        const string sql = "UPDATE order_audit_log_local SET shipped = 1 WHERE audit_id = @AuditId";

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct).ConfigureAwait(false);
            CommandDefinition cmd = new(sql, new { AuditId = auditId }, cancellationToken: ct);
            await conn.ExecuteAsync(cmd).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Not critical — the row is on both ends, just our local bookkeeping
            // got out of sync. Reconciliation worker will fix it later.
            _logger.LogWarning(ex, "Failed to mark audit {AuditId} as shipped locally", auditId);
        }
    }

    /// <summary>
    /// POST the audit row to the Worker ingest endpoint. Returns false on any
    /// non-2xx or network failure — caller treats that as "stays on local,
    /// retry later".
    /// </summary>
    private async Task<bool> ShipToWorkerAsync(OrderAuditEntry entry, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_cloudflare.WorkerUrl))
        {
            // Dev mode: Worker not configured. Silently skip shipping — local row is the record.
            return false;
        }

        string endpoint = $"{_cloudflare.WorkerUrl.TrimEnd('/')}/api/v1/ingest";

        IngestEnvelope envelope = new()
        {
            EventId = entry.AuditId,
            EventType = "order_audit",
            Payload = new IngestPayload
            {
                AuditId = entry.AuditId,
                OrderId = entry.OrderId,
                Ts = entry.Ts,
                Actor = entry.Actor,
                StrategyId = entry.StrategyId,
                ContractSymbol = entry.ContractSymbol,
                Side = entry.Side,
                Quantity = entry.Quantity,
                Price = entry.Price,
                SemaphoreStatus = entry.SemaphoreStatus,
                Outcome = entry.Outcome,
                OverrideReason = entry.OverrideReason,
                DetailsJson = entry.DetailsJson
            }
        };

        try
        {
            HttpClient http = _httpClientFactory.CreateClient(nameof(OutboxOrderAuditSink));
            http.Timeout = TimeSpan.FromSeconds(5);

            using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
            if (!string.IsNullOrEmpty(_cloudflare.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _cloudflare.ApiKey);
            }
            request.Content = JsonContent.Create(envelope, options: s_jsonOptions);

            using HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OutboxOrderAuditSink: Worker returned {StatusCode} for audit {AuditId} — will retry later",
                    (int)response.StatusCode, entry.AuditId);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OutboxOrderAuditSink: ship failed for audit {AuditId} — will retry later", entry.AuditId);
            return false;
        }
    }

    // ----------------------------------------------------------------------
    // Payload DTOs — snake_case for the Worker contract, PascalCase in C#.
    // ----------------------------------------------------------------------
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private sealed class IngestEnvelope
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; } = string.Empty;

        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public IngestPayload Payload { get; set; } = new();
    }

    private sealed class IngestPayload
    {
        [JsonPropertyName("audit_id")]
        public string AuditId { get; set; } = string.Empty;

        [JsonPropertyName("order_id")]
        public string? OrderId { get; set; }

        [JsonPropertyName("ts")]
        public string Ts { get; set; } = string.Empty;

        [JsonPropertyName("actor")]
        public string Actor { get; set; } = "system";

        [JsonPropertyName("strategy_id")]
        public string? StrategyId { get; set; }

        [JsonPropertyName("contract_symbol")]
        public string ContractSymbol { get; set; } = string.Empty;

        [JsonPropertyName("side")]
        public string Side { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("semaphore_status")]
        public string SemaphoreStatus { get; set; } = "unknown";

        [JsonPropertyName("outcome")]
        public string Outcome { get; set; } = string.Empty;

        [JsonPropertyName("override_reason")]
        public string? OverrideReason { get; set; }

        [JsonPropertyName("details_json")]
        public string? DetailsJson { get; set; }
    }
}
