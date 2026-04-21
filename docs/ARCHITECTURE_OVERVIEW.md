---
title: "Trading System - Architecture Overview"
tags: ["architecture", "reference", "onboarding"]
aliases: ["Architecture Overview", "System Overview"]
status: current
audience: ["developer", "new-user", "reviewer"]
last-reviewed: "2026-04-21"
related:
  - "[[Trading System Architecture|ARCHITECTURE]]"
  - "[[Trading System - Developer Onboarding|ONBOARDING]]"
  - "[[Trading System - Deployment Guide|DEPLOYMENT_GUIDE]]"
  - "[[MARKET_DATA_PIPELINE]]"
---

# Trading System - Architecture Overview

**Last Updated**: 2026-04-07  
**Version**: 1.0.0

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Cloudflare Edge                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐         ┌──────────────┐                 │
│  │   Dashboard  │         │    Worker    │                 │
│  │   (Pages)    │────────▶│   (Hono)     │                 │
│  └──────────────┘         └──────┬───────┘                 │
│                                  │                          │
│                           ┌──────▼───────┐                  │
│                           │   D1 (SQL)   │                  │
│                           └──────────────┘                  │
└─────────────────────────────────────────────────────────────┘
                                │
                                │ HTTPS
                                ▼
┌─────────────────────────────────────────────────────────────┐
│                    Windows Server                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────────────────┐    ┌──────────────────────┐    │
│  │TradingSupervisorService│◀──▶│OptionsExecutionService│   │
│  │  - Health monitoring   │    │  - Campaign mgmt     │    │
│  │  - IBKR connection     │    │  - Order execution   │    │
│  │  - Metrics collection  │    │  - Strategy engine   │    │
│  │  - Alert routing       │    │  - Position tracking │    │
│  └───────┬────────────────┘    └────────┬─────────────┘    │
│          │                              │                   │
│  ┌───────▼──────────┐          ┌────────▼─────────┐        │
│  │  supervisor.db   │          │   options.db     │        │
│  │  (SQLite WAL)    │          │   (SQLite WAL)   │        │
│  └──────────────────┘          └──────────────────┘        │
│                                                             │
│                      ┌─────────────────┐                    │
│                      │   IBKR TWS      │                    │
│                      │   (Socket API)  │                    │
│                      └─────────────────┘                    │
└─────────────────────────────────────────────────────────────┘
```

---

## Components

### 1. Windows Services (.NET 10)

#### TradingSupervisorService

**Purpose**: System monitoring and health checks

**Responsibilities**:
- Monitor IBKR connection state
- Collect system metrics (CPU, RAM, disk)
- Track service heartbeats
- Route alerts to Telegram
- Sync events to Cloudflare via outbox pattern
- Log reader for OptionsExecutionService logs
- IV term structure monitoring

**Database**: `supervisor.db` (SQLite)

Tables:
- `heartbeats`: Service health status
- `outbox_events`: Event log for sync to cloud
- `alerts`: System alerts and notifications
- `log_reader_state`: File position tracking for log parsing
- `ivts_snapshots`: Implied volatility term structure data
- `positions`: Shared position tracking with OptionsExecutionService

**Workers** (BackgroundService):
- `HeartbeatWorker`: Metrics collection (every 60s)
- `OutboxSyncWorker`: Cloud sync (every 30s)
- `TelegramWorker`: Alert delivery (every 10s)
- `LogReaderWorker`: Log parsing (every 120s)
- `IvtsMonitorWorker`: IV snapshot (every 300s)
- `GreeksMonitorWorker`: Options greeks tracking (every 300s)

#### OptionsExecutionService

**Purpose**: Execute options trading strategies

**Responsibilities**:
- Campaign lifecycle management (activate, monitor, close)
- Order submission and tracking
- Position monitoring and P&L calculation
- Circuit breaker and risk management
- Strategy execution engine
- IBKR API integration

**Database**: `options.db` (SQLite)

Tables:
- `campaigns`: Active and historical strategy campaigns
- `orders`: Order history and status
- `fills`: Trade executions
- `positions`: Current and historical positions
- `strategies`: Strategy definitions (SDF format)

**Workers**:
- `PositionMonitorWorker`: Track open positions
- `CampaignSupervisorWorker`: Monitor active campaigns
- `OrderStatusPollerWorker`: Update order states from IBKR

---

### 2. Cloudflare Worker (TypeScript + Hono)

**Purpose**: Serverless API and bot platform

**Features**:
- **Bot API**: Telegram + Discord command handling
- **Strategy Converter**: EasyLanguage → SDF (via Claude API)
- **Whitelist Management**: Authorized users only
- **Command Logging**: Audit trail in D1

**Routes**:
```
POST /bot/telegram     - Telegram webhook
POST /bot/discord      - Discord interactions
POST /strategies/convert - EL → SDF conversion
GET  /health           - Health check
```

**D1 Database** (Cloudflare):
- `bot_whitelist`: Authorized Telegram/Discord users
- `el_conversion_log`: EL conversion audit trail
- `bot_commands_log`: Bot command history

**Auth**:
- Telegram: HMAC signature verification
- Discord: Ed25519 signature verification
- Whitelist: `user_id` + `platform` check

---

### 3. Dashboard (React + TypeScript)

**Purpose**: Web UI for strategy management

**Features**:
- **Strategy Wizard**: 10-step SDF builder
  - Legs configuration (spreads, butterflies, etc.)
  - Entry/exit rules
  - Risk management
  - Position sizing
- **EL Converter**: Paste EasyLanguage, get SDF
- **Campaign Manager**: View active campaigns (future)
- **System Monitor**: Service health (future)

**Stack**:
- React 19 + TypeScript
- Vite build
- Zustand state management
- TanStack Query for API calls
- Tailwind CSS v4

---

## Data Flow

### Strategy Execution Flow

```
1. User creates strategy via Dashboard
   └─▶ Strategy Wizard (10 steps)
       └─▶ SDF JSON exported

