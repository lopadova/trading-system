namespace SharedKernel.Ibkr;

/// <summary>
/// IBKR connection configuration. Immutable.
/// </summary>
public sealed record IbkrConfig
{
    /// <summary>
    /// IBKR host address. Default: 127.0.0.1
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>
    /// IBKR port.
    /// 7497 = TWS Paper, 7496 = TWS Live
    /// 4002 = IB Gateway Paper, 4001 = IB Gateway Live
    /// NOTE: Only paper ports (7497, 4002) are allowed by Validate().
    /// </summary>
    public int Port { get; init; } = 7497;

    /// <summary>
    /// Client ID must be unique per connection. Default: 1
    /// </summary>
    public int ClientId { get; init; } = 1;

    /// <summary>
    /// Trading mode. MUST be Paper for safety.
    /// </summary>
    public Domain.TradingMode TradingMode { get; init; } = Domain.TradingMode.Paper;

    /// <summary>
    /// Initial reconnect delay in seconds. Default: 5
    /// </summary>
    public int ReconnectInitialDelaySeconds { get; init; } = 5;

    /// <summary>
    /// Maximum reconnect delay in seconds (exponential backoff cap). Default: 300 (5 minutes)
    /// </summary>
    public int ReconnectMaxDelaySeconds { get; init; } = 300;

    /// <summary>
    /// Maximum reconnect attempts before giving up. 0 = infinite. Default: 0
    /// </summary>
    public int MaxReconnectAttempts { get; init; } = 0;

    /// <summary>
    /// Connection timeout in seconds. Default: 10
    /// </summary>
    public int ConnectionTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Validates configuration. Throws ArgumentException if invalid.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ArgumentException("Host cannot be empty", nameof(Host));
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new ArgumentException($"Port must be between 1 and 65535, got {Port}", nameof(Port));
        }

        // SAFETY: Enforce paper trading only
        if (Port == 7496 || Port == 4001)
        {
            throw new ArgumentException(
                $"Port {Port} is LIVE trading port. Only paper trading ports (7497, 4002) are allowed.",
                nameof(Port));
        }

        if (TradingMode != Domain.TradingMode.Paper)
        {
            throw new ArgumentException(
                "Only Paper trading mode is allowed. Never connect to live trading without explicit authorization.",
                nameof(TradingMode));
        }

        if (ClientId < 0)
        {
            throw new ArgumentException($"ClientId must be non-negative, got {ClientId}", nameof(ClientId));
        }

        if (ReconnectInitialDelaySeconds <= 0)
        {
            throw new ArgumentException(
                $"ReconnectInitialDelaySeconds must be positive, got {ReconnectInitialDelaySeconds}",
                nameof(ReconnectInitialDelaySeconds));
        }

        if (ReconnectMaxDelaySeconds < ReconnectInitialDelaySeconds)
        {
            throw new ArgumentException(
                $"ReconnectMaxDelaySeconds ({ReconnectMaxDelaySeconds}) must be >= ReconnectInitialDelaySeconds ({ReconnectInitialDelaySeconds})",
                nameof(ReconnectMaxDelaySeconds));
        }

        if (MaxReconnectAttempts < 0)
        {
            throw new ArgumentException(
                $"MaxReconnectAttempts must be non-negative, got {MaxReconnectAttempts}",
                nameof(MaxReconnectAttempts));
        }

        if (ConnectionTimeoutSeconds <= 0)
        {
            throw new ArgumentException(
                $"ConnectionTimeoutSeconds must be positive, got {ConnectionTimeoutSeconds}",
                nameof(ConnectionTimeoutSeconds));
        }
    }
}
