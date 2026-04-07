namespace SharedKernel.Domain;

/// <summary>
/// Represents the type of order.
/// SAFETY: Market is default (0) as it's most likely to fill but requires careful position sizing.
/// </summary>
public enum OrderType
{
    /// <summary>
    /// Market order - execute at current market price immediately.
    /// Highest execution certainty but no price control.
    /// </summary>
    Market = 0,

    /// <summary>
    /// Limit order - execute only at specified price or better.
    /// Price control but no execution certainty.
    /// </summary>
    Limit = 1,

    /// <summary>
    /// Stop order - becomes market order when stop price is reached.
    /// Used for stop-loss orders.
    /// </summary>
    Stop = 2,

    /// <summary>
    /// Stop-limit order - becomes limit order when stop price is reached.
    /// Used for stop-loss with price protection.
    /// </summary>
    StopLimit = 3
}
