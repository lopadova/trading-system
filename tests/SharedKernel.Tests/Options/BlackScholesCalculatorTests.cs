using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Options;
using Xunit;

namespace SharedKernel.Tests.Options;

/// <summary>
/// Tests for Black-Scholes Greeks calculator.
/// Uses known values from option pricing literature for validation.
/// </summary>
public sealed class BlackScholesCalculatorTests
{
    private readonly IGreeksCalculator _calculator;
    private readonly Mock<ILogger<BlackScholesCalculator>> _loggerMock;

    public BlackScholesCalculatorTests()
    {
        _loggerMock = new Mock<ILogger<BlackScholesCalculator>>();
        _calculator = new BlackScholesCalculator(_loggerMock.Object);
    }

    [Fact]
    [Trait("TestId", "TEST-14-01")]
    public void Calculate_AtTheMoneyCall_ReturnsExpectedDelta()
    {
        // Arrange: ATM call should have delta around 0.5
        double S = 100.0;  // underlying price
        double K = 100.0;  // strike price (ATM)
        double T = 0.25;   // 3 months to expiry
        double r = 0.05;   // 5% risk-free rate
        double vol = 0.20; // 20% volatility

        // Act
        GreeksData result = _calculator.Calculate(S, K, T, r, vol, isCall: true);

        // Assert
        // ATM call delta should be around 0.5 (between 0.4 and 0.6)
        Assert.InRange(result.Delta, 0.4, 0.6);
        Assert.True(result.Gamma > 0, "Gamma should be positive");
        Assert.True(result.Vega > 0, "Vega should be positive");
        Assert.True(result.Theta < 0, "Theta should be negative (time decay)");
        Assert.Equal(S, result.UnderlyingPrice);
        Assert.Null(result.ImpliedVolatility); // not using IV in this test
    }

    [Fact]
    [Trait("TestId", "TEST-14-02")]
    public void Calculate_AtTheMoneyPut_ReturnsExpectedDelta()
    {
        // Arrange: ATM put should have delta around -0.5
        double S = 100.0;
        double K = 100.0;
        double T = 0.25;
        double r = 0.05;
        double vol = 0.20;

        // Act
        GreeksData result = _calculator.Calculate(S, K, T, r, vol, isCall: false);

        // Assert
        // ATM put delta should be around -0.5 (between -0.6 and -0.4)
        Assert.InRange(result.Delta, -0.6, -0.4);
        Assert.True(result.Gamma > 0, "Gamma should be positive");
        Assert.True(result.Vega > 0, "Vega should be positive");
        Assert.True(result.Theta < 0, "Theta should be negative (time decay)");
    }

    [Fact]
    [Trait("TestId", "TEST-14-03")]
    public void Calculate_DeepInTheMoneyCall_ReturnsHighDelta()
    {
        // Arrange: Deep ITM call should have delta close to 1.0
        double S = 120.0;  // underlying 20% above strike
        double K = 100.0;
        double T = 0.25;
        double r = 0.05;
        double vol = 0.20;

        // Act
        GreeksData result = _calculator.Calculate(S, K, T, r, vol, isCall: true);

        // Assert
        // Deep ITM call delta should be > 0.8
        Assert.InRange(result.Delta, 0.8, 1.0);
        Assert.True(result.Gamma >= 0, "Gamma should be non-negative");
    }

    [Fact]
    [Trait("TestId", "TEST-14-04")]
    public void Calculate_DeepOutOfTheMoneyPut_ReturnsLowDelta()
    {
        // Arrange: Deep OTM put (underlying well above strike) should have delta close to 0
        double S = 120.0;
        double K = 100.0;
        double T = 0.25;
        double r = 0.05;
        double vol = 0.20;

        // Act
        GreeksData result = _calculator.Calculate(S, K, T, r, vol, isCall: false);

        // Assert
        // Deep OTM put delta should be close to 0 (between -0.2 and 0)
        Assert.InRange(result.Delta, -0.2, 0.0);
    }

