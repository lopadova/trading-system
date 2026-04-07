using Microsoft.Extensions.Logging;

namespace SharedKernel.Options;

/// <summary>
/// Implements Black-Scholes model for option Greeks calculation.
/// Reference: Black, F., & Scholes, M. (1973). The Pricing of Options and Corporate Liabilities.
/// </summary>
public sealed class BlackScholesCalculator : IGreeksCalculator
{
    private readonly ILogger<BlackScholesCalculator> _logger;

    // Constants for normal distribution calculation
    private const double OneOverSqrt2Pi = 0.3989422804014327; // 1 / sqrt(2 * PI)

    public BlackScholesCalculator(ILogger<BlackScholesCalculator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public GreeksData Calculate(
        double underlyingPrice,
        double strikePrice,
        double timeToExpiryYears,
        double riskFreeRate,
        double volatility,
        bool isCall,
        double? impliedVolatility = null)
    {
        // Validate inputs - negative-first conditionals
        if (underlyingPrice <= 0)
        {
            _logger.LogWarning("Invalid underlyingPrice={Price}, returning empty Greeks", underlyingPrice);
            return GreeksData.Empty;
        }

        if (strikePrice <= 0)
        {
            _logger.LogWarning("Invalid strikePrice={Price}, returning empty Greeks", strikePrice);
            return GreeksData.Empty;
        }

        if (timeToExpiryYears <= 0)
        {
            _logger.LogDebug("Time to expiry <= 0, option expired, returning empty Greeks");
            return GreeksData.Empty;
        }

        if (volatility <= 0)
        {
            _logger.LogWarning("Invalid volatility={Vol}, returning empty Greeks", volatility);
            return GreeksData.Empty;
        }

        try
        {
            // Use implied volatility if available, otherwise use historical/fallback
            double effectiveVol = impliedVolatility ?? volatility;

            // Calculate Black-Scholes intermediate values
            double sqrtT = Math.Sqrt(timeToExpiryYears);
            double volSqrtT = effectiveVol * sqrtT;

            // d1 = [ln(S/K) + (r + σ²/2)T] / (σ√T)
            double d1 = (Math.Log(underlyingPrice / strikePrice) +
                        (riskFreeRate + 0.5 * effectiveVol * effectiveVol) * timeToExpiryYears) / volSqrtT;

            // d2 = d1 - σ√T
            double d2 = d1 - volSqrtT;

            // Standard normal CDF values
            double Nd1 = NormalCDF(d1);
            double Nd2 = NormalCDF(d2);

            // Standard normal PDF value (same for both d1 and d2 in certain calculations)
            double nd1 = NormalPDF(d1);

            // Calculate Greeks
            double delta = CalculateDelta(Nd1, Nd2, isCall);
            double gamma = CalculateGamma(nd1, underlyingPrice, volSqrtT);
            double theta = CalculateTheta(underlyingPrice, strikePrice, nd1, Nd1, Nd2,
                                        effectiveVol, riskFreeRate, timeToExpiryYears, sqrtT, isCall);
            double vega = CalculateVega(underlyingPrice, nd1, sqrtT);

            _logger.LogDebug(
                "Calculated Greeks: Delta={Delta:F4}, Gamma={Gamma:F4}, Theta={Theta:F4}, Vega={Vega:F4}, " +
                "S={S}, K={K}, T={T:F4}, vol={Vol:F4}, isCall={IsCall}",
                delta, gamma, theta, vega, underlyingPrice, strikePrice, timeToExpiryYears, effectiveVol, isCall);

            return new GreeksData
            {
                Delta = delta,
                Gamma = gamma,
                Theta = theta,
                Vega = vega,
                ImpliedVolatility = impliedVolatility,
                CalculatedAtUtc = DateTime.UtcNow,
                UnderlyingPrice = underlyingPrice
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Greeks for S={S}, K={K}, T={T}, vol={Vol}",
                underlyingPrice, strikePrice, timeToExpiryYears, volatility);
            return GreeksData.Empty;
        }
    }

    /// <summary>
    /// Delta: ∂V/∂S
    /// Call: N(d1)
    /// Put: N(d1) - 1
    /// </summary>
    private static double CalculateDelta(double Nd1, double Nd2, bool isCall)
    {
        return isCall ? Nd1 : Nd1 - 1.0;
    }

    /// <summary>
    /// Gamma: ∂²V/∂S²
    /// Gamma = N'(d1) / (S * σ * √T)
    /// Same for calls and puts.
    /// </summary>
    private static double CalculateGamma(double nd1, double underlyingPrice, double volSqrtT)
    {
        return nd1 / (underlyingPrice * volSqrtT);
    }

    /// <summary>
    /// Theta: ∂V/∂T (per year, we convert to per day at the end)
    /// Call: -[S * N'(d1) * σ / (2√T)] - r * K * e^(-rT) * N(d2)
    /// Put:  -[S * N'(d1) * σ / (2√T)] + r * K * e^(-rT) * N(-d2)
    /// Result is in dollars per day (divide annual by 365).
    /// </summary>
    private static double CalculateTheta(
        double S, double K, double nd1, double Nd1, double Nd2,
        double vol, double r, double T, double sqrtT, bool isCall)
    {
        // First term: common for both call and put
        double term1 = -(S * nd1 * vol) / (2.0 * sqrtT);

        // Second term: different for call and put
        double discountFactor = Math.Exp(-r * T);
        double term2 = isCall
            ? -r * K * discountFactor * Nd2
            : r * K * discountFactor * (1.0 - Nd2);

        // Theta per year
        double thetaPerYear = term1 + term2;

        // Convert to per day (divide by 365)
        return thetaPerYear / 365.0;
    }

    /// <summary>
    /// Vega: ∂V/∂σ
    /// Vega = S * √T * N'(d1)
    /// Same for calls and puts.
    /// Result is dollars per 1 percentage point change in volatility (e.g., from 20% to 21%).
    /// </summary>
    private static double CalculateVega(double S, double nd1, double sqrtT)
    {
        // Vega for 1 percentage point change in volatility
        // Standard formula without scaling
        return S * sqrtT * nd1;
    }

    /// <summary>
    /// Standard normal cumulative distribution function (CDF).
    /// Uses rational approximation from Abramowitz and Stegun.
    /// Accuracy: |error| &lt; 7.5e-8
    /// </summary>
    private static double NormalCDF(double x)
    {
        // Handle extreme values
        if (x < -10.0) return 0.0;
        if (x > 10.0) return 1.0;

        // Abramowitz & Stegun approximation
        double k = 1.0 / (1.0 + 0.2316419 * Math.Abs(x));
        double k2 = k * k;
        double k3 = k2 * k;
        double k4 = k3 * k;
        double k5 = k4 * k;

        double pdf = OneOverSqrt2Pi * Math.Exp(-0.5 * x * x);
        double cdf = 1.0 - pdf * (0.319381530 * k - 0.356563782 * k2 + 1.781477937 * k3
                                  - 1.821255978 * k4 + 1.330274429 * k5);

        return x >= 0 ? cdf : 1.0 - cdf;
    }

    /// <summary>
    /// Standard normal probability density function (PDF).
    /// N'(x) = (1 / √(2π)) * e^(-x²/2)
    /// </summary>
    private static double NormalPDF(double x)
    {
        return OneOverSqrt2Pi * Math.Exp(-0.5 * x * x);
    }
}
