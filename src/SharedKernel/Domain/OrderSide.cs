namespace SharedKernel.Domain;

/// <summary>
/// Represents the side of an order (Buy or Sell).
/// </summary>
public enum OrderSide
{
    /// <summary>
    /// Buy order - opening a long position or closing a short position.
    /// </summary>
    Buy = 0,

    /// <summary>
    /// Sell order - opening a short position or closing a long position.
    /// </summary>
    Sell = 1
}
