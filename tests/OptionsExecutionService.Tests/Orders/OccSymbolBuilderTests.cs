using OptionsExecutionService.Orders;
using Xunit;

namespace OptionsExecutionService.Tests.Orders;

public class OccSymbolBuilderTests
{
    [Fact]
    public void BuildSymbol_ValidInputs_ReturnsCorrectOccFormat()
    {
        // Arrange
        string underlying = "SPX";
        DateTime expiry = new(2025, 3, 21);
        decimal strike = 5000.00m;
        string right = "P";

        // Act
        string occSymbol = OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right);

        // Assert
        Assert.Equal("SPX   250321P05000000", occSymbol);
    }

    [Fact]
    public void BuildSymbol_ShortUnderlying_PadsToSixCharacters()
    {
        // Arrange
        string underlying = "SPY";
        DateTime expiry = new(2026, 12, 18);
        decimal strike = 450.50m;
        string right = "C";

        // Act
        string occSymbol = OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right);

        // Assert
        Assert.Equal("SPY   261218C00450500", occSymbol);
        Assert.Equal(21, occSymbol.Length); // OCC format is always 21 chars
    }

    [Fact]
    public void BuildSymbol_MaxLengthUnderlying_NoException()
    {
        // Arrange
        string underlying = "ABCDEF"; // exactly 6 chars
        DateTime expiry = new(2025, 1, 17);
        decimal strike = 100.00m;
        string right = "P";

        // Act
        string occSymbol = OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right);

        // Assert
        Assert.Equal("ABCDEF250117P00100000", occSymbol);
    }

    [Fact]
    public void BuildSymbol_UnderlyingTooLong_ThrowsArgumentException()
    {
        // Arrange
        string underlying = "TOOLONG"; // 7 chars, exceeds limit
        DateTime expiry = new(2025, 1, 17);
        decimal strike = 100.00m;
        string right = "P";

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right));
        Assert.Contains("exceeds 6 characters", ex.Message);
    }

    [Fact]
    public void BuildSymbol_EmptyUnderlying_ThrowsArgumentException()
    {
        // Arrange
        string underlying = "";
        DateTime expiry = new(2025, 1, 17);
        decimal strike = 100.00m;
        string right = "P";

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void BuildSymbol_DecimalStrike_FormatsCorrectly()
    {
        // Arrange
        string underlying = "QQQ";
        DateTime expiry = new(2025, 6, 20);
        decimal strike = 375.75m; // Strike with decimals
        string right = "C";

        // Act
        string occSymbol = OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right);

        // Assert
        // 375.75 * 1000 = 375750 → formatted as "00375750"
        Assert.Equal("QQQ   250620C00375750", occSymbol);
    }

    [Fact]
    public void BuildSymbol_ZeroStrike_FormatsCorrectly()
    {
        // Arrange
        string underlying = "SPX";
        DateTime expiry = new(2025, 1, 17);
        decimal strike = 0.00m;
        string right = "P";

        // Act
        string occSymbol = OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right);

        // Assert
        Assert.Equal("SPX   250117P00000000", occSymbol);
    }

    [Fact]
    public void BuildSymbol_NegativeStrike_ThrowsArgumentException()
    {
        // Arrange
        string underlying = "SPX";
        DateTime expiry = new(2025, 1, 17);
        decimal strike = -100.00m;
        string right = "P";

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right));
        Assert.Contains("must be non-negative", ex.Message);
    }

    [Fact]
    public void BuildSymbol_StrikeTooLarge_ThrowsArgumentException()
    {
        // Arrange
        string underlying = "SPX";
        DateTime expiry = new(2025, 1, 17);
        decimal strike = 100000.00m; // Exceeds 8-digit limit (max 99999.999)
        string right = "P";

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right));
        Assert.Contains("exceeds OCC format limit", ex.Message);
    }

    [Theory]
    [InlineData("C", "C")]
    [InlineData("c", "C")]
    [InlineData("P", "P")]
    [InlineData("p", "P")]
    public void BuildSymbol_RightCaseInsensitive_NormalizesToUppercase(string inputRight, string expectedRight)
    {
        // Arrange
        string underlying = "SPY";
        DateTime expiry = new(2025, 1, 17);
        decimal strike = 450.00m;

        // Act
        string occSymbol = OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, inputRight);

        // Assert
        Assert.Contains(expectedRight, occSymbol);
        Assert.Equal($"SPY   250117{expectedRight}00450000", occSymbol);
    }

    [Fact]
    public void BuildSymbol_InvalidRight_ThrowsArgumentException()
    {
        // Arrange
        string underlying = "SPY";
        DateTime expiry = new(2025, 1, 17);
        decimal strike = 450.00m;
        string right = "X"; // Invalid

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right));
        Assert.Contains("must be 'C' or 'P'", ex.Message);
    }

    [Fact]
    public void BuildSymbol_WithYyyyMmDdString_ParsesCorrectly()
    {
        // Arrange
        string underlying = "SPX";
        string expiryYyyyMmDd = "20250321";
        decimal strike = 5000.00m;
        string right = "P";

        // Act
        string occSymbol = OccSymbolBuilder.BuildSymbol(underlying, expiryYyyyMmDd, strike, right);

        // Assert
        Assert.Equal("SPX   250321P05000000", occSymbol);
    }

    [Fact]
    public void BuildSymbol_InvalidYyyyMmDdFormat_ThrowsArgumentException()
    {
        // Arrange
        string underlying = "SPX";
        string expiryYyyyMmDd = "2025-03-21"; // Wrong format (has dashes)
        decimal strike = 5000.00m;
        string right = "P";

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolBuilder.BuildSymbol(underlying, expiryYyyyMmDd, strike, right));
        Assert.Contains("Invalid expiry format", ex.Message);
    }

    [Fact]
    public void BuildSymbol_MultipleCallsWithDifferentStrikes_GenerateUniqueSymbols()
    {
        // Arrange
        string underlying = "SPX";
        DateTime expiry = new(2025, 3, 21);
        string right = "P";

        // Act
        string symbol1 = OccSymbolBuilder.BuildSymbol(underlying, expiry, 4950.00m, right);
        string symbol2 = OccSymbolBuilder.BuildSymbol(underlying, expiry, 5000.00m, right);
        string symbol3 = OccSymbolBuilder.BuildSymbol(underlying, expiry, 5050.00m, right);

        // Assert
        Assert.Equal("SPX   250321P04950000", symbol1);
        Assert.Equal("SPX   250321P05000000", symbol2);
        Assert.Equal("SPX   250321P05050000", symbol3);
        Assert.NotEqual(symbol1, symbol2);
        Assert.NotEqual(symbol2, symbol3);
    }
}
