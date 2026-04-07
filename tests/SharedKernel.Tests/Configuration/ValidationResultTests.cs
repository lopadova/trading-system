using SharedKernel.Configuration;
using Xunit;

namespace SharedKernel.Tests.Configuration;

/// <summary>
/// Unit tests for ValidationResult.
/// Tests factory methods and immutability.
/// </summary>
public sealed class ValidationResultTests
{
    [Fact]
    public void Success_ReturnsValidResult()
    {
        // Act
        ValidationResult result = ValidationResult.Success();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.CriticalErrors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void SuccessWithWarnings_ReturnsValidResultWithWarnings()
    {
        // Arrange
        string[] warnings = { "Warning 1", "Warning 2" };

        // Act
        ValidationResult result = ValidationResult.SuccessWithWarnings(warnings);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.CriticalErrors);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains("Warning 1", result.Warnings);
        Assert.Contains("Warning 2", result.Warnings);
    }

    [Fact]
    public void Failure_WithErrors_ReturnsInvalidResult()
    {
        // Arrange
        string[] errors = { "Error 1", "Error 2" };

        // Act
        ValidationResult result = ValidationResult.Failure(errors);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.CriticalErrors.Count);
        Assert.Contains("Error 1", result.CriticalErrors);
        Assert.Contains("Error 2", result.CriticalErrors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Failure_WithErrorsAndWarnings_ReturnsInvalidResult()
    {
        // Arrange
        string[] errors = { "Error 1" };
        string[] warnings = { "Warning 1" };

        // Act
        ValidationResult result = ValidationResult.Failure(errors, warnings);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.CriticalErrors);
        Assert.Contains("Error 1", result.CriticalErrors);
        Assert.Single(result.Warnings);
        Assert.Contains("Warning 1", result.Warnings);
    }

    [Fact]
    public void ValidationResult_IsImmutable()
    {
        // Arrange
        ValidationResult result = ValidationResult.Success();

        // Assert - collections are read-only
        Assert.IsAssignableFrom<IReadOnlyList<string>>(result.CriticalErrors);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Warnings);
    }
}
