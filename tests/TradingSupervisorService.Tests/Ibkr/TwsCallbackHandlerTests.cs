using IBApi;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;
using TradingSupervisorService.Ibkr;
using Xunit;

namespace TradingSupervisorService.Tests.Ibkr;

/// <summary>
/// Tests for TwsCallbackHandler (EWrapper implementation).
/// </summary>
public sealed class TwsCallbackHandlerTests
{
    private readonly ILogger<TwsCallbackHandler> _logger;

    public TwsCallbackHandlerTests()
    {
        _logger = LoggerFactory
            .Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<TwsCallbackHandler>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        // Arrange
        ILogger<TwsCallbackHandler> nullLogger = null!;
        Action<ConnectionState> callback = _ => { };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TwsCallbackHandler(nullLogger, callback));
    }

    [Fact]
    public void Constructor_NullCallback_Throws()
    {
        // Arrange
        Action<ConnectionState> nullCallback = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TwsCallbackHandler(_logger, nullCallback));
    }

    [Fact]
    public void ConnectionClosed_InvokesCallback()
    {
        // Arrange
        ConnectionState? capturedState = null;
        Action<ConnectionState> callback = state => capturedState = state;
        TwsCallbackHandler handler = new(_logger, callback);

        // Act
        handler.connectionClosed();

        // Assert
        Assert.NotNull(capturedState);
        Assert.Equal(ConnectionState.Disconnected, capturedState.Value);
    }

    [Fact]
    public void ConnectAck_InvokesCallback()
    {
        // Arrange
        ConnectionState? capturedState = null;
        Action<ConnectionState> callback = state => capturedState = state;
        TwsCallbackHandler handler = new(_logger, callback);

        // Act
        handler.connectAck();

        // Assert
        Assert.NotNull(capturedState);
        Assert.Equal(ConnectionState.Connected, capturedState.Value);
    }

    [Fact]
    public void CurrentTime_UpdatesLastServerTime()
    {
        // Arrange
        Action<ConnectionState> callback = _ => { };
        TwsCallbackHandler handler = new(_logger, callback);
        long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        handler.currentTime(unixTime);

        // Assert
        Assert.NotEqual(DateTime.MinValue, handler.LastServerTime);
        Assert.InRange(handler.LastServerTime, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public void Error_ConnectionLost1100_InvokesDisconnectedCallback()
    {
        // Arrange
        ConnectionState? capturedState = null;
        Action<ConnectionState> callback = state => capturedState = state;
        TwsCallbackHandler handler = new(_logger, callback);

        // Act - error code 1100 = connection lost
        handler.error(0, 0L, 1100, "Connection lost", "");

        // Assert
        Assert.NotNull(capturedState);
        Assert.Equal(ConnectionState.Disconnected, capturedState.Value);
    }

    [Fact]
    public void Error_ConnectionRestored1101_InvokesConnectedCallback()
    {
        // Arrange
        ConnectionState? capturedState = null;
        Action<ConnectionState> callback = state => capturedState = state;
        TwsCallbackHandler handler = new(_logger, callback);

        // Act - error code 1101 = connection restored
        handler.error(0, 0L, 1101, "Connection restored", "");

        // Assert
        Assert.NotNull(capturedState);
        Assert.Equal(ConnectionState.Connected, capturedState.Value);
    }

    [Fact]
    public void Error_InformationalCode2104_DoesNotThrow()
    {
        // Arrange
        Action<ConnectionState> callback = _ => { };
        TwsCallbackHandler handler = new(_logger, callback);

        // Act & Assert - informational codes should not throw
        Exception? ex = Record.Exception(() => handler.error(0, 0L, 2104, "Market data farm OK", ""));
        Assert.Null(ex);
    }

    [Fact]
    public void TickPrice_DoesNotThrow()
    {
        // Arrange
        Action<ConnectionState> callback = _ => { };
        TwsCallbackHandler handler = new(_logger, callback);
        TickAttrib attrib = new() { CanAutoExecute = false, PastLimit = false };

        // Act & Assert
        Exception? ex = Record.Exception(() => handler.tickPrice(1001, 4, 4500.50, attrib));
        Assert.Null(ex);
    }

    [Fact]
    public void TickSize_DoesNotThrow()
    {
        // Arrange
        Action<ConnectionState> callback = _ => { };
        TwsCallbackHandler handler = new(_logger, callback);

        // Act & Assert
        Exception? ex = Record.Exception(() => handler.tickSize(1001, 0, 100));
        Assert.Null(ex);
    }

    [Fact]
    public void TickOptionComputation_DoesNotThrow()
    {
        // Arrange
        Action<ConnectionState> callback = _ => { };
        TwsCallbackHandler handler = new(_logger, callback);

        // Act & Assert
        Exception? ex = Record.Exception(() =>
            handler.tickOptionComputation(2000, 10, 0, 0.25, -0.45, 18.5, 0, 0.15, 0.08, -0.05, 4500));
        Assert.Null(ex);
    }

    [Fact]
    public void AllUnusedCallbacks_DoNotThrow()
    {
        // Arrange
        Action<ConnectionState> callback = _ => { };
        TwsCallbackHandler handler = new(_logger, callback);

        // Act & Assert - ensure all stub methods compile and don't throw
        Assert.NotNull(handler);

        // Sample a few unused callbacks
        Exception? ex1 = Record.Exception(() => handler.accountDownloadEnd("U123456"));
        Assert.Null(ex1);

        Exception? ex2 = Record.Exception(() => handler.positionEnd());
        Assert.Null(ex2);

        Exception? ex3 = Record.Exception(() => handler.openOrderEnd());
        Assert.Null(ex3);
    }
}