2. User uploads SDF to OptionsExecutionService
   └─▶ POST /api/strategies (future API)
       └─▶ Validated against SDF schema
           └─▶ Saved to options.db

3. User activates campaign
   └─▶ POST /api/campaigns/activate
       └─▶ Campaign entity created (State: Active)
           └─▶ CampaignSupervisorWorker picks it up
               └─▶ Entry rules evaluated
                   └─▶ OrderPlacer submits orders to IBKR
                       └─▶ Order tracking in orders table
                           └─▶ Fills stored, positions updated
                               └─▶ P&L calculated
                                   └─▶ Exit rules evaluated
                                       └─▶ Campaign closed when conditions met
```

### Alert Flow

```
1. OptionsExecutionService detects error
   └─▶ AlertPublisher.PublishAsync(alert)
       └─▶ Alert inserted into supervisor.db (via shared repository)

2. TelegramWorker (TradingSupervisorService)
   └─▶ Polls alerts (every 10s)
       └─▶ GetUnresolvedAsync(limit: 10)
           └─▶ Sends via TelegramAlerter.SendImmediateAsync()
               └─▶ Marks alert as resolved
```

### Outbox Sync Flow

```
1. Service emits domain event
   └─▶ OutboxRepository.InsertAsync(event)
       └─▶ Event stored in outbox_events (Status: pending)

2. OutboxSyncWorker (TradingSupervisorService)
   └─▶ Polls pending events (every 30s)
       └─▶ GetPendingAsync(limit: 10)
           └─▶ HTTP POST to Cloudflare Worker
               └─▶ Worker stores in D1
                   └─▶ Event status → sent
