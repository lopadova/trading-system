using SharedKernel.Domain;
using Xunit;

namespace SharedKernel.Tests;

/// <summary>
/// Basic sanity tests for domain enums.
/// Ensures enums have expected values and default behaviors.
/// </summary>
public sealed class DomainTypesTests
{
    [Fact]
    public void TradingMode_DefaultValue_IsPaper()
    {
        // SAFETY CRITICAL: Default TradingMode must always be Paper (0)
        TradingMode defaultMode = default;
        Assert.Equal(TradingMode.Paper, defaultMode);
        Assert.Equal(0, (int)defaultMode);
    }

    [Fact]
    public void TradingMode_LiveValue_IsOne()
    {
        // Verify Live mode has explicit value 1
        Assert.Equal(1, (int)TradingMode.Live);
    }

    [Fact]
    public void AlertType_HasExpectedValues()
    {
        // Verify all alert types have expected enum values
        Assert.Equal(0, (int)AlertType.SystemHealth);
        Assert.Equal(1, (int)AlertType.TradeExecution);
        Assert.Equal(2, (int)AlertType.StrategySignal);
        Assert.Equal(3, (int)AlertType.RiskManagement);
        Assert.Equal(4, (int)AlertType.ConnectionStatus);
        Assert.Equal(5, (int)AlertType.Configuration);
        Assert.Equal(99, (int)AlertType.Error);
    }

    [Fact]
    public void AlertSeverity_DefaultValue_IsInfo()
    {
        // Default severity should be Info (least critical)
        AlertSeverity defaultSeverity = default;
        Assert.Equal(AlertSeverity.Info, defaultSeverity);
        Assert.Equal(0, (int)defaultSeverity);
    }

    [Fact]
    public void AlertSeverity_HasExpectedOrder()
    {
        // Verify severity levels increase in criticality
        Assert.True((int)AlertSeverity.Info < (int)AlertSeverity.Warning);
        Assert.True((int)AlertSeverity.Warning < (int)AlertSeverity.Error);
        Assert.True((int)AlertSeverity.Error < (int)AlertSeverity.Critical);
    }
}
