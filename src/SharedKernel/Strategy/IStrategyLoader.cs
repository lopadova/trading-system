namespace SharedKernel.Strategy;

using SharedKernel.Domain;

/// <summary>
/// Service for loading and parsing strategy definitions from JSON files.
/// Supports loading from both examples/ and private/ folders.
/// </summary>
public interface IStrategyLoader
{
    /// <summary>
    /// Loads a single strategy from a JSON file.
    /// </summary>
    /// <param name="filePath">Absolute path to the strategy JSON file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed strategy definition with validation applied.</returns>
    /// <exception cref="FileNotFoundException">If file does not exist.</exception>
    /// <exception cref="InvalidOperationException">If JSON parsing fails or validation fails.</exception>
    Task<StrategyDefinition> LoadStrategyAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Loads all strategies from a directory (recursively).
    /// Skips files that fail to parse or validate, logging warnings instead of throwing.
    /// </summary>
    /// <param name="directoryPath">Directory to scan for .json files.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of successfully loaded strategies.</returns>
    Task<IReadOnlyList<StrategyDefinition>> LoadAllStrategiesAsync(string directoryPath, CancellationToken ct = default);

    /// <summary>
    /// Loads all example strategies from the default examples/ folder.
    /// </summary>
    Task<IReadOnlyList<StrategyDefinition>> LoadExampleStrategiesAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads all private strategies from the default private/ folder.
    /// </summary>
    Task<IReadOnlyList<StrategyDefinition>> LoadPrivateStrategiesAsync(CancellationToken ct = default);
}
