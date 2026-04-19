using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Ibkr;

/// <summary>
/// Utility for scanning IBKR standard ports and diagnosing connection issues.
/// Helps identify if TWS/IB Gateway is running and on which port.
/// </summary>
public sealed class IbkrPortScanner
{
    private readonly ILogger<IbkrPortScanner> _logger;

    // Standard IBKR ports
    private static readonly int[] StandardPorts = new[] { 4001, 4002, 7496, 7497 };
    private static readonly Dictionary<int, string> PortDescriptions = new()
    {
        { 4001, "IB Gateway Live (standard)" },
        { 4002, "IB Gateway Paper (standard)" },
        { 7496, "TWS Live (standard)" },
        { 7497, "TWS Paper (standard)" }
    };

    public IbkrPortScanner(ILogger<IbkrPortScanner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scans a specific port to check if it's open and potentially running IBKR.
    /// Uses TCP socket connection test without sending any data.
    /// </summary>
    /// <param name="host">Hostname or IP address</param>
    /// <param name="port">Port number to scan</param>
    /// <param name="timeoutMs">Connection timeout in milliseconds</param>
    /// <returns>True if port is open (something is listening)</returns>
    public async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs = 1000)
    {
        try
        {
            using TcpClient tcpClient = new();
            using CancellationTokenSource cts = new(timeoutMs);

            await tcpClient.ConnectAsync(host, port, cts.Token);

            // Port is open and accepting connections
            return tcpClient.Connected;
        }
        catch (SocketException)
        {
            // Port closed or unreachable
            return false;
        }
        catch (OperationCanceledException)
        {
            // Timeout - port not responding
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unexpected error scanning port {Host}:{Port}", host, port);
            return false;
        }
    }

    /// <summary>
    /// Scans all standard IBKR ports and returns which ones are open.
    /// This helps diagnose if TWS/IB Gateway is running on a non-configured port.
    /// </summary>
    /// <param name="host">Hostname or IP address</param>
    /// <param name="excludePort">Port to exclude from scan (usually the configured port)</param>
    /// <returns>List of open ports with descriptions</returns>
    public async Task<List<(int Port, string Description)>> ScanStandardPortsAsync(string host, int? excludePort = null)
    {
        List<(int Port, string Description)> openPorts = new();

        _logger.LogDebug("Scanning IBKR standard ports on {Host}...", host);

        // Scan all standard ports in parallel for speed
        List<Task<(int Port, bool IsOpen)>> scanTasks = StandardPorts
            .Where(p => p != excludePort)
            .Select(async port =>
            {
                bool isOpen = await IsPortOpenAsync(host, port, timeoutMs: 1500);
                return (Port: port, IsOpen: isOpen);
            })
            .ToList();

        (int Port, bool IsOpen)[] results = await Task.WhenAll(scanTasks);

        foreach ((int port, bool isOpen) in results)
        {
            if (isOpen)
            {
                string description = PortDescriptions.TryGetValue(port, out string? desc)
                    ? desc
                    : "Unknown IBKR service";

                openPorts.Add((port, description));
                _logger.LogInformation("Found open port: {Port} ({Description})", port, description);
            }
        }

        return openPorts;
    }

    /// <summary>
    /// Performs comprehensive diagnostics for IBKR connection issues.
    /// Checks configured port and suggests alternatives if unavailable.
    /// </summary>
    /// <param name="host">Configured hostname</param>
    /// <param name="configuredPort">Configured port number</param>
    /// <param name="tradingMode">Paper or Live mode (for better suggestions)</param>
    /// <returns>Diagnostic report with suggestions</returns>
    public async Task<IbkrPortDiagnostics> DiagnoseConnectionAsync(
        string host,
        int configuredPort,
        string tradingMode)
    {
        IbkrPortDiagnostics diagnostics = new()
        {
            ConfiguredHost = host,
            ConfiguredPort = configuredPort,
            ConfiguredPortDescription = PortDescriptions.TryGetValue(configuredPort, out string? desc)
                ? desc
                : "Non-standard port"
        };

        // Check configured port
        _logger.LogInformation(
            "Checking configured port {Host}:{Port} ({Description})...",
            host, configuredPort, diagnostics.ConfiguredPortDescription);

        diagnostics.ConfiguredPortIsOpen = await IsPortOpenAsync(host, configuredPort, timeoutMs: 2000);

        if (diagnostics.ConfiguredPortIsOpen)
        {
            _logger.LogInformation("✓ Configured port {Port} is open and accepting connections", configuredPort);
            diagnostics.Status = DiagnosticStatus.ConfiguredPortAvailable;
            return diagnostics;
        }

        // Configured port is closed - scan for alternatives
        _logger.LogWarning("✗ Configured port {Port} is closed or not responding", configuredPort);
        diagnostics.Status = DiagnosticStatus.ConfiguredPortClosed;

        _logger.LogInformation("Scanning standard IBKR ports for running instances...");
        diagnostics.AlternativePorts = await ScanStandardPortsAsync(host, excludePort: configuredPort);

        if (diagnostics.AlternativePorts.Count == 0)
        {
            _logger.LogWarning("✗ No IBKR services found on any standard port");
            diagnostics.Status = DiagnosticStatus.NoIbkrServicesFound;
            diagnostics.Suggestion = BuildNoServicesFoundSuggestion(tradingMode);
        }
        else
        {
            _logger.LogInformation("✓ Found {Count} alternative port(s) with IBKR services", diagnostics.AlternativePorts.Count);
            diagnostics.Status = DiagnosticStatus.AlternativePortsFound;
            diagnostics.Suggestion = BuildAlternativePortsSuggestion(diagnostics.AlternativePorts, tradingMode);
        }

        return diagnostics;
    }

