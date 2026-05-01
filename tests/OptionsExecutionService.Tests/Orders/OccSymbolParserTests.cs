using OptionsExecutionService.Orders;
using Xunit;

namespace OptionsExecutionService.Tests.Orders;

public class OccSymbolParserTests
{
    [Fact]
    public void Parse_ValidOccSymbol_ReturnsCorrectComponents()
    {
        // Arrange
        string occSymbol = "SPX   250321P05000000";

        // Act
        OccSymbolParser.OccSymbolComponents components = OccSymbolParser.Parse(occSymbol);

        // Assert
        Assert.Equal("SPX", components.Underlying);
        Assert.Equal(new DateTime(2025, 3, 21), components.Expiry);
        Assert.Equal(5000.00m, components.Strike);
        Assert.Equal("P", components.Right);
        Assert.Equal("20250321", components.ExpiryYyyyMmDd);
    }

    [Fact]
    public void Parse_CallOption_ParsesCorrectly()
    {
        // Arrange
        string occSymbol = "SPY   261218C00450500";

        // Act
        OccSymbolParser.OccSymbolComponents components = OccSymbolParser.Parse(occSymbol);

        // Assert
        Assert.Equal("SPY", components.Underlying);
        Assert.Equal(new DateTime(2026, 12, 18), components.Expiry);
        Assert.Equal(450.50m, components.Strike);
        Assert.Equal("C", components.Right);
    }

    [Fact]
    public void Parse_SixCharUnderlying_ParsesCorrectly()
    {
        // Arrange
        string occSymbol = "ABCDEF250117P00100000";

        // Act
        OccSymbolParser.OccSymbolComponents components = OccSymbolParser.Parse(occSymbol);

        // Assert
        Assert.Equal("ABCDEF", components.Underlying);
        Assert.Equal(new DateTime(2025, 1, 17), components.Expiry);
        Assert.Equal(100.00m, components.Strike);
        Assert.Equal("P", components.Right);
    }

    [Fact]
    public void Parse_DecimalStrike_ParsesCorrectly()
    {
        // Arrange
        string occSymbol = "QQQ   250620C00375750"; // 375.75

        // Act
        OccSymbolParser.OccSymbolComponents components = OccSymbolParser.Parse(occSymbol);

        // Assert
        Assert.Equal("QQQ", components.Underlying);
        Assert.Equal(375.75m, components.Strike);
    }

    [Fact]
    public void Parse_ZeroStrike_ParsesCorrectly()
    {
        // Arrange
        string occSymbol = "SPX   250117P00000000";

        // Act
        OccSymbolParser.OccSymbolComponents components = OccSymbolParser.Parse(occSymbol);

        // Assert
        Assert.Equal(0.00m, components.Strike);
    }

    [Fact]
    public void Parse_EmptySymbol_ThrowsArgumentException()
    {
        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolParser.Parse(""));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void Parse_NullSymbol_ThrowsArgumentException()
    {
        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolParser.Parse(null!));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void Parse_WrongLength_ThrowsArgumentException()
    {
        // Arrange
        string occSymbol = "SPX250321P05000000"; // 18 chars (missing padding)

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolParser.Parse(occSymbol));
        Assert.Contains("expected 21 characters", ex.Message);
    }

    [Fact]
    public void Parse_InvalidExpiry_ThrowsArgumentException()
    {
        // Arrange
        string occSymbol = "SPX   999999P05000000"; // Invalid date

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolParser.Parse(occSymbol));
        Assert.Contains("not in YYMMDD format", ex.Message);
    }

    [Fact]
    public void Parse_InvalidRight_ThrowsArgumentException()
    {
        // Arrange
        string occSymbol = "SPX   250321X05000000"; // 'X' instead of C/P

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolParser.Parse(occSymbol));
        Assert.Contains("must be 'C' or 'P'", ex.Message);
    }

    [Fact]
    public void Parse_InvalidStrike_ThrowsArgumentException()
    {
        // Arrange
        string occSymbol = "SPX   250321PABCDEFGH"; // Non-numeric strike

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OccSymbolParser.Parse(occSymbol));
        Assert.Contains("not a valid integer", ex.Message);
    }

    [Fact]
    public void Parse_LowercaseRight_NormalizesToUppercase()
    {
        // Arrange
        string occSymbol = "SPX   250321p05000000"; // lowercase 'p'

        // Act
        OccSymbolParser.OccSymbolComponents components = OccSymbolParser.Parse(occSymbol);

        // Assert
        Assert.Equal("P", components.Right); // Should be normalized to uppercase
    }

    [Fact]
    public void TryParse_ValidSymbol_ReturnsTrue()
    {
        // Arrange
        string occSymbol = "SPX   250321P05000000";

        // Act
        bool success = OccSymbolParser.TryParse(occSymbol, out OccSymbolParser.OccSymbolComponents? components);

        // Assert
        Assert.True(success);
        Assert.NotNull(components);
        Assert.Equal("SPX", components.Underlying);
    }

    [Fact]
    public void TryParse_InvalidSymbol_ReturnsFalse()
    {
        // Arrange
        string occSymbol = "INVALID";

        // Act
        bool success = OccSymbolParser.TryParse(occSymbol, out OccSymbolParser.OccSymbolComponents? components);

        // Assert
        Assert.False(success);
        Assert.Null(components);
    }

    [Fact]
    public void IsValidOccSymbol_ValidSymbol_ReturnsTrue()
    {
        // Arrange
        string occSymbol = "SPX   250321P05000000";

        // Act
        bool isValid = OccSymbolParser.IsValidOccSymbol(occSymbol);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidOccSymbol_InvalidSymbol_ReturnsFalse()
    {
        // Arrange
        string occSymbol = "SPX-5000.00-P"; // Old placeholder format

        // Act
        bool isValid = OccSymbolParser.IsValidOccSymbol(occSymbol);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Parse_RoundTrip_BuildAndParse_PreservesComponents()
    {
        // Arrange
        string underlying = "SPX";
        DateTime expiry = new(2025, 3, 21);
        decimal strike = 5000.00m;
        string right = "P";

        // Act
        string occSymbol = OccSymbolBuilder.BuildSymbol(underlying, expiry, strike, right);
        OccSymbolParser.OccSymbolComponents components = OccSymbolParser.Parse(occSymbol);

        // Assert
        Assert.Equal(underlying, components.Underlying);
        Assert.Equal(expiry, components.Expiry);
        Assert.Equal(strike, components.Strike);
        Assert.Equal(right, components.Right);
    }

    [Theory]
    [InlineData("SPX   250117P04950000", "SPX", "20250117", 4950.00, "P")]
    [InlineData("SPY   261218C00450500", "SPY", "20261218", 450.50, "C")]
    [InlineData("QQQ   250620C00375750", "QQQ", "20250620", 375.75, "C")]
    [InlineData("IWM   250919P00200000", "IWM", "20250919", 200.00, "P")]
    public void Parse_VariousSymbols_ParsesCorrectly(
        string occSymbol,
        string expectedUnderlying,
        string expectedExpiryYyyyMmDd,
        decimal expectedStrike,
        string expectedRight)
    {
        // Act
        OccSymbolParser.OccSymbolComponents components = OccSymbolParser.Parse(occSymbol);

        // Assert
        Assert.Equal(expectedUnderlying, components.Underlying);
        Assert.Equal(expectedExpiryYyyyMmDd, components.ExpiryYyyyMmDd);
        Assert.Equal(expectedStrike, components.Strike);
        Assert.Equal(expectedRight, components.Right);
    }
}