    [Fact]
    [Trait("TestId", "TEST-14-05")]
    public void Calculate_WithImpliedVolatility_UsesIVInsteadOfHistorical()
    {
        // Arrange
        double S = 100.0;
        double K = 100.0;
        double T = 0.25;
        double r = 0.05;
        double historicalVol = 0.15; // 15% historical
        double impliedVol = 0.30;    // 30% implied (much higher)

        // Act
        GreeksData withoutIV = _calculator.Calculate(S, K, T, r, historicalVol, isCall: true);
        GreeksData withIV = _calculator.Calculate(S, K, T, r, historicalVol, isCall: true, impliedVolatility: impliedVol);

        // Assert
        // Higher volatility should result in higher Vega and Gamma
        Assert.True(withIV.Vega > withoutIV.Vega, "IV calculation should have higher Vega");
        Assert.True(withIV.Gamma > withoutIV.Gamma, "IV calculation should have higher Gamma");
        Assert.Equal(impliedVol, withIV.ImpliedVolatility);
        Assert.Null(withoutIV.ImpliedVolatility);
    }

    [Fact]
    [Trait("TestId", "TEST-14-06")]
    public void Calculate_PutCallParity_GammaAndVegaMatch()
    {
        // Arrange: Call and Put with same parameters should have identical Gamma and Vega
        double S = 100.0;
        double K = 100.0;
        double T = 0.25;
        double r = 0.05;
        double vol = 0.20;

        // Act
        GreeksData call = _calculator.Calculate(S, K, T, r, vol, isCall: true);
        GreeksData put = _calculator.Calculate(S, K, T, r, vol, isCall: false);

        // Assert
        // Gamma and Vega should be identical for call and put (within floating point tolerance)
        Assert.Equal(call.Gamma, put.Gamma, precision: 6);
        Assert.Equal(call.Vega, put.Vega, precision: 6);

        // Delta should differ by 1.0 (put-call parity)
        Assert.Equal(1.0, call.Delta - put.Delta, precision: 4);
    }

    [Fact]
    [Trait("TestId", "TEST-14-07")]
    public void Calculate_ShortTimeToExpiry_HighGamma()
    {
        // Arrange: Options near expiry have high gamma (rapid delta changes)
        double S = 100.0;
        double K = 100.0;
        double T_long = 1.0;   // 1 year
        double T_short = 0.02; // ~1 week
        double r = 0.05;
        double vol = 0.20;

        // Act
        GreeksData longExpiry = _calculator.Calculate(S, K, T_long, r, vol, isCall: true);
        GreeksData shortExpiry = _calculator.Calculate(S, K, T_short, r, vol, isCall: true);

        // Assert
        // Short expiry ATM options have higher gamma
        Assert.True(shortExpiry.Gamma > longExpiry.Gamma, "Short expiry should have higher gamma");

        // Short expiry also has higher (more negative) theta
        Assert.True(shortExpiry.Theta < longExpiry.Theta, "Short expiry should have more negative theta");
    }

    [Fact]
    [Trait("TestId", "TEST-14-08")]
    public void Calculate_InvalidInputs_ReturnsEmptyGreeks()
    {
        // Arrange: invalid inputs should return empty Greeks, not throw
        double validPrice = 100.0;
        double validTime = 0.25;
        double validRate = 0.05;
        double validVol = 0.20;

        // Act & Assert: negative underlying price
        GreeksData result1 = _calculator.Calculate(-10, validPrice, validTime, validRate, validVol, true);
        Assert.Equal(GreeksData.Empty.Delta, result1.Delta);

        // Negative strike
        GreeksData result2 = _calculator.Calculate(validPrice, -10, validTime, validRate, validVol, true);
        Assert.Equal(GreeksData.Empty.Delta, result2.Delta);

        // Zero or negative time to expiry
        GreeksData result3 = _calculator.Calculate(validPrice, validPrice, 0, validRate, validVol, true);
        Assert.Equal(GreeksData.Empty.Delta, result3.Delta);

        GreeksData result4 = _calculator.Calculate(validPrice, validPrice, -0.1, validRate, validVol, true);
        Assert.Equal(GreeksData.Empty.Delta, result4.Delta);

        // Zero or negative volatility
        GreeksData result5 = _calculator.Calculate(validPrice, validPrice, validTime, validRate, 0, true);
        Assert.Equal(GreeksData.Empty.Delta, result5.Delta);

        GreeksData result6 = _calculator.Calculate(validPrice, validPrice, validTime, validRate, -0.1, true);
        Assert.Equal(GreeksData.Empty.Delta, result6.Delta);
    }

