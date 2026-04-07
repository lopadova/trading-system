using IBApi;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using SharedKernel.Ibkr;

namespace OptionsExecutionService.Ibkr;

/// <summary>
/// IBKR TWS API client wrapper for OptionsExecutionService.
/// Handles connection management, order placement, and order status tracking.
/// Thread-safe with exponential backoff reconnection logic.
/// </summary>
public sealed class IbkrClient : IIbkrClient
{
    private readonly ILogger<IbkrClient> _logger;
    private readonly IbkrConfig _config;
    private readonly TwsCallbackHandler _wrapper;
    private readonly EClientSocket _client;
    private readonly EReaderSignal _signal;

    private ConnectionState _state = ConnectionState.Disconnected;
    private Thread? _messageProcessorThread;
    private int _reconnectAttempts = 0;
    private bool _disposed = false;

    private readonly object _stateLock = new();

    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<(int OrderId, string Status, int Filled, int Remaining, double AvgFillPrice)>? OrderStatusChanged;
    public event EventHandler<(int OrderId, int ErrorCode, string ErrorMessage)>? OrderError;

    public IbkrClient(ILogger<IbkrClient> logger, IbkrConfig config, TwsCallbackHandler wrapper)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));

        // Validate configuration (throws if invalid)
        _config.Validate();

        // Initialize EClient components
        _signal = new EReaderMonitorSignal();
        _client = new EClientSocket(_wrapper, _signal);

        // Wire up callback handler
        _wrapper.SetConnectionStateCallback(newState => State = newState);
        _wrapper.OrderStatusChanged += (sender, args) => OrderStatusChanged?.Invoke(this, args);
        _wrapper.OrderError += (sender, args) => OrderError?.Invoke(this, args);

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
                // eConnect is synchronous in IBApi
                _client.eConnect(_config.Host, _config.Port, _config.ClientId, false);

                // Wait for connection confirmation with timeout
                bool connected = await WaitForConnectionAsync(ct);
                if (!connected)
                {
                    _logger.LogWarning("IBKR connection timeout after {Timeout}s", _config.ConnectionTimeoutSeconds);
                    State = ConnectionState.Error;
                    throw new TimeoutException($"Connection timeout after {_config.ConnectionTimeoutSeconds}s");
                }

                // Start message processing thread
                StartMessageProcessorThread();

                State = ConnectionState.Connected;
                _reconnectAttempts = 0; // Reset on successful connection
                _logger.LogInformation("IBKR connected successfully");

                // Request next valid order ID
                _client.reqIds(-1);

                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "IBKR connection attempt {Attempt} failed", attempt);
                State = ConnectionState.Error;

                // Check max attempts
                if (_config.MaxReconnectAttempts > 0 && attempt >= _config.MaxReconnectAttempts)
                {
                    _logger.LogError("Max reconnect attempts ({Max}) reached. Giving up.", _config.MaxReconnectAttempts);
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

    public int GetNextOrderId()
    {
        return _wrapper.NextValidOrderId;
    }

    public bool PlaceOrder(int orderId, OrderRequest request)
    {
        if (!IsConnected)
        {
            _logger.LogError("Cannot place order: not connected to IBKR");
            return false;
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            // Create IBKR contract
            Contract contract = new()
            {
                Symbol = request.Symbol,
                SecType = request.SecurityType ?? "STK", // Default to stock if not specified
                Exchange = request.Exchange ?? "SMART",
                Currency = "USD"
            };

            // For options, add additional fields
            if (request.SecurityType == "OPT" && request.Strike.HasValue && !string.IsNullOrEmpty(request.Expiry))
            {
                contract.Strike = (double)request.Strike.Value;
                contract.LastTradeDateOrContractMonth = request.Expiry;
                contract.Right = request.OptionRight ?? "C"; // Default to call if not specified
            }

            // Create IBKR order
            Order order = new()
            {
                Action = request.Side == OrderSide.Buy ? "BUY" : "SELL",
                TotalQuantity = request.Quantity,
                OrderType = MapOrderType(request.Type),
                Account = request.Account ?? string.Empty
            };

            // Set limit price if applicable
            if (request.Type == SharedKernel.Domain.OrderType.Limit && request.LimitPrice.HasValue)
            {
                order.LmtPrice = (double)request.LimitPrice.Value;
            }

            _logger.LogInformation(
                "Placing order: orderId={OrderId} symbol={Symbol} side={Side} qty={Qty} type={Type} limitPrice={LimitPrice}",
                orderId, request.Symbol, request.Side, request.Quantity, request.Type, request.LimitPrice);

            _client.placeOrder(orderId, contract, order);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place order {OrderId}", orderId);
            return false;
        }
    }

    public void CancelOrder(int orderId)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to IBKR");
        }

        _logger.LogInformation("Canceling order: orderId={OrderId}", orderId);
        _client.cancelOrder(orderId, string.Empty);
    }

    public void RequestOpenOrders()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to IBKR");
        }

        _logger.LogDebug("Requesting open orders");
        _client.reqOpenOrders();
    }

    public void RequestPositions()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to IBKR");
        }

        _logger.LogDebug("Requesting positions");
        _client.reqPositions();
    }

    public void RequestAccountSummary(int requestId)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to IBKR");
        }

        _logger.LogDebug("Requesting account summary: reqId={ReqId}", requestId);
        _client.reqAccountSummary(requestId, "All", "TotalCashValue,NetLiquidation,BuyingPower");
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

        Contract contract = new()
        {
            Symbol = symbol,
            SecType = secType,
            Exchange = exchange,
            Currency = currency
        };

        _logger.LogInformation(
            "Requesting market data: reqId={ReqId} symbol={Symbol} secType={SecType} exchange={Exchange}",
            requestId, symbol, secType, exchange);

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

    private static string MapOrderType(SharedKernel.Domain.OrderType orderType)
    {
        return orderType switch
        {
            SharedKernel.Domain.OrderType.Market => "MKT",
            SharedKernel.Domain.OrderType.Limit => "LMT",
            _ => throw new ArgumentException($"Unsupported order type: {orderType}", nameof(orderType))
        };
    }

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

        EReader reader = new(_client, _signal);
        reader.Start();

        _messageProcessorThread = new Thread(() =>
        {
            _logger.LogDebug("Message processor thread started");

            try
            {
                while (_client.IsConnected())
                {
                    _signal.waitForSignal();
                    reader.processMsgs();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message processor thread error");
            }

            _logger.LogDebug("Message processor thread exiting");
        })
        {
            IsBackground = true,
            Name = "IBKR-MessageProcessor-Options"
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
            // Signal the thread to wake up and exit
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

    #endregion
}
