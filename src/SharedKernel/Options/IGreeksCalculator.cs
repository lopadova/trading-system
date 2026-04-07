namespace SharedKernel.Options;

/// <summary>
/// Service for calculating option Greeks using Black-Scholes model.
/// </summary>
public interface IGreeksCalculator
{
    /// <summary>
    /// Calculates Greeks for an option contract.
    /// </summary>
    /// <param name="underlyingPrice">Current underlying price</param>
    /// <param name="strikePrice">Option strike price</param>
    /// <param name="timeToExpiryYears">Time to expiration in years (use DaysToExpiry / 365.0)</param>
    /// <param name="riskFreeRate">Risk-free rate (annualized, e.g., 0.05 for 5%)</param>
    /// <param name="volatility">Volatility (annualized, e.g., 0.20 for 20% IV)</param>
    /// <param name="isCall">True for call option, false for put</param>
    /// <param name="impliedVolatility">Optional IBKR implied volatility (if available)</param>
    /// <returns>Greeks data</returns>
    GreeksData Calculate(
        double underlyingPrice,
        double strikePrice,
        double timeToExpiryYears,
        double riskFreeRate,
        double volatility,
        bool isCall,
        double? impliedVolatility = null);
}