```

---

## Technology Stack

### Backend (.NET)

- **.NET 10** (C# 13.0)
- **Dapper** (SQL mapping)
- **SQLite** (persistent storage, WAL mode)
- **IBApi** (IBKR TWS connection)
- **BackgroundService** (worker pattern)

### Frontend (Dashboard)

- **React 19**
- **TypeScript 5.7** (strict mode)
- **Vite 6** (bundler)
- **Zustand** (state management)
- **TanStack Query** (data fetching)
- **Tailwind CSS v4**

### Cloudflare

- **Hono** (web framework)
- **D1** (SQLite at edge)
- **Workers** (serverless compute)
- **Pages** (static hosting for dashboard)

### Testing

- **xUnit** (.NET tests)
- **Vitest** (TypeScript tests)
- **Moq** (mocking framework)

---

## Design Patterns

### Repository Pattern (Domain-Driven)

```csharp
// Anti-pattern (generic CRUD)
public interface IRepository<T> {
    Task InsertAsync(T entity);
    Task<T?> GetByIdAsync(string id);
}

// Pattern (domain-driven)
public interface ICampaignRepository {
    Task SaveCampaignAsync(Campaign campaign, CancellationToken ct);
    Task<Campaign?> GetCampaignAsync(string campaignId, CancellationToken ct);
    Task<IReadOnlyList<Campaign>> GetCampaignsByStateAsync(
        CampaignState state, CancellationToken ct);
}
```

**Benefits**:
- Explicit intent: `SaveCampaignAsync` vs generic `InsertAsync`
- Type safety: `CampaignState.Active` vs string "active"
- Domain encapsulation: State changes via domain methods (`campaign.Activate()`)

### Outbox Pattern (Event Sync)

**Problem**: Sync domain events to cloud without distributed transactions

**Solution**:
1. Insert event into local `outbox_events` table (same transaction as domain write)
2. Worker polls outbox periodically
3. HTTP POST to Cloudflare Worker
4. Mark event as sent on success

**Benefits**:
- No distributed transactions
- Eventual consistency
- Retry on failure
- Audit trail

### Circuit Breaker (Order Safety)

**Purpose**: Prevent runaway order submission

**Rules**:
- Max orders per minute: 10
- Circuit opens on: 5 errors in 60s
- Cooldown: 120 minutes
- Manual reset available

**Implementation**: `OrderSafetyValidator` in OptionsExecutionService

---

## Database Schema

### supervisor.db

```sql
-- Service health tracking
CREATE TABLE heartbeats (
    service_name TEXT PRIMARY KEY,
    hostname TEXT NOT NULL,
    last_seen_at TEXT NOT NULL,  -- ISO 8601
    uptime_seconds INTEGER,
    cpu_percent REAL,
    ram_percent REAL,
    disk_free_gb REAL,
    trading_mode TEXT,
    version TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

-- Event outbox for cloud sync
CREATE TABLE outbox_events (
    event_id TEXT PRIMARY KEY,
    event_type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    dedupe_key TEXT,
    status TEXT NOT NULL DEFAULT 'pending',  -- pending|sent|failed
    retry_count INTEGER DEFAULT 0,
    last_error TEXT,
    next_retry_at TEXT,
    created_at TEXT NOT NULL,
    sent_at TEXT
);
CREATE UNIQUE INDEX idx_outbox_dedupe ON outbox_events(dedupe_key) 
WHERE dedupe_key IS NOT NULL;

-- System alerts
CREATE TABLE alerts (
    alert_id TEXT PRIMARY KEY,
    alert_type TEXT NOT NULL,
    severity TEXT NOT NULL,  -- info|warning|critical
    message TEXT NOT NULL,
    details_json TEXT,
    source_service TEXT NOT NULL,
    created_at TEXT NOT NULL,
    resolved_at TEXT,
    resolved_by TEXT
);

-- Log file position tracking
CREATE TABLE log_reader_state (
    file_path TEXT PRIMARY KEY,
    last_position INTEGER NOT NULL,
    last_size INTEGER NOT NULL,
    updated_at TEXT NOT NULL
);

-- IV term structure snapshots
CREATE TABLE ivts_snapshots (
    snapshot_id TEXT PRIMARY KEY,
    symbol TEXT NOT NULL,
    timestamp_utc TEXT NOT NULL,
    iv_30d REAL,
    iv_60d REAL,
    iv_90d REAL,
    iv_120d REAL,
    ivr_percentile REAL,
    term_structure_slope REAL,
    is_inverted INTEGER,  -- boolean
    iv_min_52week REAL,
    iv_max_52week REAL,
    created_at TEXT NOT NULL
);
CREATE INDEX idx_ivts_symbol_timestamp ON ivts_snapshots(symbol, timestamp_utc DESC);
```

### options.db

```sql
-- Strategy campaigns
CREATE TABLE campaigns (
    campaign_id TEXT PRIMARY KEY,
    strategy_json TEXT NOT NULL,  -- Full SDF
    state TEXT NOT NULL,  -- Active|Closed|Failed
    state_json TEXT,  -- State metadata
    created_at TEXT NOT NULL,
    activated_at TEXT,
    closed_at TEXT,
    close_reason TEXT
);

-- Orders
CREATE TABLE orders (
    order_id TEXT PRIMARY KEY,
    campaign_id TEXT NOT NULL,
    ibkr_order_id INTEGER,
    symbol TEXT NOT NULL,
    action TEXT NOT NULL,  -- BUY|SELL
    order_type TEXT NOT NULL,  -- LMT|MKT|STP
    quantity INTEGER NOT NULL,
    limit_price REAL,
    stop_price REAL,
    status TEXT NOT NULL,  -- PendingSubmit|Submitted|Filled|Cancelled|Failed
    filled_quantity INTEGER DEFAULT 0,
    avg_fill_price REAL,
    commission REAL,
    submitted_at TEXT,
    filled_at TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (campaign_id) REFERENCES campaigns(campaign_id)
);
CREATE INDEX idx_orders_campaign ON orders(campaign_id);
CREATE INDEX idx_orders_ibkr ON orders(ibkr_order_id);

-- Trade fills
CREATE TABLE fills (
    fill_id TEXT PRIMARY KEY,
    order_id TEXT NOT NULL,
    execution_id TEXT,
    symbol TEXT NOT NULL,
    side TEXT NOT NULL,  -- BOT|SLD
    shares INTEGER NOT NULL,
    price REAL NOT NULL,
    commission REAL,
    executed_at TEXT NOT NULL,
    FOREIGN KEY (order_id) REFERENCES orders(order_id)
);
CREATE INDEX idx_fills_order ON fills(order_id);

-- Positions (shared with TradingSupervisorService)
CREATE TABLE positions (
    position_id TEXT PRIMARY KEY,
    account_id TEXT NOT NULL,
    symbol TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    avg_cost REAL NOT NULL,
    market_value REAL,
    unrealized_pnl REAL,
    realized_pnl REAL,
    updated_at TEXT NOT NULL
);
CREATE UNIQUE INDEX idx_positions_account_symbol ON positions(account_id, symbol);
```

---

## Configuration

### appsettings.json

```json
{
  "TradingMode": "paper",  // "paper" | "live" (CRITICAL SAFETY)
  
  "Sqlite": {
    "SupervisorDbPath": "data/supervisor.db"
  },
  
  "OptionsDb": {
    "OptionsDbPath": "data/options.db"
  },
  
  "IBKR": {
    "Host": "127.0.0.1",
    "PaperPort": 4002,
    "LivePort": 4001,
    "ClientId": 1,
    "ConnectionTimeoutSeconds": 30,
    "ReconnectInitialDelaySeconds": 5,
    "ReconnectMaxDelaySeconds": 60,
    "MaxReconnectAttempts": 10
  },
  
  "Safety": {
    "MaxPositionPctOfAccount": 0.05,
    "CircuitBreakerCooldownMinutes": 120,
    "MaxOrdersPerMinute": 10
  },
  
  "Monitoring": {
    "HeartbeatIntervalSeconds": 60,
    "OutboxSyncIntervalSeconds": 30,
    "TelegramIntervalSeconds": 10
  },
  
  "Telegram": {
    "BotToken": "",  // Secret
    "ChatId": "",
    "Enabled": true
  }
}
```

---

## Testing Strategy

### Unit Tests (xUnit)

**Location**: `tests/*/`

**Coverage**:
- Repository integration tests (with in-memory SQLite)
- Worker lifecycle tests
- Domain model tests
- Service DI registration tests

**Pattern**:
```csharp
public sealed class RepositoryTests : IAsyncLifetime
{
    private InMemoryConnectionFactory _db;
    
    public async Task InitializeAsync() {
        _db = new InMemoryConnectionFactory();
        await RunMigrations();
    }
    
    [Fact]
    public async Task SaveAsync_ThenGet_ReturnsEntity() { ... }
}
```

### E2E Tests (Vitest)

**Location**: `infra/cloudflare/worker/test/`, `dashboard/test/`

**Coverage**:
- Bot webhook flows (Telegram + Discord)
- Strategy conversion API
- Dashboard wizard flows

**Pattern**:
```typescript
describe('Telegram Bot E2E', () => {
  it('Start → Menu → Query → Response', async () => {
    const webhook = createTelegramUpdate('/start');
    const response = await app.request('/bot/telegram', { ... });
    expect(response.status).toBe(200);
  });
});
```

---

## Performance Considerations

### SQLite Optimization

**WAL Mode** (Write-Ahead Logging):
```sql
PRAGMA journal_mode=WAL;      -- Enables concurrent reads
PRAGMA synchronous=NORMAL;    -- Balance safety/performance
PRAGMA busy_timeout=5000;     -- 5s lock wait
PRAGMA foreign_keys=ON;       -- Enforce referential integrity
```

**Indexes**:
- All foreign keys indexed
- Query filters (WHERE, ORDER BY) indexed
- Unique constraints for deduplication

### Worker Scheduling

**Intervals** (default):
- HeartbeatWorker: 60s (metrics collection)
- OutboxSyncWorker: 30s (cloud sync)
- TelegramWorker: 10s (alert delivery)
- LogReaderWorker: 120s (log parsing)
- IvtsMonitorWorker: 300s (IV snapshots)

**Adjust** based on load:
```json
{
  "Monitoring": {
    "HeartbeatIntervalSeconds": 30  // More frequent
  }
}
```

---

## Security

### Secrets Management

**Never commit**:
- `TELEGRAM_BOT_TOKEN`
- `DISCORD_PUBLIC_KEY`
- `CLAUDE_API_KEY`
- IBKR credentials

**Storage**:
- Cloudflare: `wrangler secret put`
- Windows Services: `appsettings.json` (gitignored)
- Environment variables (production)

### Access Control

**Bot Whitelist**:
```sql
SELECT * FROM bot_whitelist WHERE user_id = ? AND platform = ?;
```

**IBKR Connection**:
- Localhost only (127.0.0.1)
- Paper trading by default
- Live trading requires explicit `TradingMode: "live"`

---

## Deployment Targets

| Component | Target | URL |
|-----------|--------|-----|
| TradingSupervisorService | Windows Server | localhost |
| OptionsExecutionService | Windows Server | localhost |
| Cloudflare Worker | Cloudflare Workers | https://trading-bot.padosoft.workers.dev |
| Dashboard | Cloudflare Pages | https://trading-dashboard.pages.dev |

---

## References

- [Deployment Guide](DEPLOYMENT_GUIDE.md)
- [Strategy File Format (SDF)](STRATEGY_FORMAT.md)
- [Error Registry](../knowledge/errors-registry.md)
- [Lessons Learned](../knowledge/lessons-learned.md)

---

**Last reviewed**: 2026-04-07  
**Maintainer**: Trading System Team  
**Version**: 1.0.0
