using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using SharedKernel.Observability;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests.Workers;

/// <summary>
/// Unit tests for <see cref="SafetyAlertsWorker"/>. We drive private <c>CheckIbkrConnectionAsync</c>
/// behavior indirectly by letting ExecuteAsync run for a bounded cancellation window with very
/// short intervals, then asserting on the mocked <see cref="IAlerter"/> invocations.
/// </summary>
public sealed class SafetyAlertsWorkerTests
{
    private static IConfiguration BuildConfig(
        bool enabled = true,
        int intervalSec = 1,
        int ibkrThresholdSec = 1,
        double marginThreshold = 75.0)
    {
        Dictionary<string, string?> d = new()
        {
            ["SafetyAlerts:Enabled"] = enabled ? "true" : "false",
            ["SafetyAlerts:IntervalSeconds"] = intervalSec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SafetyAlerts:IbkrDisconnectThresholdSeconds"] = ibkrThresholdSec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SafetyAlerts:MarginThresholdPercent"] = marginThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return new ConfigurationBuilder().AddInMemoryCollection(d).Build();
    }

    [Fact]
    public async Task ExecuteAsync_IbkrDisconnectedOverThreshold_FiresCriticalAlert()
    {
        // IBKR "IsConnected = false" from the first call. With threshold=1s, interval=1s,
        // the second cycle will see elapsed >= 1s and raise Critical.
        Mock<IIbkrClient> ibkr = new();
        ibkr.Setup(x => x.IsConnected).Returns(false);

        Mock<IAlerter> alerter = new();
        alerter.Setup(x => x.SendImmediateAsync(It.IsAny<AlertSeverity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        SafetyAlertsWorker sut = new(
            NullLogger<SafetyAlertsWorker>.Instance,
            new[] { alerter.Object },
            ibkr.Object,
            BuildConfig(intervalSec: 1, ibkrThresholdSec: 1));

        // Run for a bounded window — long enough for ≥3 cycles.
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(4));
        await sut.StartAsync(cts.Token);
        await Task.Delay(3500, CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        // Expect exactly ONE critical alert (deduplicated across cycles within the outage window).
        alerter.Verify(x => x.SendImmediateAsync(
            AlertSeverity.Critical,
            It.Is<string>(s => s.Contains("IBKR disconnected")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_IbkrConnected_NoAlertsFired()
    {
        Mock<IIbkrClient> ibkr = new();
        ibkr.Setup(x => x.IsConnected).Returns(true);

        Mock<IAlerter> alerter = new();

        SafetyAlertsWorker sut = new(
            NullLogger<SafetyAlertsWorker>.Instance,
            new[] { alerter.Object },
            ibkr.Object,
            BuildConfig(intervalSec: 1, ibkrThresholdSec: 1));

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
        await sut.StartAsync(cts.Token);
        await Task.Delay(2500, CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        alerter.Verify(x => x.SendImmediateAsync(It.IsAny<AlertSeverity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_AlerterThrows_OtherAlertersStillCalled()
    {
        // One alerter throws; another must still be invoked (fan-out isolation).
        Mock<IIbkrClient> ibkr = new();
        ibkr.Setup(x => x.IsConnected).Returns(false);

        Mock<IAlerter> bad = new();
        bad.Setup(x => x.SendImmediateAsync(It.IsAny<AlertSeverity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("telegram rate-limited"));

        Mock<IAlerter> good = new();
        good.Setup(x => x.SendImmediateAsync(It.IsAny<AlertSeverity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SafetyAlertsWorker sut = new(
            NullLogger<SafetyAlertsWorker>.Instance,
            new[] { bad.Object, good.Object },
            ibkr.Object,
            BuildConfig(intervalSec: 1, ibkrThresholdSec: 1));

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(4));
        await sut.StartAsync(cts.Token);
        await Task.Delay(3000, CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        // Both were attempted, despite bad.Object throwing.
        bad.Verify(x => x.SendImmediateAsync(AlertSeverity.Critical, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        good.Verify(x => x.SendImmediateAsync(AlertSeverity.Critical, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_FiresNoAlerts()
    {
        Mock<IIbkrClient> ibkr = new();
        ibkr.Setup(x => x.IsConnected).Returns(false);
        Mock<IAlerter> alerter = new();

        SafetyAlertsWorker sut = new(
            NullLogger<SafetyAlertsWorker>.Instance,
            new[] { alerter.Object },
            ibkr.Object,
            BuildConfig(enabled: false));

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
        await sut.StartAsync(cts.Token);
        await Task.Delay(1500, CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        alerter.Verify(x => x.SendImmediateAsync(It.IsAny<AlertSeverity>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
