using System.Globalization;

namespace OptionsExecutionService.Orders;

/// <summary>
/// Builds OCC (Options Clearing Corporation) standard option symbols.
/// Format: UNDERLYING(6 chars padded) + YYMMDD + C/P + STRIKE(8 digits, 3 decimals)
/// Example: "SPX   250321P05000000" = SPX Mar 21, 2025 5000.00 Put
/// </summary>
public static class OccSymbolBuilder
{
    /// <summary>
    /// Builds an OCC-format option symbol from components.
    /// </summary>
    /// <param name="underlying">Underlying symbol (e.g., "SPX", "SPY"). Will be padded to 6 characters.</param>
    /// <param name="expiry">Option expiry date</param>
    /// <param name="strike">Strike price (e.g., 5000.00)</param>
    /// <param name="right">"C" for Call or "P" for Put (case-insensitive)</param>
    /// <returns>OCC-format symbol (e.g., "SPX   250321P05000000")</returns>
    /// <exception cref="ArgumentException">If parameters are invalid</exception>
    public static string BuildSymbol(string underlying, DateTime expiry, decimal strike, string right)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(underlying))
        {
            throw new ArgumentException("Underlying symbol cannot be empty", nameof(underlying));
        }

        if (underlying.Length > 6)
        {
            throw new ArgumentException($"Underlying symbol '{underlying}' exceeds 6 characters (OCC limit)", nameof(underlying));
        }

        if (strike < 0)
        {
            throw new ArgumentException($"Strike price must be non-negative, got {strike}", nameof(strike));
        }

        if (strike >= 100000) // 8 digits max, 3 decimals → max value 99999.999
        {
            throw new ArgumentException($"Strike price {strike} exceeds OCC format limit (99999.999)", nameof(strike));
        }

        string normalizedRight = right.ToUpperInvariant();
        if (normalizedRight is not ("C" or "P"))
        {
            throw new ArgumentException($"Right must be 'C' or 'P', got '{right}'", nameof(right));
        }

        // Build OCC symbol
        // 1. Underlying: pad to 6 characters with spaces on the right
        string underlyingPadded = underlying.PadRight(6);

        // 2. Expiry: YYMMDD format
        string expiryFormatted = expiry.ToString("yyMMdd", CultureInfo.InvariantCulture);

        // 3. Right: C or P
        string rightFormatted = normalizedRight;

        // 4. Strike: 8 digits with 3 decimals (e.g., 5000.00 → "05000000")
        // Strike is multiplied by 1000 to shift decimals, then formatted as 8-digit integer
        int strikeMillis = (int)(strike * 1000m);
        string strikeFormatted = strikeMillis.ToString("D8", CultureInfo.InvariantCulture);

        return $"{underlyingPadded}{expiryFormatted}{rightFormatted}{strikeFormatted}";
    }

    /// <summary>
    /// Builds an OCC-format symbol from a Contract-like object (for testing/compatibility).
    /// </summary>
    /// <param name="underlying">Underlying symbol</param>
    /// <param name="expiryYyyyMmDd">Expiry in YYYYMMDD format (e.g., "20250321")</param>
    /// <param name="strike">Strike price</param>
    /// <param name="right">"C" or "P"</param>
    /// <returns>OCC-format symbol</returns>
    public static string BuildSymbol(string underlying, string expiryYyyyMmDd, decimal strike, string right)
    {
        // Parse YYYYMMDD to DateTime
        if (!DateTime.TryParseExact(expiryYyyyMmDd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiry))
        {
            throw new ArgumentException($"Invalid expiry format '{expiryYyyyMmDd}'. Expected YYYYMMDD.", nameof(expiryYyyyMmDd));
        }

        return BuildSymbol(underlying, expiry, strike, right);
    }
}
