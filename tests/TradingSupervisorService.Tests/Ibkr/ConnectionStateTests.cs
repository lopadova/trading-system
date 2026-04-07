using SharedKernel.Ibkr;
using Xunit;

namespace TradingSupervisorService.Tests.Ibkr;

/// <summary>
/// Tests for ConnectionState enum and state transitions.
/// </summary>
public sealed class ConnectionStateTests
{
    [Fact]
    public void ConnectionState_DefaultIsDisconnected()
    {
        // Arrange & Act
        ConnectionState state = default;

        // Assert - safety-first: default (0) is Disconnected
        Assert.Equal(ConnectionState.Disconnected, state);
        Assert.Equal(0, (int)state);
    }

    [Fact]
    public void ConnectionState_AllValuesAreValid()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)ConnectionState.Disconnected);
        Assert.Equal(1, (int)ConnectionState.Connecting);
        Assert.Equal(2, (int)ConnectionState.Connected);
        Assert.Equal(3, (int)ConnectionState.Error);
    }

    [Fact]
    public void ConnectionState_CanConvertToString()
    {
        // Arrange & Act & Assert
        Assert.Equal("Disconnected", ConnectionState.Disconnected.ToString());
        Assert.Equal("Connecting", ConnectionState.Connecting.ToString());
        Assert.Equal("Connected", ConnectionState.Connected.ToString());
        Assert.Equal("Error", ConnectionState.Error.ToString());
    }
}
