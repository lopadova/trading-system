namespace SharedKernel.Ibkr;

/// <summary>
/// IBKR connection state machine.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Not connected to IBKR. Initial state.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// Connection attempt in progress.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Successfully connected to IBKR.
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Connection error occurred. May retry.
    /// </summary>
    Error = 3
}
