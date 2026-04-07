# Skill: IBKR TWS API (.NET) — Pattern e Best Practice
> Aggiornabile dagli agenti. Controlla knowledge/errors-registry.md per fix noti.

---

> ⚠️ AGGIORNATO da TASK-02 in data 2026-04-05
> Motivo: Implementazione completa wrapper layer con connection management
> Fix: Aggiunto pattern completo per message processor thread, connection state, e safety validation

## NuGet Package

```xml
<PackageReference Include="IBApi" Version="10.19.2" />
```

Versione 10.19.2 testata e funzionante con .NET 8. Compatibile con TWS API 10.19 (2023).

## Setup Connessione — Pattern Completo

```csharp
// 1. Create wrapper (implements EWrapper interface)
TwsCallbackHandler wrapper = new(logger, OnConnectionStateChanged);

// 2. Create signal for message processing
EReaderSignal signal = new EReaderMonitorSignal();

// 3. Create client socket
EClientSocket client = new(wrapper, signal);

// 4. Connect (synchronous call)
// Port: 4001 = IB Gateway Paper | 4002 = IB Gateway Live (FORBIDDEN)
// Port: 7497 = TWS Paper        | 7496 = TWS Live (FORBIDDEN)
// ClientId deve essere UNICO per sessione (usa 1 per il servizio principale)
client.eConnect("127.0.0.1", 7497, clientId: 1, extraAuth: false);

// 5. Wait for connection with timeout
int timeoutMs = 10000;
int waited = 0;
while (!client.IsConnected() && waited < timeoutMs)
{
    await Task.Delay(100);
    waited += 100;
}
if (!client.IsConnected()) throw new TimeoutException("Connection timeout");

// 6. Start message processor thread (CRITICAL - callbacks won't fire without this)
EReader reader = new(client, signal);
reader.Start();

Thread messageThread = new(() =>
{
    while (client.IsConnected())
    {
        signal.waitForSignal(); // Blocks until message available
        reader.processMsgs();   // Process callbacks
    }
})
{
    IsBackground = true,
    Name = "IBKR-MessageProcessor"
};
messageThread.Start();
```

## Reconnection Pattern con Exponential Backoff

```csharp
public async Task ConnectAsync(CancellationToken ct)
{
    int attempt = 0;
    int delaySeconds = 5; // Initial delay
    int maxDelaySeconds = 300; // 5 minutes cap

    while (!ct.IsCancellationRequested)
    {
        attempt++;
        try
        {
            client.eConnect(host, port, clientId, false);
            
            // Wait for connection with timeout
            bool connected = await WaitForConnectionAsync(ct);
            if (!connected) throw new TimeoutException();

            // Start message processor
            StartMessageProcessorThread();
            
            logger.LogInformation("Connected after {Attempts} attempts", attempt);
            return; // Success
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Connection attempt {Attempt} failed", attempt);
            
            // Exponential backoff
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
            delaySeconds = Math.Min(delaySeconds * 2, maxDelaySeconds);
        }
    }
}
```

## Connection State Management

```csharp
// Track state separately from IsConnected() (socket status)
public enum ConnectionState
{
    Disconnected = 0, // Default safe state
    Connecting = 1,
    Connected = 2,
    Error = 3
}

// Update state based on callbacks
public void connectAck() // EWrapper callback
{
    State = ConnectionState.Connected;
    StateChanged?.Invoke(this, State);
}

public void connectionClosed() // EWrapper callback
{
    State = ConnectionState.Disconnected;
    StateChanged?.Invoke(this, State);
}

public void error(int id, int errorCode, string errorMsg, string json)
{
    // Connection lost
    if (errorCode is 1100 or 1300)
    {
        State = ConnectionState.Disconnected;
        TriggerReconnect();
    }
    // Connection restored
    if (errorCode is 1101 or 1102)
    {
        State = ConnectionState.Connected;
        TriggerReconciliation();
    }
}
```

## Verifica Connessione

```csharp
// reqCurrentTime() come keepalive — risposta in currentTime() callback
client.reqCurrentTime();
// Timeout: se non risponde in 10s → disconnessione
```

## Market Data — SPX Index

```csharp
Contract spxContract = new()
{
    Symbol   = "SPX",
    SecType  = "IND",
    Exchange = "CBOE",
    Currency = "USD"
};
// Tick generico: price data
client.reqMktData(reqId: 1001, spxContract, genericTickList: "", snapshot: false,
    regulatorySnapshot: false, mktDataOptions: null);
// Callback: tickPrice(reqId, tickType, price, attrib)
// TickType 4 = LAST price
```

## Market Data — VIX3M (attenzione: simbolo può variare)

```csharp
// NOTA: il simbolo VIX3M potrebbe non essere disponibile su tutti gli account paper.
// Alternativa: VXV (stesso prodotto, simbolo Bloomberg legacy)
// Verifica disponibilità con il tuo account prima di usarlo.
Contract vix3mContract = new()
{
    Symbol   = "VIX3M",   // provare anche "VXV" se VIX3M non funziona
    SecType  = "IND",
    Exchange = "CBOE",
    Currency = "USD"
};
client.reqMktData(reqId: 1003, vix3mContract, genericTickList: "45",
    snapshot: false, regulatorySnapshot: false, mktDataOptions: null);
// TickType 45 = LAST_TIMESTAMP (per staleness check)
// TickType 4  = LAST (valore corrente)
```

## Option Chain — reqContractDetails

