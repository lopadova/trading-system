using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OptionsExecutionService.Services;
using Xunit;

namespace OptionsExecutionService.Tests.Services;

/// <summary>
/// Tests for AccountEquityProvider - singleton equity cache with freshness tracking.
/// Phase 2: Shared safety state P1 - Task RM-06
/// </summary>
public sealed class AccountEquityProviderTests
{
    /// <summary>
    /// Verifies that GetEquity returns null when no equity has been set.
    /// </summary>
    [Fact]
    public void GetEquity_ReturnsNull_WhenNoEquitySet()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<AccountEquityProvider>>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Safety:AccountBalanceMaxAgeSeconds"] = "300"
        }).Build();

        var provider = new AccountEquityProvider(config, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        AccountEquitySnapshot? snapshot = provider.GetEquity();

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.Null(snapshot);
    }

    /// <summary>
    /// Verifies that fresh equity data is marked as not stale.
    /// </summary>
    [Fact]
    public void GetEquity_ReturnsFreshSnapshot_WhenRecentlyUpdated()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<AccountEquityProvider>>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Safety:AccountBalanceMaxAgeSeconds"] = "300"
        }).Build();

        var provider = new AccountEquityProvider(config, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        DateTime now = DateTime.UtcNow;
        provider.UpdateEquity(100000m, now);
        AccountEquitySnapshot? snapshot = provider.GetEquity();

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.NotNull(snapshot);
        Assert.Equal(100000m, snapshot.NetLiquidation);
        Assert.Equal(now, snapshot.AsOfUtc);
        Assert.False(snapshot.IsStale);
        Assert.True(snapshot.Age.TotalSeconds < 1);
    }

    /// <summary>
    /// Verifies that old equity data is marked as stale.
    /// </summary>
    [Fact]
    public void GetEquity_MarksStale_WhenEquityIsOld()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<AccountEquityProvider>>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Safety:AccountBalanceMaxAgeSeconds"] = "1" // 1 second threshold
        }).Build();

        var provider = new AccountEquityProvider(config, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        DateTime old = DateTime.UtcNow.AddSeconds(-10); // 10 seconds old
        provider.UpdateEquity(100000m, old);
        AccountEquitySnapshot? snapshot = provider.GetEquity();

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.NotNull(snapshot);
        Assert.Equal(100000m, snapshot.NetLiquidation);
        Assert.Equal(old, snapshot.AsOfUtc);
        Assert.True(snapshot.IsStale);
        Assert.True(snapshot.Age.TotalSeconds >= 10);
    }

    /// <summary>
    /// Verifies that UpdateEquity overwrites previous value.
    /// </summary>
    [Fact]
    public void UpdateEquity_OverwritesPreviousValue()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<AccountEquityProvider>>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Safety:AccountBalanceMaxAgeSeconds"] = "300"
        }).Build();

        var provider = new AccountEquityProvider(config, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        DateTime firstUpdate = DateTime.UtcNow.AddSeconds(-1);
        provider.UpdateEquity(50000m, firstUpdate);

        DateTime secondUpdate = DateTime.UtcNow;
        provider.UpdateEquity(75000m, secondUpdate);

        AccountEquitySnapshot? snapshot = provider.GetEquity();

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.NotNull(snapshot);
        Assert.Equal(75000m, snapshot.NetLiquidation);
        Assert.Equal(secondUpdate, snapshot.AsOfUtc);
    }

    /// <summary>
    /// Verifies that default max age is 300 seconds when not configured.
    /// </summary>
    [Fact]
    public void Constructor_UsesDefaultMaxAge_WhenNotConfigured()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<AccountEquityProvider>>();
        var config = new ConfigurationBuilder().Build(); // Empty config

        var provider = new AccountEquityProvider(config, mockLogger.Object);

        // ============================================================
        // ACT
        // ============================================================

        // Update with equity from 299 seconds ago (just under default 300s threshold)
        DateTime almostStale = DateTime.UtcNow.AddSeconds(-299);
        provider.UpdateEquity(100000m, almostStale);
        AccountEquitySnapshot? snapshot1 = provider.GetEquity();

        // Update with equity from 301 seconds ago (just over default 300s threshold)
        provider.UpdateEquity(100000m, DateTime.UtcNow.AddSeconds(-301));
        AccountEquitySnapshot? snapshot2 = provider.GetEquity();

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.NotNull(snapshot1);
        Assert.False(snapshot1.IsStale, "299 seconds should be fresh with default 300s threshold");

        Assert.NotNull(snapshot2);
        Assert.True(snapshot2.IsStale, "301 seconds should be stale with default 300s threshold");
    }

    /// <summary>
    /// CRITICAL TEST: Verifies that equity state persists across multiple requests.
    /// This is the key requirement of RM-06: singleton provider must maintain state.
    /// </summary>
    [Fact]
    public void AccountEquityProvider_PersistsStateAcrossRequests()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        var mockLogger = new Mock<ILogger<AccountEquityProvider>>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Safety:AccountBalanceMaxAgeSeconds"] = "300"
        }).Build();

        // Single singleton instance
        var sharedProvider = new AccountEquityProvider(config, mockLogger.Object);

        // ============================================================
        // ACT & ASSERT
        // ============================================================

        // Request 1: No equity yet
        AccountEquitySnapshot? snapshot1 = sharedProvider.GetEquity();
        Assert.Null(snapshot1);

        // Request 2: Update equity
        DateTime now = DateTime.UtcNow;
        sharedProvider.UpdateEquity(100000m, now);
        AccountEquitySnapshot? snapshot2 = sharedProvider.GetEquity();
        Assert.NotNull(snapshot2);
        Assert.Equal(100000m, snapshot2.NetLiquidation);

        // Request 3: Same singleton sees updated value
        AccountEquitySnapshot? snapshot3 = sharedProvider.GetEquity();
        Assert.NotNull(snapshot3);
        Assert.Equal(100000m, snapshot3.NetLiquidation);
        Assert.Equal(now, snapshot3.AsOfUtc);

        // This test verifies the fix for RM-06: before Phase 2, account balance
        // was cached locally in scoped OrderPlacer and reset every worker cycle.
        // After Phase 2, equity is in singleton and persists across all requests.
    }
}
