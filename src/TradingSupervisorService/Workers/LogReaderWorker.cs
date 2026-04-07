using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using TradingSupervisorService.Repositories;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Background service that monitors OptionsExecutionService log files.
/// Reads log entries incrementally (tail -f style), detects ERROR/WARN/FATAL levels,
/// and creates alerts for critical events.
/// Tracks read position in log_reader_state table to survive restarts.
/// </summary>
public sealed class LogReaderWorker : BackgroundService
{
    private readonly ILogger<LogReaderWorker> _logger;
    private readonly ILogReaderStateRepository _stateRepo;
    private readonly IAlertRepository _alertRepo;
    private readonly string _logFilePath;
    private readonly int _intervalSeconds;

    // Regex pattern to detect Serilog text format log levels
    // Matches: "[2026-04-05 12:34:56 ERR]" or "[2026-04-05 12:34:56 WRN]" or "[2026-04-05 12:34:56 FTL]"
    private static readonly Regex LogLevelRegex = new(
        @"^\[(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+(?<level>ERR|WRN|FTL|INF|DBG)\]",
        RegexOptions.Compiled);

    public LogReaderWorker(
        ILogger<LogReaderWorker> logger,
        ILogReaderStateRepository stateRepo,
        IAlertRepository alertRepo,
        IConfiguration config)
    {
        _logger = logger;
        _stateRepo = stateRepo;
        _alertRepo = alertRepo;

        // Read configuration
        _logFilePath = config.GetValue<string>("LogReader:OptionsServiceLogPath")
            ?? "logs/options-execution-.log";  // Default path (will expand with date)
        _intervalSeconds = config.GetValue<int>("LogReader:IntervalSeconds", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Worker} started. LogPath={LogPath} Interval={Interval}s",
            nameof(LogReaderWorker), _logFilePath, _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("{Worker} stopped", nameof(LogReaderWorker));
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            // Check cancellation early - before starting work
            ct.ThrowIfCancellationRequested();

            // Get current log file path (Serilog uses daily rolling: options-execution-20260405.log)
            string currentLogFile = GetCurrentLogFilePath();

            // Check if log file exists
            if (!File.Exists(currentLogFile))
            {
                _logger.LogDebug("Log file does not exist yet: {LogFile}", currentLogFile);
                return;
            }

            // Get file info to check size and detect rotation
            FileInfo fileInfo = new(currentLogFile);
            long currentSize = fileInfo.Length;

            // Get last read position from database
            // Use CancellationToken.None for DB reads - we want these to complete even during shutdown
            LogReaderStateRecord? state = await _stateRepo.GetStateAsync(currentLogFile, CancellationToken.None);

            long startPosition;
            if (state == null)
            {
                // First time reading this file - start from beginning
                startPosition = 0;
                _logger.LogInformation("First read of log file {LogFile}, starting from beginning", currentLogFile);
            }
            else if (state.LastSize > currentSize)
            {
                // File was rotated or truncated - start from beginning
                startPosition = 0;
                _logger.LogWarning("Log file {LogFile} rotated or truncated (was {OldSize}, now {NewSize}), restarting from beginning",
                    currentLogFile, state.LastSize, currentSize);
            }
            else if (state.LastPosition > currentSize)
            {
                // Position is beyond current file size (shouldn't happen, but defensive)
                startPosition = 0;
                _logger.LogWarning("Read position {Position} exceeds file size {Size} for {LogFile}, restarting from beginning",
                    state.LastPosition, currentSize, currentLogFile);
            }
            else
            {
                // Normal case - resume from last position
                startPosition = state.LastPosition;
            }

            // If no new data, skip processing
            if (startPosition >= currentSize)
            {
                return;
            }

            // Read new log entries
            // Note: Pass ct to allow cancellation during file reading (non-critical operation)
            int linesProcessed = await ReadAndProcessLogEntriesAsync(currentLogFile, startPosition, currentSize, ct);

            // Update state in database
            // Use CancellationToken.None - state updates must complete to avoid re-processing lines
            await _stateRepo.UpsertStateAsync(new LogReaderStateRecord
            {
                FilePath = currentLogFile,
                LastPosition = currentSize,
                LastSize = currentSize,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            }, CancellationToken.None);

            if (linesProcessed > 0)
            {
                _logger.LogDebug("Processed {Count} log lines from {LogFile}", linesProcessed, currentLogFile);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown signal - do not log as error
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} cycle failed. Retry in {Interval}s",
                nameof(LogReaderWorker), _intervalSeconds);
            // Do NOT rethrow - worker must survive errors and retry on next cycle
        }
    }

    /// <summary>
    /// Reads log file from startPosition to end of file and processes each line.
    /// Detects error/warning/fatal levels and creates alerts.
    /// Returns number of lines processed.
    ///
    /// Note: endPosition parameter represents current file size for state tracking,
    /// not a read limit. We read all available lines from startPosition to EOF
    /// because StreamReader buffers data internally and tracking position per-line
    /// is unreliable (buffer reads jump file position in chunks, not line-by-line).
    /// </summary>
    private async Task<int> ReadAndProcessLogEntriesAsync(string filePath, long startPosition, long endPosition, CancellationToken ct)
    {
        int lineCount = 0;

        try
        {
            // Open file with FileShare.ReadWrite to allow Serilog to continue writing
            await using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(startPosition, SeekOrigin.Begin);

            using StreamReader reader = new(fs);

            // Read all lines from startPosition to end of stream
            // We cannot reliably check fs.Position during line reading because StreamReader
            // buffers data in chunks (1KB+), causing fs.Position to jump ahead of actual consumption
            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line == null)
                {
                    break;
                }

                lineCount++;

                // Process line for errors/warnings
                await ProcessLogLineAsync(line, filePath, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read log file {FilePath}", filePath);
            throw;
        }

        return lineCount;
    }

    /// <summary>
    /// Processes a single log line.
    /// Detects ERROR, WARNING, FATAL levels and creates alerts.
    /// </summary>
    private async Task ProcessLogLineAsync(string line, string sourceFile, CancellationToken ct)
    {
        // Parse log level from line using regex
        Match match = LogLevelRegex.Match(line);
        if (!match.Success)
        {
            // Not a log line (might be stack trace continuation or multiline message)
            return;
        }

        string level = match.Groups["level"].Value;
        string timestamp = match.Groups["timestamp"].Value;

        // Only create alerts for ERROR, WARNING, FATAL
        AlertSeverity? severity = level switch
        {
            "FTL" => AlertSeverity.Critical,
            "ERR" => AlertSeverity.Error,
            "WRN" => AlertSeverity.Warning,
            _ => null
        };

        if (!severity.HasValue)
        {
            // Info or Debug level - ignore
            return;
        }

        // Create alert for this log entry
        await CreateAlertFromLogLineAsync(line, severity.Value, timestamp, sourceFile, ct);
    }

    /// <summary>
    /// Creates an alert record for a detected error/warning in the log.
    /// Uses CancellationToken.None to ensure alert insertion completes even during shutdown.
    /// </summary>
    private async Task CreateAlertFromLogLineAsync(string logLine, AlertSeverity severity, string timestamp, string sourceFile, CancellationToken ct)
    {
        try
        {
            // Build alert message
            string message = ExtractLogMessage(logLine);

            // Create alert record
            AlertRecord alert = new()
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "LogError",
                Severity = severity.ToString().ToLowerInvariant(),
                Message = message,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    LogFile = sourceFile,
                    Timestamp = timestamp,
                    FullLine = logLine
                }),
                SourceService = "OptionsExecutionService",
                CreatedAt = DateTime.UtcNow.ToString("O"),
                ResolvedAt = null,
                ResolvedBy = null
            };

            // Use CancellationToken.None to ensure alert is persisted even during shutdown
            // Critical: alerts must not be lost when service stops
            await _alertRepo.InsertAsync(alert, CancellationToken.None);

            _logger.LogInformation("Created alert {AlertId} from log entry: severity={Severity} message={Message}",
                alert.AlertId, severity, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert from log line: {Line}", logLine);
            // Do not rethrow - continue processing other lines
        }
    }

    /// <summary>
    /// Extracts human-readable message from log line.
    /// Removes timestamp and log level prefix, returns message portion.
    /// </summary>
    private static string ExtractLogMessage(string logLine)
    {
        // Remove the "[timestamp LEVEL]" prefix
        Match match = LogLevelRegex.Match(logLine);
        if (!match.Success)
        {
            return logLine;
        }

        // Get text after the log level prefix
        int messageStart = match.Length;
        if (messageStart < logLine.Length)
        {
            string message = logLine[messageStart..].Trim();
            return string.IsNullOrWhiteSpace(message) ? logLine : message;
        }

        return logLine;
    }

    /// <summary>
    /// Gets the current log file path for today.
    /// Serilog uses daily rolling format: options-execution-20260405.log
    /// </summary>
    private string GetCurrentLogFilePath()
    {
        // If path already contains date pattern, use as-is
        if (_logFilePath.Contains("{Date}") || _logFilePath.Contains("yyyyMMdd"))
        {
            return _logFilePath.Replace("{Date}", DateTime.UtcNow.ToString("yyyyMMdd"));
        }

        // Expand Serilog rolling file pattern
        // Pattern: "logs/options-execution-.log" becomes "logs/options-execution-20260405.log"
        if (_logFilePath.Contains("-.log"))
        {
            return _logFilePath.Replace("-.log", $"-{DateTime.UtcNow:yyyyMMdd}.log");
        }

        // No rolling pattern detected - use path as-is
        return _logFilePath;
    }
}
