using SharedKernel.Ibkr;
using Xunit;

namespace TradingSupervisorService.Tests.Ibkr;

/// <summary>
/// Tests for IbkrConfig validation and safety rules.
/// </summary>
public sealed class IbkrConfigTests
{
    [Fact]
    public void Validate_ValidPaperConfig_DoesNotThrow()
    {
        // Arrange
        IbkrConfig config = new()
        {
            Host = "127.0.0.1",
            Port = 7497, // TWS Paper
            ClientId = 1,
            TradingMode = SharedKernel.Domain.TradingMode.Paper
        };

        // Act & Assert
        Exception? ex = Record.Exception(() => config.Validate());
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_IbGatewayPaperPort_DoesNotThrow()
    {
        // Arrange
        IbkrConfig config = new()
        {
            Port = 4001, // IB Gateway Paper
            TradingMode = SharedKernel.Domain.TradingMode.Paper
        };

        // Act & Assert
        Exception? ex = Record.Exception(() => config.Validate());
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_LiveTradingPort7496_Throws()
    {
        // Arrange
        IbkrConfig config = new()
        {
            Port = 7496, // TWS Live - FORBIDDEN
            TradingMode = SharedKernel.Domain.TradingMode.Paper
        };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("LIVE trading port", ex.Message);
    }

    [Fact]
    public void Validate_LiveTradingPort4002_Throws()
    {
        // Arrange
        IbkrConfig config = new()
        {
            Port = 4002, // IB Gateway Live - FORBIDDEN
            TradingMode = SharedKernel.Domain.TradingMode.Paper
        };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("LIVE trading port", ex.Message);
    }

    [Fact]
    public void Validate_LiveTradingMode_Throws()
    {
        // Arrange
        IbkrConfig config = new()
        {
            Port = 7497, // Paper port
            TradingMode = SharedKernel.Domain.TradingMode.Live // FORBIDDEN
        };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("Only Paper trading mode", ex.Message);
    }

    [Fact]
    public void Validate_EmptyHost_Throws()
    {
        // Arrange
        IbkrConfig config = new()
        {
            Host = "",
            Port = 7497
        };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Equal("Host", ex.ParamName);
    }

    [Fact]
    public void Validate_InvalidPort_Throws()
    {
        // Arrange
        IbkrConfig config = new() { Port = 0 };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Equal("Port", ex.ParamName);
    }

    [Fact]
    public void Validate_PortTooHigh_Throws()
    {
        // Arrange
        IbkrConfig config = new() { Port = 99999 };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Equal("Port", ex.ParamName);
    }

    [Fact]
    public void Validate_NegativeClientId_Throws()
    {
        // Arrange
        IbkrConfig config = new() { ClientId = -1 };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Equal("ClientId", ex.ParamName);
    }

    [Fact]
    public void Validate_ZeroReconnectDelay_Throws()
    {
        // Arrange
        IbkrConfig config = new() { ReconnectInitialDelaySeconds = 0 };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Equal("ReconnectInitialDelaySeconds", ex.ParamName);
    }

    [Fact]
    public void Validate_MaxDelayLessThanInitialDelay_Throws()
    {
        // Arrange
        IbkrConfig config = new()
        {
            ReconnectInitialDelaySeconds = 10,
            ReconnectMaxDelaySeconds = 5 // Less than initial
        };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Equal("ReconnectMaxDelaySeconds", ex.ParamName);
    }

    [Fact]
    public void Validate_NegativeMaxReconnectAttempts_Throws()
    {
        // Arrange
        IbkrConfig config = new() { MaxReconnectAttempts = -5 };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Equal("MaxReconnectAttempts", ex.ParamName);
    }

    [Fact]
    public void Validate_ZeroConnectionTimeout_Throws()
    {
        // Arrange
        IbkrConfig config = new() { ConnectionTimeoutSeconds = 0 };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Equal("ConnectionTimeoutSeconds", ex.ParamName);
    }

    [Fact]
    public void DefaultConfig_IsPaperMode()
    {
        // Arrange & Act
        IbkrConfig config = new();

        // Assert - safety-first defaults
        Assert.Equal(SharedKernel.Domain.TradingMode.Paper, config.TradingMode);
        Assert.Equal(7497, config.Port); // TWS Paper port
    }
}
