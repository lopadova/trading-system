# IBKR TWS API Integration

This directory contains the Interactive Brokers TWS API integration layer for the Trading Supervisor Service.

## Components

### `IbkrClient`
Main client wrapper that manages connection lifecycle and provides high-level API for IBKR operations.

**Features:**
- Automatic reconnection with exponential backoff
- Thread-safe connection state management
- Message processor thread for async callbacks
- Safety validations (paper trading only)

**Usage:**
```csharp
IbkrConfig config = new()
{
    Host = "127.0.0.1",
    Port = 7497, // TWS Paper
    ClientId = 1,
    TradingMode = TradingMode.Paper
};

TwsCallbackHandler wrapper = new(_logger, OnConnectionStateChanged);
IbkrClient client = new(_logger, config, wrapper);

await client.ConnectAsync(cancellationToken);
client.RequestMarketData(1001, "SPX", "IND", "CBOE");
```

### `TwsCallbackHandler`
Implementation of IBApi.EWrapper interface. Handles all IBKR callbacks (60+ methods).

**Key callbacks:**
- `connectAck()` - Connection established
- `connectionClosed()` - Connection lost
- `error()` - Error messages and codes
- `tickPrice()` - Market data price updates
- `currentTime()` - Server time (keepalive)
- `tickOptionComputation()` - Option Greeks

**Thread-safety:**
All callbacks are executed on the IBKR message processor thread. State changes are synchronized with locks.

### `IbkrConfig`
Immutable configuration record with validation.

**Safety rules:**
- Only ports 7497 (TWS Paper) and 4001 (IB Gateway Paper) allowed
- Only TradingMode.Paper allowed
- Validates all parameters in `Validate()` method

### `ConnectionState`
Enum representing connection lifecycle:
- `Disconnected` (0) - Initial state
- `Connecting` (1) - Connection attempt in progress
- `Connected` (2) - Successfully connected
- `Error` (3) - Connection error

## Ports Reference

| Port | Type | Trading Mode | Allowed |
|------|------|--------------|---------|
| 7497 | TWS | Paper | ✅ Yes |
| 7496 | TWS | Live | ❌ No |
| 4001 | IB Gateway | Paper | ✅ Yes |
| 4002 | IB Gateway | Live | ❌ No |

## Error Codes

| Code | Meaning | Action |
|------|---------|--------|
| 1100 | Connection lost | Triggers reconnection |
| 1101 | Connection restored (verify data) | Trigger reconciliation |
| 1102 | Connection restored (data preserved) | Verify subscriptions |
| 1300 | TWS disconnected | Triggers reconnection |
| 2104, 2106, 2158 | Informational (market data farm) | Logged as debug |
| 10147 | Order not found | Order already cancelled |
| 201 | Order rejected | Do not retry automatically |

## Reconnection Logic

**Exponential backoff:**
1. Initial delay: 5 seconds (configurable)
2. Max delay: 300 seconds / 5 minutes (configurable)
3. Delay doubles on each failure: 5s → 10s → 20s → 40s → 80s → 160s → 300s (capped)
4. Max attempts: Unlimited by default (configurable)

**Connection timeout:** 10 seconds (configurable)

## Threading Model

**Message Processor Thread:**
- Background thread started on connection
- Runs `EReader.processMsgs()` loop
- Processes IBKR callbacks asynchronously
- Stopped on disconnection

**Thread-safety:**
- All state changes protected by locks
- Connection state events raised on caller thread
- IBKR API calls are thread-safe (documented by IB)

## Testing

### Unit Tests
- `IbkrConfigTests` - Configuration validation and safety rules
- `ConnectionStateTests` - Enum values and defaults
- `TwsCallbackHandlerTests` - Callback handler behavior

### Integration Tests
**NOTE:** Integration tests require a running TWS/IB Gateway instance on paper trading account.

```bash
# Start TWS Paper (port 7497) or IB Gateway Paper (port 4001)
# Enable API connections in TWS settings
dotnet test --filter "FullyQualifiedName~Ibkr"
```

## Dependencies

- **IBApi** (10.19.2) - IBKR TWS API .NET wrapper
- **Microsoft.Extensions.Logging.Abstractions** - Logging interface

## Safety Features

1. **Port validation** - Rejects live trading ports at configuration level
2. **TradingMode enforcement** - Only Paper mode allowed
3. **Immutable config** - Configuration cannot change after creation
4. **Connection state tracking** - Always know connection status
5. **Error handling** - All IBKR errors logged with context
6. **Graceful shutdown** - Disconnects cleanly on disposal

## Future Enhancements (Not in T-02)

- Order placement and management
- Position tracking and reconciliation
- Contract details queries
- Historical data requests
- Account value monitoring
- Commission reports

---

**Implementation:** TASK-02 (IBKR API Wrapper Layer)  
**Status:** Complete  
**Last Updated:** 2026-04-05