```csharp
// Step 1: ottieni lista contratti disponibili
Contract optionSpec = new()
{
    Symbol      = "SPX",
    SecType     = "OPT",
    Exchange    = "SMART",
    Currency    = "USD",
    Right       = "P",           // Put
    Multiplier  = "100",
    // LastTradeDateOrContractMonth = "20251220"  // filtra per scadenza
};
client.reqContractDetails(reqId: 2000, optionSpec);
// Callback: contractDetails(reqId, ContractDetails details)
// contractDetailsEnd(reqId) → segnala fine lista
```

## Option Greeks — reqMktData su singola opzione

```csharp
Contract optContract = new()
{
    Symbol     = "SPX",
    SecType    = "OPT",
    Exchange   = "SMART",
    Currency   = "USD",
    Strike     = 4500,
    Right      = "P",
    LastTradeDateOrContractMonth = "20251220",
    Multiplier = "100"
};
client.reqMktData(reqId, optContract, genericTickList: "106", // 106 = Option Greeks
    snapshot: false, regulatorySnapshot: false, mktDataOptions: null);
// Callback: tickOptionComputation(reqId, tickType, impliedVolatility,
//           delta, optPrice, pvDividend, gamma, vega, theta, undPrice)
// TickType 10 = BID_OPTION, 11 = ASK_OPTION, 13 = LAST_OPTION
```

## WhatIf Margin Check

```csharp
Order whatIfOrder = new()
{
    Action      = "BUY",
    OrderType   = "MKT",
    TotalQuantity = 1,
    WhatIf      = true    // ← CRITICO: non invia ordine reale
};
client.placeOrder(orderId, contract, whatIfOrder);
// Callback: openOrder(orderId, contract, order, orderState)
// orderState.InitMarginChange → margine richiesto
// orderState.MaintMarginChange → margine di mantenimento
// orderState.EquityWithLoanValue → equity disponibile
```

## Invio Ordine Reale

```csharp
// SEMPRE: verifica TradingMode prima di placeOrder
if (_tradingMode == TradingMode.Paper)
{
    // Port 4001 (IB Gateway paper) → ordini vanno al paper account
}

Order limitOrder = new()
{
    Action        = "BUY",       // "BUY" | "SELL"
    OrderType     = "LMT",       // limite
    TotalQuantity = 5,
    LmtPrice      = 18.40m,
    Tif           = "GTC",       // "DAY" | "GTC"
    Transmit      = true         // false = staged order (non trasmesso)
};
int orderId = _nextOrderId++;
client.placeOrder(orderId, contract, limitOrder);
```

## Cancellazione Ordine

```csharp
client.cancelOrder(ibkrOrderId, manualCancelOrderTime: "");
// Callback: orderStatus con status="Cancelled"
```

## reqPositions (Reconciliation)

```csharp
client.reqPositions();
// Callback: position(account, contract, pos, avgCost) per ogni posizione
// positionEnd() → fine lista
```

## reqOpenOrders (Reconciliation)

```csharp
client.reqAllOpenOrders();
// Callback: openOrder() per ogni ordine aperto
// openOrderEnd() → fine lista
```

## reqExecutions (Fill history)

```csharp
ExecutionFilter filter = new() { ClientId = 1, Time = DateTime.Today.ToString("yyyyMMdd HH:mm:ss") };
client.reqExecutions(reqId: 9000, filter);
// Callback: execDetails(reqId, contract, execution)
// execDetailsEnd(reqId) → fine lista
```

## Pattern EWrapper (callback handler)

```csharp
public sealed class TwsWrapper : EWrapper
{
    // Implementa TUTTI i metodi dell'interfaccia EWrapper
    // (sono oltre 60 — usa IDE per generarli come stub)

    public void tickPrice(int reqId, int field, double price, TickAttrib attrib)
    {
        // field == 4 → LAST price
        // field == 1 → BID
        // field == 2 → ASK
        _marketDataHandler.OnTickPrice(reqId, field, (decimal)price);
    }

    public void error(int id, int errorCode, string errorMsg, string advancedOrderRejectJson)
    {
        // errorCode 2104,2106,2158 → informativi (market data farm)
        // errorCode 1100,1300 → connessione persa
        // errorCode 10147 → order not found (già cancellato)
        if (errorCode is 2104 or 2106 or 2158)
        {
            _logger.LogDebug("IBKR info {Code}: {Msg}", errorCode, errorMsg);
            return;
        }
        _logger.LogError("IBKR error {Code} for req {Id}: {Msg}", errorCode, id, errorMsg);
        _errorHandler.OnError(id, errorCode, errorMsg);
    }

    public void connectionClosed()
    {
        _logger.LogWarning("IBKR connection closed");
        _lifecycleManager.OnConnectionLost();
    }

    // Stub per metodi non usati (obbligatori per compilare)
    public void tickSize(int reqId, int field, decimal size) { }
    public void tickString(int reqId, int tickType, string value) { }
    // ... tutti gli altri
}
```

## Error Codes Importanti

| Code | Significato | Azione |
|---|---|---|
| 1100 | Connessione persa | Trigger reconnect |
| 1101 | Connessione ripristinata, dati da verificare | Trigger reconciliation |
| 1102 | Connessione ripristinata, dati preservati | Verify market data subs |
| 1300 | TWS disconnesso | Trigger reconnect |
| 2104 | Market data farm OK | Informativo, ignora |
| 2106 | HMDS data farm OK | Informativo, ignora |
| 2158 | Sec-def data farm OK | Informativo, ignora |
| 10147 | Order ID non trovato | Ordine già cancellato |
| 10148 | OrderId duplicato | DuplicateOrderException |
| 201 | Order rejected | Analizza motivo, no retry automatico |
