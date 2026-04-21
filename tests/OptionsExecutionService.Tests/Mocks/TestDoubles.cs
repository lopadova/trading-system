using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedKernel.Configuration;
using SharedKernel.Domain;
using SharedKernel.Observability;
using SharedKernel.Safety;

namespace OptionsExecutionService.Tests.Mocks;

/// <summary>
/// Minimal <see cref="IAlerter"/> that records invocations in memory so tests
/// can assert the gate fired a Critical alert without wiring Telegram/SMTP.
/// </summary>
public sealed class RecordingAlerter : IAlerter
{
    public List<(AlertSeverity Severity, string Title, string Message)> Sent { get; } = new();

    public Task SendImmediateAsync(AlertSeverity severity, string title, string message, CancellationToken ct)
    {
        lock (Sent)
        {
            Sent.Add((severity, title, message));
        }
        return Task.CompletedTask;
    }

    public Task SendDigestAsync(AlertSeverity severity, IReadOnlyList<(string title, string message)> entries, CancellationToken ct)
    {
        // Digest path not exercised in OrderPlacer tests — no-op.
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory <see cref="ISafetyFlagStore"/>. Matches the real contract: <see cref="GetAsync"/>
/// never throws, <see cref="SetAsync"/> throws only if configured to.
/// </summary>
public sealed class InMemorySafetyFlagStore : ISafetyFlagStore
{
    private readonly ConcurrentDictionary<string, string> _flags = new();
    public bool ThrowOnSet { get; set; }

    public Task<string?> GetAsync(string key, CancellationToken ct)
    {
        _flags.TryGetValue(key, out string? value);
        return Task.FromResult<string?>(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct)
    {
        if (ThrowOnSet)
        {
            throw new InvalidOperationException("Test-configured SetAsync failure");
        }
        _flags[key] = value;
        return Task.CompletedTask;
    }

    public Task<bool> IsSetAsync(string key, CancellationToken ct)
    {
        _flags.TryGetValue(key, out string? value);
        return Task.FromResult(value == "1");
    }
}

/// <summary>
/// In-memory <see cref="IOrderAuditSink"/> — records every entry for later assertion.
/// </summary>
public sealed class RecordingAuditSink : IOrderAuditSink
{
    public List<OrderAuditEntry> Entries { get; } = new();

    public Task WriteAsync(OrderAuditEntry entry, CancellationToken ct)
    {
        lock (Entries)
        {
            Entries.Add(entry);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// <see cref="HttpMessageHandler"/> that canned-responds to every request with
/// the configured payload. Used by <c>SemaphoreGate</c> tests to simulate the
/// Worker without spinning a real HTTP server.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }
    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (Responder is null)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        }
        return Task.FromResult(Responder(request));
    }
}

/// <summary>
/// <see cref="IHttpClientFactory"/> that always hands out a client wrapping a given handler.
/// </summary>
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public StubHttpClientFactory(HttpMessageHandler handler) { _handler = handler; }
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

/// <summary>
/// Helper utilities to construct the SemaphoreGate DI triad in tests.
/// </summary>
public static class GateTestHelpers
{
    public static OptionsExecutionService.Services.SemaphoreGate BuildSemaphoreGate(
        HttpMessageHandler handler,
        CloudflareOptions? options = null,
        IAlerter? alerter = null,
        TimeProvider? timeProvider = null)
    {
        IOptions<CloudflareOptions> opts = Options.Create(options ?? new CloudflareOptions
        {
            WorkerUrl = "https://test.workers.dev",
            ApiKey = "test-api-key"
        });
        IAlerter al = alerter ?? new RecordingAlerter();
        return new OptionsExecutionService.Services.SemaphoreGate(
            new StubHttpClientFactory(handler),
            opts,
            NullLogger<OptionsExecutionService.Services.SemaphoreGate>.Instance,
            al,
            timeProvider);
    }

    /// <summary>Short-circuit SemaphoreGate that always returns a fixed status.</summary>
    public static OptionsExecutionService.Services.SemaphoreGate FixedGate(SemaphoreStatus status)
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"status\":\"{status.ToWire()}\"}}")
            }
        };
        return BuildSemaphoreGate(handler);
    }
}
