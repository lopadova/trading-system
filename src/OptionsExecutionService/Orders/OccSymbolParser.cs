using System.Globalization;

namespace OptionsExecutionService.Orders;

/// <summary>
/// Parses OCC (Options Clearing Corporation) standard option symbols.
/// Format: UNDERLYING(6 chars) + YYMMDD + C/P + STRIKE(8 digits, 3 decimals)
/// Example: "SPX   250321P05000000" → underlying="SPX", expiry=2025-03-21, strike=5000.00, right="P"
/// </summary>
public static class OccSymbolParser
{
    /// <summary>
    /// Represents the parsed components of an OCC option symbol.
    /// </summary>
    public sealed record OccSymbolComponents
    {
        public required string Underlying { get; init; }
        public required DateTime Expiry { get; init; }
        public required decimal Strike { get; init; }
        public required string Right { get; init; } // "C" or "P"

        /// <summary>
        /// Expiry in YYYYMMDD format (e.g., "20250321") for IBKR compatibility.
        /// </summary>
        public string ExpiryYyyyMmDd => Expiry.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an OCC-format option symbol into its components.
    /// </summary>
    /// <param name="occSymbol">OCC symbol (e.g., "SPX   250321P05000000")</param>
    /// <returns>Parsed components</returns>
    /// <exception cref="ArgumentException">If symbol format is invalid</exception>
    public static OccSymbolComponents Parse(string occSymbol)
    {
        if (string.IsNullOrWhiteSpace(occSymbol))
        {
            throw new ArgumentException("OCC symbol cannot be empty", nameof(occSymbol));
        }

        // OCC format: 6 chars underlying + 6 chars date (YYMMDD) + 1 char right + 8 digits strike = 21 chars total
        if (occSymbol.Length != 21)
        {
            throw new ArgumentException(
                $"Invalid OCC symbol format: expected 21 characters, got {occSymbol.Length}. Symbol: '{occSymbol}'",
                nameof(occSymbol));
        }

        try
        {
            // 1. Underlying: first 6 characters, trim trailing spaces
            string underlying = occSymbol.Substring(0, 6).TrimEnd();

            if (string.IsNullOrWhiteSpace(underlying))
            {
                throw new ArgumentException($"Invalid OCC symbol: underlying is empty. Symbol: '{occSymbol}'");
            }

            // 2. Expiry: chars 6-11 (YYMMDD format)
            string expiryStr = occSymbol.Substring(6, 6);
            if (!DateTime.TryParseExact(expiryStr, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiry))
            {
                throw new ArgumentException($"Invalid OCC symbol: expiry '{expiryStr}' is not in YYMMDD format. Symbol: '{occSymbol}'");
            }

            // 3. Right: char 12 (C or P)
            string right = occSymbol.Substring(12, 1).ToUpperInvariant();
            if (right is not ("C" or "P"))
            {
                throw new ArgumentException($"Invalid OCC symbol: right '{right}' must be 'C' or 'P'. Symbol: '{occSymbol}'");
            }

            // 4. Strike: chars 13-20 (8 digits representing price * 1000)
            string strikeStr = occSymbol.Substring(13, 8);
            if (!int.TryParse(strikeStr, NumberStyles.None, CultureInfo.InvariantCulture, out int strikeMillis))
            {
                throw new ArgumentException($"Invalid OCC symbol: strike '{strikeStr}' is not a valid integer. Symbol: '{occSymbol}'");
            }

            // Convert millis back to decimal (divide by 1000)
            decimal strike = strikeMillis / 1000m;

            return new OccSymbolComponents
            {
                Underlying = underlying,
                Expiry = expiry,
                Strike = strike,
                Right = right
            };
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ArgumentException($"Failed to parse OCC symbol '{occSymbol}': {ex.Message}", nameof(occSymbol), ex);
        }
    }

    /// <summary>
    /// Tries to parse an OCC-format symbol. Returns false if format is invalid.
    /// </summary>
    public static bool TryParse(string occSymbol, out OccSymbolComponents? components)
    {
        try
        {
            components = Parse(occSymbol);
            return true;
        }
        catch (ArgumentException)
        {
            components = null;
            return false;
        }
    }

    /// <summary>
    /// Checks if a string is a valid OCC-format option symbol.
    /// </summary>
    public static bool IsValidOccSymbol(string symbol)
    {
        return TryParse(symbol, out _);
    }
}
