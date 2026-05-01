using IBApi;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;

namespace TradingSupervisorService.Ibkr;

/// <summary>
/// IBKR TWS API client wrapper with connection management and retry logic.
/// Thread-safe. Implements exponential backoff for reconnection.
/// </summary>
public sealed class IbkrClient : IIbkrClient
{
    private readonly ILogger<IbkrClient> _logger;
    private readonly IbkrConfig _config;
    private readonly TwsCallbackHandler _wrapper;
    private readonly EClientSocket _client;
    private readonly EReaderSignal _signal;
    private readonly IbkrPortScanner _portScanner;

    private ConnectionState _state = ConnectionState.Disconnected;
    private Thread? _messageProcessorThread;
    private int _reconnectAttempts = 0;
    private bool _disposed = false;
    private bool _initialDiagnosticsRun = false;
    private volatile bool _shouldProcessMessages = false; // Control flag for message processor thread

    // RM-01: Order ID reservation with atomic increment
    private int _localNextOrderId = 0;
    private readonly object _orderIdLock = new();

    private readonly object _stateLock = new();

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    // Suppress unused event warnings - these are part of IIbkrClient interface
    // and will be used when order management features are implemented
#pragma warning disable CS0067
    public event EventHandler<(int OrderId, string Status, int Filled, int Remaining, double AvgFillPrice)>? OrderStatusChanged;
    public event EventHandler<(int OrderId, int ErrorCode, string ErrorMessage)>? OrderError;
#pragma warning restore CS0067

    public IbkrClient(ILogger<IbkrClient> logger, IbkrConfig config, TwsCallbackHandler wrapper, IbkrPortScanner portScanner)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
        _portScanner = portScanner ?? throw new ArgumentNullException(nameof(portScanner));

        // Validate configuration (throws if invalid)
        _config.Validate();

        // Initialize EClient components
        _signal = new EReaderMonitorSignal();
        _client = new EClientSocket(_wrapper, _signal);

        // RM-01: Subscribe to nextValidId for order ID initialization and reconnect handling
        _wrapper.NextValidIdReceived += OnNextValidIdReceived;

