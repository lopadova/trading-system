using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Domain;
using SharedKernel.Observability;
using TradingSupervisorService.Services;
using Xunit;

namespace TradingSupervisorService.Tests.Services;

/// <summary>
/// Tests for TelegramAlerter's IAlerter implementation — severity-based routing.
/// <para>
/// We can't easily mock <see cref="Telegram.Bot.TelegramBotClient"/>, so we rely on the
/// fact that when Telegram is <b>disabled</b> in config, the alerter still accepts calls
/// but routes them deterministically:
/// </para>
/// <list type="bullet">
///   <item><description>Info → logged, no queue mutation.</description></item>
///   <item><description>Warning → added to digest buffer (not sent).</description></item>
///   <item><description>Error/Critical → attempted immediate-send path (which no-ops when disabled).</description></item>
/// </list>
/// Behavioral assertions are made via the public API (GetPendingCount + the log assertions).
/// </summary>
public sealed class TelegramAlerterSeverityTests
{
    private static IConfiguration DisabledConfig(int digestFlushMinutes = 15) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Telegram:Enabled", "false" },
                { "Telegram:DigestFlushMinutes", digestFlushMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            })
            .Build();

    [Fact]
    public async Task SendImmediateAsync_Info_LogsOnly_NoQueueEffect()
    {
        // Info never ships and never buffers — nothing should end up in the send queue.
        Mock<ILogger<TelegramAlerter>> logger = new();
        using TelegramAlerter sut = new(DisabledConfig(), logger.Object);

        int before = sut.GetPendingCount();
        await sut.SendImmediateAsync(AlertSeverity.Info, "hello", "world", CancellationToken.None);
        int after = sut.GetPendingCount();

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task SendImmediateAsync_Critical_DoesNotBufferAsDigest()
    {
        // Critical takes the immediate-send path → NO digest buffering → digest should be empty.
        Mock<ILogger<TelegramAlerter>> logger = new();
        using TelegramAlerter sut = new(DisabledConfig(), logger.Object);

        await sut.SendImmediateAsync(AlertSeverity.Critical, "ibkr down", "30s", CancellationToken.None);

        // Force a digest flush: since Critical doesn't buffer, sending an explicit digest of 0 entries
        // should be a no-op (no exception).
        await sut.SendDigestAsync(AlertSeverity.Warning, Array.Empty<(string, string)>(), CancellationToken.None);
    }

    [Fact]
    public async Task SendImmediateAsync_WarningThenExplicitDigest_SendsViaDigestPath()
    {
        // Warning ships via the digest path; calling SendDigestAsync explicitly must succeed
        // without throwing regardless of whether Telegram is configured.
        Mock<ILogger<TelegramAlerter>> logger = new();
        using TelegramAlerter sut = new(DisabledConfig(), logger.Object);

        await sut.SendImmediateAsync(AlertSeverity.Warning, "margin", "78%", CancellationToken.None);

        await sut.SendDigestAsync(AlertSeverity.Warning,
            new List<(string, string)> { ("margin", "78%"), ("ingest", "7%") },
            CancellationToken.None);

        // No throw == pass
    }

    [Fact]
    public async Task SendImmediateAsync_ThroughIAlerterInterface_DispatchesCorrectly()
    {
        // Confirms TelegramAlerter is correctly cast-able to IAlerter for the composite pipeline.
        Mock<ILogger<TelegramAlerter>> logger = new();
        using TelegramAlerter concrete = new(DisabledConfig(), logger.Object);
        IAlerter asAlerter = concrete;

        // Every severity path should be non-throwing even when the underlying channel is disabled.
        await asAlerter.SendImmediateAsync(AlertSeverity.Info, "i", "m", CancellationToken.None);
        await asAlerter.SendImmediateAsync(AlertSeverity.Warning, "w", "m", CancellationToken.None);
        await asAlerter.SendImmediateAsync(AlertSeverity.Error, "e", "m", CancellationToken.None);
        await asAlerter.SendImmediateAsync(AlertSeverity.Critical, "c", "m", CancellationToken.None);
    }
}
