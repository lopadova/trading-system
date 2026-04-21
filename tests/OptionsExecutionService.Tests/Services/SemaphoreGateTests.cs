using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OptionsExecutionService.Services;
using OptionsExecutionService.Tests.Mocks;
using SharedKernel.Configuration;
using SharedKernel.Domain;
using SharedKernel.Safety;
using Xunit;

namespace OptionsExecutionService.Tests.Services;

/// <summary>
/// Tests for <see cref="SemaphoreGate"/>. Focus areas:
/// <list type="bullet">
///   <item><description>Happy-path parsing of {green|orange|red}.</description></item>
///   <item><description>Fail-cautious → Orange on network/HTTP error / unparseable payload.</description></item>
///   <item><description>Fail-closed → Red + Critical alert on 401/403.</description></item>
///   <item><description>Cache TTL behavior (60s).</description></item>
///   <item><description>IsRed timeout returns true (fail-closed).</description></item>
/// </list>
/// </summary>
public sealed class SemaphoreGateTests
{
    private static HttpResponseMessage OkJson(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        };
    }

    [Fact]
    public async Task GetCurrentStatusAsync_GreenBody_ReturnsGreen()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => OkJson("{\"status\":\"green\",\"score\":15}")
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Green, status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_RedBody_ReturnsRed()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => OkJson("{\"status\":\"red\",\"score\":95}")
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Red, status);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_OrangeBody_ReturnsOrange()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => OkJson("{\"status\":\"orange\"}")
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Orange, status);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_UnparseableBody_DefaultsOrange()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => OkJson("{\"nope\":123}")
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Orange, status);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_500Response_DefaultsOrange()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Orange, status);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_401Response_DefaultsRedAndAlerts()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        };
        RecordingAlerter alerter = new();
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler, alerter: alerter);

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Red, status);
        Assert.Single(alerter.Sent);
        Assert.Equal(AlertSeverity.Critical, alerter.Sent[0].Severity);
        Assert.Contains("auth", alerter.Sent[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_403Response_DefaultsRed()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Red, status);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_NetworkException_DefaultsOrange()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => throw new HttpRequestException("connection refused")
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Orange, status);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_BlankWorkerUrl_DefaultsOrangeNoCall()
    {
        StubHttpMessageHandler handler = new();
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(
            handler,
            new CloudflareOptions { WorkerUrl = "", ApiKey = "" });

        SemaphoreStatus status = await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(SemaphoreStatus.Orange, status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_CacheHitWithinTtl_NoSecondCall()
    {
        int callCount = 0;
        StubHttpMessageHandler handler = new()
        {
            Responder = _ =>
            {
                callCount++;
                return OkJson("{\"status\":\"green\"}");
            }
        };
        FakeTimeProvider time = new();
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler, timeProvider: time);

        // First call hits HTTP.
        await gate.GetCurrentStatusAsync(CancellationToken.None);
        // Second call within TTL hits the cache.
        await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_CacheExpires_RefetchesAfterTtl()
    {
        int callCount = 0;
        StubHttpMessageHandler handler = new()
        {
            Responder = _ =>
            {
                callCount++;
                return OkJson("{\"status\":\"green\"}");
            }
        };
        FakeTimeProvider time = new();
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler, timeProvider: time);

        await gate.GetCurrentStatusAsync(CancellationToken.None);
        // Advance past the 60s TTL.
        time.Advance(TimeSpan.FromSeconds(61));
        await gate.GetCurrentStatusAsync(CancellationToken.None);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void IsRed_CaseRed_ReturnsTrue()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => OkJson("{\"status\":\"red\"}")
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        Assert.True(gate.IsRed());
    }

    [Fact]
    public void IsRed_CaseGreen_ReturnsFalse()
    {
        StubHttpMessageHandler handler = new()
        {
            Responder = _ => OkJson("{\"status\":\"green\"}")
        };
        SemaphoreGate gate = GateTestHelpers.BuildSemaphoreGate(handler);

        Assert.False(gate.IsRed());
    }
}

/// <summary>
/// Minimal fake TimeProvider that advances on demand. TimeProvider is sealed
/// and its mock variant ships with .NET 9+. Implementing just GetUtcNow since
/// that's the only thing the gate touches.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