        _logger.LogInformation(
            "IbkrClient initialized. Host={Host} Port={Port} ClientId={ClientId} Mode={Mode}",
            _config.Host, _config.Port, _config.ClientId, _config.TradingMode);
    }

    public ConnectionState State
    {
        get { lock (_stateLock) return _state; }
        private set
        {
            lock (_stateLock)
            {
                if (_state == value) return;
                ConnectionState oldState = _state;
                _state = value;
                _logger.LogInformation("IBKR connection state: {OldState} -> {NewState}", oldState, _state);
                ConnectionStateChanged?.Invoke(this, _state);
            }
        }
    }

    public bool IsConnected => _client.IsConnected();

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(IbkrClient));
        }

        if (IsConnected)
        {
            _logger.LogDebug("Already connected to IBKR");
            return;
        }

        State = ConnectionState.Connecting;

        // Run initial diagnostics on first connection attempt
        if (!_initialDiagnosticsRun)
        {
            await RunConnectionDiagnosticsAsync(ct);
            _initialDiagnosticsRun = true;
        }

        int attempt = 0;
        int delaySeconds = _config.ReconnectInitialDelaySeconds;

        while (!ct.IsCancellationRequested)
        {
            attempt++;
            _reconnectAttempts++;

            _logger.LogInformation(
                "IBKR connection attempt {Attempt} to {Host}:{Port}",
                attempt, _config.Host, _config.Port);

            try
            {
                // eConnect is synchronous - opens socket and performs version handshake
                // Per TWS API docs: "after a connection has been established" = after eConnect() returns
                _client.eConnect(_config.Host, _config.Port, _config.ClientId, false);

                // CRITICAL: Create EReader AFTER eConnect() returns (connection established = handshake done)
                // Per official docs: "EReader object is not created until AFTER a connection has been established"
                // "established" = eConnect() completed version negotiation, NOT IsConnected() = true
                // IsConnected() becomes true only AFTER message processor receives first message from TWS
                StartMessageProcessorThread();

                // NOW wait for TWS to send confirmation (nextValidId or other callbacks)
                // The message processor must be running to receive these messages
                bool connected = await WaitForConnectionAsync(ct);
                if (!connected)
                {
                    _logger.LogWarning("IBKR connection timeout after {Timeout}s", _config.ConnectionTimeoutSeconds);
                    State = ConnectionState.Error;
                    throw new TimeoutException($"Connection timeout after {_config.ConnectionTimeoutSeconds}s");
                }

                State = ConnectionState.Connected;
                _reconnectAttempts = 0; // Reset on successful connection
                _logger.LogInformation("IBKR connected successfully");
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "IBKR connection attempt {Attempt} failed", attempt);
                State = ConnectionState.Error;

                // Cleanup: Stop message processor thread and disconnect socket before retry
                StopMessageProcessorThread();
                if (_client.IsConnected())
                {
                    _client.eDisconnect();
                }

                // Run diagnostics on first failure to help troubleshoot
                if (attempt == 1)
                {
                    _logger.LogWarning("First connection attempt failed. Running diagnostics...");
                    await RunConnectionDiagnosticsAsync(ct);
                }

                // Check max attempts
                if (_config.MaxReconnectAttempts > 0 && attempt >= _config.MaxReconnectAttempts)
                {
                    _logger.LogError("Max reconnect attempts ({Max}) reached. Giving up.", _config.MaxReconnectAttempts);
                    _logger.LogError("IBKR connection failed permanently. Check TWS/IB Gateway is running.");
                    throw;
                }

                // Exponential backoff
                _logger.LogInformation("Retrying in {Delay}s...", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);

                delaySeconds = Math.Min(delaySeconds * 2, _config.ReconnectMaxDelaySeconds);
            }
        }

        ct.ThrowIfCancellationRequested();
    }

    public Task DisconnectAsync()
    {
        if (!IsConnected)
        {
            _logger.LogDebug("Already disconnected from IBKR");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Disconnecting from IBKR");
        _client.eDisconnect();
        State = ConnectionState.Disconnected;

        // Stop message processor thread
        StopMessageProcessorThread();

        return Task.CompletedTask;
    }

    public void RequestCurrentTime()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to IBKR");
        }

        _logger.LogDebug("Requesting current time (keepalive)");
        _client.reqCurrentTime();
    }

    public void RequestMarketData(
        int requestId,
        string symbol,
        string secType,
        string exchange,
        string currency = "USD",
        string genericTickList = "",
        bool snapshot = false)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to IBKR");
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(secType))
        {
            throw new ArgumentException("SecType cannot be empty", nameof(secType));
        }

        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException("Exchange cannot be empty", nameof(exchange));
        }

        Contract contract = new()
        {
            Symbol = symbol,
            SecType = secType,
            Exchange = exchange,
            Currency = currency
        };

        _logger.LogInformation(
            "Requesting market data: reqId={ReqId} symbol={Symbol} secType={SecType} exchange={Exchange} snapshot={Snapshot}",
            requestId, symbol, secType, exchange, snapshot);

        _client.reqMktData(requestId, contract, genericTickList, snapshot, false, null);
    }

    public void CancelMarketData(int requestId)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to IBKR");
        }

        _logger.LogInformation("Canceling market data: reqId={ReqId}", requestId);
        _client.cancelMktData(requestId);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing IbkrClient");

        try
        {
            DisconnectAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect in Dispose");
        }

        StopMessageProcessorThread();

        _disposed = true;
    }

    #region Private Methods

    private async Task<bool> WaitForConnectionAsync(CancellationToken ct)
    {
        int waitedMs = 0;
        int timeoutMs = _config.ConnectionTimeoutSeconds * 1000;
        int pollIntervalMs = 100;

        while (waitedMs < timeoutMs && !ct.IsCancellationRequested)
        {
            if (_client.IsConnected())
            {
                return true;
            }

            await Task.Delay(pollIntervalMs, ct);
            waitedMs += pollIntervalMs;
        }

        return _client.IsConnected();
    }

    private void StartMessageProcessorThread()
    {
        if (_messageProcessorThread?.IsAlive == true)
        {
            _logger.LogWarning("Message processor thread already running");
            return;
        }

        _logger.LogDebug("Starting message processor thread");

        // CRITICAL: EReader must be created AFTER eConnect() returns (connection established)
        // Per official TWS API docs: "EReader object is not created until AFTER a connection has been established"
        // "established" = eConnect() completed socket open and version handshake
        EReader reader = new(_client, _signal);
        reader.Start();
        _logger.LogInformation("EReader created and started after eConnect()");

        // Signal thread to start processing
        _shouldProcessMessages = true;

        _messageProcessorThread = new Thread(() =>
        {
            _logger.LogInformation("▶ Message processor thread STARTED");

            try
            {
                // Use dedicated flag instead of IsConnected() to avoid chicken-egg problem:
                // IsConnected() becomes true only AFTER messages are processed,
                // but we need to process messages to make IsConnected() true!
                while (_shouldProcessMessages)
                {
                    _signal.waitForSignal();
                    reader.processMsgs();
                }
            }
            catch (Exception ex)
            {
                // This is expected when disconnecting (socket closed)
                if (_client.IsConnected())
                {
                    _logger.LogError(ex, "Message processor thread error while connected");
                }
                else
                {
                    _logger.LogDebug(ex, "Message processor thread stopped (disconnected)");
                }
            }

            _logger.LogInformation("■ Message processor thread EXITING");
        })
        {
            IsBackground = true,
            Name = "IBKR-MessageProcessor-Supervisor"
        };

        _messageProcessorThread.Start();
    }

    private void StopMessageProcessorThread()
    {
        if (_messageProcessorThread == null || !_messageProcessorThread.IsAlive)
        {
            return;
        }

        _logger.LogDebug("Stopping message processor thread");

        try
        {
            // Signal thread to stop processing
            _shouldProcessMessages = false;

            // Wake up thread if waiting
            _signal.issueSignal();

            // Wait for thread to finish (max 5 seconds)
            if (!_messageProcessorThread.Join(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("Message processor thread did not exit within timeout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping message processor thread");
        }

        _messageProcessorThread = null;
    }

    public bool PlaceOrder(int orderId, SharedKernel.Domain.OrderRequest request)
    {
        _logger.LogInformation("[STUB] PlaceOrder: orderId={OrderId}, symbol={Symbol}", orderId, request.Symbol);
        // TODO: Implement actual IBKR order placement in future tasks
        return false;
    }

    public void CancelOrder(int orderId)
    {
        _logger.LogInformation("[STUB] CancelOrder: orderId={OrderId}", orderId);
        // TODO: Implement actual IBKR order cancellation in future tasks
    }

    public void RequestOpenOrders()
    {
        _logger.LogInformation("[STUB] RequestOpenOrders");
        // TODO: Implement actual IBKR open orders request in future tasks
    }

    public void RequestPositions()
    {
        _logger.LogInformation("[STUB] RequestPositions");
        // TODO: Implement actual IBKR positions request in future tasks
    }

    public void RequestAccountSummary(int requestId)
    {
        // Phase 5: Supervisor market/account data P1 - Task RM-07

        if (!IsConnected)
        {
            throw new InvalidOperationException("Cannot request account summary: not connected to IBKR");
        }

        // Request all essential account value tags for risk management and safety checks
        const string tags =
            "NetLiquidation," +      // Total account value
            "TotalCashValue," +      // Cash balance
            "AvailableFunds," +      // Funds available for trading
            "ExcessLiquidity," +     // Excess liquidity
            "RealizedPnL," +         // Realized profit/loss
            "UnrealizedPnL";         // Unrealized profit/loss

        _logger.LogInformation(
            "Requesting account summary: reqId={RequestId} tags={Tags}",
            requestId, tags);

        // Request account summary for all accounts ("All") with specified tags
        // groupName="All" returns summary across all linked accounts
        _client.reqAccountSummary(requestId, "All", tags);
    }

    public void CancelAccountSummary(int requestId)
    {
        // Phase 5: Supervisor market/account data P1 - Task RM-07

        if (!IsConnected)
        {
            _logger.LogWarning(
                "Cannot cancel account summary reqId={RequestId}: not connected",
                requestId);
            return;
        }

        _logger.LogInformation(
            "Cancelling account summary: reqId={RequestId}",
            requestId);

        _client.cancelAccountSummary(requestId);
    }

    /// <summary>
    /// RM-01: Callback for nextValidId from IBKR. Updates local order ID counter with Math.Max
    /// to handle reconnect scenarios where IBKR might send a lower ID than we've already used.
    /// Thread-safe with lock to prevent race conditions with ReserveOrderId.
    /// </summary>
    private void OnNextValidIdReceived(object? sender, int ibkrOrderId)
    {
        lock (_orderIdLock)
        {
            int previousId = _localNextOrderId;
            // Use max to handle reconnect (don't go backwards)
            _localNextOrderId = Math.Max(_localNextOrderId, ibkrOrderId);

            _logger.LogInformation(
                "Order ID updated: previous={PreviousId} ibkr={IbkrId} current={CurrentId}",
                previousId, ibkrOrderId, _localNextOrderId);
        }
    }

    /// <summary>
    /// RM-01: Reserves the next available order ID and atomically increments the counter.
    /// Thread-safe. Ensures each call returns a unique ID, even in concurrent scenarios.
    /// CRITICAL: Must be called for EVERY order placement to prevent ID collisions.
    /// </summary>
    /// <returns>Reserved order ID for this order</returns>
    /// <exception cref="InvalidOperationException">If called before IBKR connection is established (nextValidId not yet received)</exception>
    public int ReserveOrderId()
    {
        lock (_orderIdLock)
        {
            if (_localNextOrderId == 0)
            {
                throw new InvalidOperationException(
                    "Cannot reserve order ID: IBKR connection not established (nextValidId not yet received). " +
                    "Ensure connection is active before placing orders.");
            }

            int reserved = _localNextOrderId;
            _localNextOrderId++; // Increment for next call

            _logger.LogDebug("Order ID reserved: {OrderId} (next will be {NextId})", reserved, _localNextOrderId);
            return reserved;
        }
    }

    /// <summary>
    /// Runs IBKR port diagnostics to check if configured port is available
    /// and suggests alternatives if not found.
    /// </summary>
    private async Task RunConnectionDiagnosticsAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("═══ IBKR Connection Diagnostics ═══");

            IbkrPortDiagnostics diagnostics = await _portScanner.DiagnoseConnectionAsync(
                _config.Host,
                _config.Port,
                _config.TradingMode.ToString());

            // Log summary
            _logger.LogInformation(diagnostics.GetSummary());

            // Log detailed findings based on status
            switch (diagnostics.Status)
            {
                case DiagnosticStatus.ConfiguredPortAvailable:
                    _logger.LogInformation("✓ Port check passed. Ready to connect.");
                    break;

                case DiagnosticStatus.ConfiguredPortClosed:
                    _logger.LogWarning("✗ Configured port {Port} is not responding", _config.Port);
                    if (diagnostics.AlternativePorts.Count > 0)
                    {
                        _logger.LogWarning("Alternative IBKR ports detected:");
                        foreach ((int port, string description) in diagnostics.AlternativePorts)
                        {
                            _logger.LogWarning("  • Port {Port}: {Description}", port, description);
                        }
                    }
                    if (!string.IsNullOrEmpty(diagnostics.Suggestion))
                    {
                        _logger.LogWarning("Suggestion: {Suggestion}", diagnostics.Suggestion);
                    }
                    break;

                case DiagnosticStatus.NoIbkrServicesFound:
                    _logger.LogError("✗ No IBKR services found on any standard port");
                    _logger.LogError("TWS or IB Gateway may not be running");
                    if (!string.IsNullOrEmpty(diagnostics.Suggestion))
                    {
                        _logger.LogError("Troubleshooting steps:");
                        foreach (string line in diagnostics.Suggestion.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                _logger.LogError("  {Line}", line.Trim());
                            }
                        }
                    }
                    break;
            }

            _logger.LogInformation("═══════════════════════════════════");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Port diagnostics failed (non-critical)");
        }
    }

    #endregion
}
