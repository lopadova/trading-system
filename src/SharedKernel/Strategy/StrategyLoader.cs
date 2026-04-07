namespace SharedKernel.Strategy;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;

/// <summary>
/// Loads and parses trading strategy definitions from JSON files.
/// Supports loading from examples/ and private/ directories.
/// Validates strategies after parsing.
/// </summary>
public sealed class StrategyLoader : IStrategyLoader
{
    private readonly IStrategyValidator _validator;
    private readonly ILogger<StrategyLoader> _logger;
    private readonly string _strategiesBasePath;

    // JSON serialization options configured for strategy files
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new TimeOnlyJsonConverter(),
            new DayOfWeekArrayJsonConverter()
        }
    };

    /// <summary>
    /// Creates a new StrategyLoader.
    /// </summary>
    /// <param name="validator">Validator for loaded strategies.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="strategiesBasePath">Base path to strategies folder. If null, uses "./strategies" relative to current directory.</param>
    public StrategyLoader(IStrategyValidator validator, ILogger<StrategyLoader> logger, string? strategiesBasePath = null)
    {
        _validator = validator;
        _logger = logger;
        _strategiesBasePath = strategiesBasePath ?? Path.Combine(Directory.GetCurrentDirectory(), "strategies");
    }

    public async Task<StrategyDefinition> LoadStrategyAsync(string filePath, CancellationToken ct = default)
    {
        // Validate file path
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Strategy file not found: {filePath}", filePath);
        }

        _logger.LogInformation("Loading strategy from {FilePath}", filePath);

        try
        {
            // Read JSON file
            string json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

            // Parse JSON to StrategyDefinition
            StrategyDefinition? strategy = JsonSerializer.Deserialize<StrategyDefinition>(json, JsonOptions);

            if (strategy == null)
            {
                throw new InvalidOperationException($"Failed to deserialize strategy from {filePath}: result is null");
            }

            // Set source file path
            strategy = strategy with { SourceFilePath = filePath };

            // Validate strategy
            ValidationResult validation = _validator.Validate(strategy);
            if (!validation.IsValid)
            {
                string errors = string.Join("; ", validation.Errors);
                throw new InvalidOperationException($"Strategy validation failed for {filePath}: {errors}");
            }

            _logger.LogInformation("Successfully loaded strategy '{StrategyName}' from {FilePath}",
                strategy.StrategyName, filePath);

            return strategy;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing failed for {FilePath}", filePath);
            throw new InvalidOperationException($"Failed to parse JSON from {filePath}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not FileNotFoundException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error loading strategy from {FilePath}", filePath);
            throw;
        }
    }

    public async Task<IReadOnlyList<StrategyDefinition>> LoadAllStrategiesAsync(string directoryPath, CancellationToken ct = default)
    {
        // Validate directory path
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory {DirectoryPath} does not exist, returning empty list", directoryPath);
            return Array.Empty<StrategyDefinition>();
        }

        _logger.LogInformation("Loading all strategies from {DirectoryPath}", directoryPath);

        // Find all JSON files recursively
        string[] jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

        if (jsonFiles.Length == 0)
        {
            _logger.LogInformation("No JSON files found in {DirectoryPath}", directoryPath);
            return Array.Empty<StrategyDefinition>();
        }

        _logger.LogInformation("Found {FileCount} JSON files in {DirectoryPath}", jsonFiles.Length, directoryPath);

        List<StrategyDefinition> strategies = new();

        // Load each file, skip failures
        foreach (string filePath in jsonFiles)
        {
            try
            {
                StrategyDefinition strategy = await LoadStrategyAsync(filePath, ct).ConfigureAwait(false);
                strategies.Add(strategy);
            }
            catch (Exception ex)
            {
                // Log error but continue processing other files
                _logger.LogWarning(ex, "Failed to load strategy from {FilePath}, skipping", filePath);
            }
        }

        _logger.LogInformation("Successfully loaded {SuccessCount} of {TotalCount} strategies from {DirectoryPath}",
            strategies.Count, jsonFiles.Length, directoryPath);

        return strategies;
    }

    public Task<IReadOnlyList<StrategyDefinition>> LoadExampleStrategiesAsync(CancellationToken ct = default)
    {
        string examplesPath = Path.Combine(_strategiesBasePath, "examples");
        _logger.LogDebug("Loading example strategies from {ExamplesPath}", examplesPath);
        return LoadAllStrategiesAsync(examplesPath, ct);
    }

    public Task<IReadOnlyList<StrategyDefinition>> LoadPrivateStrategiesAsync(CancellationToken ct = default)
    {
        string privatePath = Path.Combine(_strategiesBasePath, "private");
        _logger.LogDebug("Loading private strategies from {PrivatePath}", privatePath);
        return LoadAllStrategiesAsync(privatePath, ct);
    }
}

/// <summary>
/// JSON converter for TimeOnly (HH:mm:ss format).
/// </summary>
internal sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private const string TimeFormat = "HH:mm:ss";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("TimeOnly value cannot be null or empty");
        }

        if (!TimeOnly.TryParseExact(value, TimeFormat, out TimeOnly result))
        {
            throw new JsonException($"Invalid TimeOnly format: {value}. Expected format: {TimeFormat}");
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(TimeFormat));
    }
}

/// <summary>
/// JSON converter for DayOfWeek arrays (string format like "Monday", "Tuesday", etc.).
/// </summary>
internal sealed class DayOfWeekArrayJsonConverter : JsonConverter<DayOfWeek[]>
{
    public override DayOfWeek[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected array for DayOfWeek[]");
        }

        List<DayOfWeek> days = new();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return days.ToArray();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? dayString = reader.GetString();
                if (string.IsNullOrWhiteSpace(dayString))
                {
                    throw new JsonException("DayOfWeek value cannot be null or empty");
                }

                if (!Enum.TryParse<DayOfWeek>(dayString, ignoreCase: true, out DayOfWeek day))
                {
                    throw new JsonException($"Invalid DayOfWeek value: {dayString}");
                }

                days.Add(day);
            }
            else
            {
                throw new JsonException($"Expected string for DayOfWeek, got {reader.TokenType}");
            }
        }

        throw new JsonException("Unexpected end of JSON while reading DayOfWeek array");
    }

    public override void Write(Utf8JsonWriter writer, DayOfWeek[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (DayOfWeek day in value)
        {
            writer.WriteStringValue(day.ToString());
        }
        writer.WriteEndArray();
    }
}
