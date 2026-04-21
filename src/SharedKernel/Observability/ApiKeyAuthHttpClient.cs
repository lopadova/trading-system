using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace SharedKernel.Observability;

/// <summary>
/// Serilog.Sinks.Http <see cref="IHttpClient"/> that adds an "X-Api-Key" header to every
/// outgoing request (matching the existing OutboxSyncWorker auth scheme).
/// <para>
/// Timeouts are tight (10s) — log shipping must never block the host service. If the Worker
/// is down, the sink buffers in-memory (up to queueLimitBytes) and drops the oldest on overflow.
/// </para>
/// </summary>
public sealed class ApiKeyAuthHttpClient : IHttpClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private bool _disposed;

    /// <summary>
    /// Constructs the client with the key already bound. The Configure() hook from Serilog
    /// is a no-op in our setup (we don't read from IConfiguration at shipment time).
    /// </summary>
    public ApiKeyAuthHttpClient(string apiKey)
    {
        _apiKey = apiKey ?? string.Empty;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _http.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        }
    }

    /// <summary>
    /// Serilog.Sinks.Http calls this once after construction, passing the Serilog.Settings.Configuration
    /// section. We already took the key via constructor, so this is a no-op.
    /// </summary>
    public void Configure(IConfiguration configuration)
    {
        // Intentionally empty — configuration was bound at construction time.
    }

    /// <summary>
    /// Sends the pre-serialized batch stream to the Worker. The sink handles retry/backoff
    /// when a non-2xx is returned; we just forward the response.
    /// </summary>
    public async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ApiKeyAuthHttpClient));
        }

        using StreamContent content = new(contentStream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        // Serilog.Sinks.Http expects us NOT to dispose the response — the caller does.
        return await _http.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _http.Dispose();
        _disposed = true;
    }
}