    [Fact]
    [Trait("TestId", "TEST-14-09")]
    public void Calculate_KnownValues_MatchesExpectedResults()
    {
        // Arrange: Use a known example from option pricing literature
        // Example: Hull's "Options, Futures, and Other Derivatives"
        // S=42, K=40, r=0.10, vol=0.20, T=0.5 (6 months)
        double S = 42.0;
        double K = 40.0;
        double T = 0.5;
        double r = 0.10;
        double vol = 0.20;

        // Act
        GreeksData call = _calculator.Calculate(S, K, T, r, vol, isCall: true);

        // Assert
        // Known values (approximate, from Black-Scholes formula):
        // Call price ≈ 4.76, Delta ≈ 0.7693, Gamma ≈ 0.0652, Vega ≈ 5.47, Theta ≈ -5.71 (per year)
        Assert.InRange(call.Delta, 0.75, 0.80);    // Delta around 0.7693
        Assert.InRange(call.Gamma, 0.06, 0.07);    // Gamma around 0.0652
        Assert.InRange(call.Vega, 5.0, 6.0);       // Vega around 5.47
        Assert.True(call.Theta < 0);               // Theta negative (time decay)
        Assert.InRange(call.Theta, -0.02, -0.01);  // Theta per day ≈ -5.71/365 ≈ -0.0156
    }

    [Fact]
    [Trait("TestId", "TEST-14-10")]
    public void Calculate_HighVolatility_IncreasesPremiumAndVega()
    {
        // Arrange
        double S = 100.0;
        double K = 100.0;
        double T = 0.25;
        double r = 0.05;
        double lowVol = 0.10;  // 10%
        double highVol = 0.40; // 40%

        // Act
        GreeksData lowVolGreeks = _calculator.Calculate(S, K, T, r, lowVol, isCall: true);
        GreeksData highVolGreeks = _calculator.Calculate(S, K, T, r, highVol, isCall: true);

        // Assert
        // Higher volatility → higher Vega, higher Gamma
        Assert.True(highVolGreeks.Vega > lowVolGreeks.Vega, "High vol should have higher Vega");
        Assert.True(highVolGreeks.Gamma > lowVolGreeks.Gamma, "High vol should have higher Gamma");

        // Delta for ATM should remain close to 0.5 regardless of vol
        Assert.InRange(lowVolGreeks.Delta, 0.4, 0.6);
        Assert.InRange(highVolGreeks.Delta, 0.4, 0.6);
    }

    [Fact]
    [Trait("TestId", "TEST-14-11")]
    public void GreeksData_Empty_ReturnsZeroValues()
    {
        // Act
        GreeksData empty = GreeksData.Empty;

        // Assert
        Assert.Equal(0, empty.Delta);
        Assert.Equal(0, empty.Gamma);
        Assert.Equal(0, empty.Theta);
        Assert.Equal(0, empty.Vega);
        Assert.Null(empty.ImpliedVolatility);
        Assert.Equal(0, empty.UnderlyingPrice);
    }

    [Fact]
    [Trait("TestId", "TEST-14-12")]
    public void Calculate_DeltaRange_CallBetween0And1()
    {
        // Arrange: Test various strike prices for calls
        double S = 100.0;
        double r = 0.05;
        double vol = 0.20;
        double T = 0.25;

        // Act & Assert
        // Deep OTM call (K >> S)
        GreeksData otm = _calculator.Calculate(S, 130.0, T, r, vol, isCall: true);
        Assert.InRange(otm.Delta, 0.0, 0.3);

        // ATM call
        GreeksData atm = _calculator.Calculate(S, 100.0, T, r, vol, isCall: true);
        Assert.InRange(atm.Delta, 0.4, 0.6);

        // Deep ITM call (K << S)
        GreeksData itm = _calculator.Calculate(S, 70.0, T, r, vol, isCall: true);
        Assert.InRange(itm.Delta, 0.9, 1.0);
    }

    [Fact]
    [Trait("TestId", "TEST-14-13")]
    public void Calculate_DeltaRange_PutBetweenNegative1And0()
    {
        // Arrange: Test various strike prices for puts
        double S = 100.0;
        double r = 0.05;
        double vol = 0.20;
        double T = 0.25;

        // Act & Assert
        // Deep OTM put (K << S)
        GreeksData otm = _calculator.Calculate(S, 70.0, T, r, vol, isCall: false);
        Assert.InRange(otm.Delta, -0.2, 0.0);

        // ATM put
        GreeksData atm = _calculator.Calculate(S, 100.0, T, r, vol, isCall: false);
        Assert.InRange(atm.Delta, -0.6, -0.4);

        // Deep ITM put (K >> S)
        GreeksData itm = _calculator.Calculate(S, 130.0, T, r, vol, isCall: false);
        Assert.InRange(itm.Delta, -1.0, -0.85);
    }
}