    private static string BuildNoServicesFoundSuggestion(string tradingMode)
    {
        bool isPaper = tradingMode.Equals("paper", StringComparison.OrdinalIgnoreCase);
        int suggestedPort = isPaper ? 4002 : 4001;
        string suggestedApp = isPaper ? "IB Gateway (Paper)" : "IB Gateway (Live)";

        return $"No IBKR services detected on any standard port.\n" +
               $"Suggested actions:\n" +
               $"  1. Start {suggestedApp} on port {suggestedPort}\n" +
               $"  2. Check TWS/IB Gateway configuration (Configure > API Settings > Socket Port)\n" +
               $"  3. Ensure 'Enable ActiveX and Socket Clients' is checked\n" +
               $"  4. Add '127.0.0.1' to Trusted IP Addresses in API settings";
    }

    private static string BuildAlternativePortsSuggestion(
        List<(int Port, string Description)> alternativePorts,
        string tradingMode)
    {
        bool isPaper = tradingMode.Equals("paper", StringComparison.OrdinalIgnoreCase);
        string expectedMode = isPaper ? "Paper" : "Live";

        // Find best matching port for current mode
        (int Port, string Description)? bestMatch = alternativePorts
            .FirstOrDefault(p => p.Description.Contains(expectedMode, StringComparison.OrdinalIgnoreCase));

        if (bestMatch.HasValue)
        {
            return $"IBKR service found on alternative port {bestMatch.Value.Port} ({bestMatch.Value.Description}).\n" +
                   $"This matches your {expectedMode} trading mode.\n" +
                   $"Suggested action: Update configuration to use port {bestMatch.Value.Port}";
        }

        // No exact match - suggest first available
        (int firstPort, string firstDesc) = alternativePorts[0];
        return $"IBKR service found on port {firstPort} ({firstDesc}).\n" +
               $"Note: This may not match your {expectedMode} trading mode.\n" +
               $"Suggested actions:\n" +
               $"  1. If this is the correct instance, update configuration to port {firstPort}\n" +
               $"  2. Otherwise, start the correct {expectedMode} instance";
    }
}

/// <summary>
/// Diagnostic result from IBKR port scanning.
/// </summary>
public sealed class IbkrPortDiagnostics
{
    public required string ConfiguredHost { get; init; }
    public required int ConfiguredPort { get; init; }
    public required string ConfiguredPortDescription { get; init; }
    public bool ConfiguredPortIsOpen { get; set; }
    public DiagnosticStatus Status { get; set; }
    public List<(int Port, string Description)> AlternativePorts { get; set; } = new();
    public string? Suggestion { get; set; }

    public string GetSummary()
    {
        return Status switch
        {
            DiagnosticStatus.ConfiguredPortAvailable =>
                $"✓ IBKR available on {ConfiguredHost}:{ConfiguredPort} ({ConfiguredPortDescription})",

            DiagnosticStatus.ConfiguredPortClosed when AlternativePorts.Count == 0 =>
                $"✗ Port {ConfiguredPort} closed. No IBKR services found on standard ports.",

            DiagnosticStatus.AlternativePortsFound =>
                $"✗ Port {ConfiguredPort} closed. Found IBKR on: {string.Join(", ", AlternativePorts.Select(p => $"{p.Port} ({p.Description})"))}",

            _ => $"✗ Connection diagnostic failed"
        };
    }
}

public enum DiagnosticStatus
{
    ConfiguredPortAvailable,
    ConfiguredPortClosed,
    NoIbkrServicesFound,
    AlternativePortsFound
}
