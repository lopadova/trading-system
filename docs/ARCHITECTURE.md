---
title: "Trading System Architecture"
tags: ["architecture", "reference", "dotnet", "worker", "dashboard"]
aliases: ["Architecture", "System Architecture", "Architecture Deep Dive"]
status: current
audience: ["developer", "reviewer"]
last-reviewed: "2026-04-21"
related:
  - "[[Trading System - Architecture Overview|ARCHITECTURE_OVERVIEW]]"
  - "[[MARKET_DATA_PIPELINE]]"
  - "[[OBSERVABILITY]]"
  - "[[Strategy File Format|STRATEGY_FORMAT]]"
  - "[[Trading System - Deployment Guide|DEPLOYMENT_GUIDE]]"
---

# Trading System Architecture

> Production-ready automated options trading system
> Last updated: 2026-04-05

---

## Table of Contents

- [System Overview](#system-overview)
- [Architecture Diagram](#architecture-diagram)
- [Components](#components)
- [Data Flow](#data-flow)
- [Technology Stack](#technology-stack)
- [Design Patterns](#design-patterns)
- [Safety Architecture](#safety-architecture)

---

## System Overview

The Trading System is a distributed application for automated options trading via Interactive Brokers (IBKR). It consists of two Windows Services, a Cloudflare Worker API, and a React dashboard.

### Key Characteristics

- **Safety-First Design**: Multi-layer validation prevents accidental live trading
- **Fault-Tolerant**: Automatic reconnection, retry logic, circuit breakers
- **Observable**: Comprehensive logging, metrics, and alerting
- **Scalable**: Event-driven architecture with transactional outbox pattern
- **Testable**: 100+ unit tests, integration tests for all critical paths

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Windows Server                            │
│                                                              │
│  ┌─────────────────────┐      ┌──────────────────────────┐ │
│  │ Supervisor Service  │      │ Execution Service        │ │
│  │                     │      │                          │ │
│  │ - Health Monitor    │      │ - Strategy Engine        │ │
│  │ - IBKR Monitor      │      │ - Order Placer           │ │
│  │ - Log Reader        │      │ - Campaign Manager       │ │
│  │ - Alert Creator     │      │ - Position Tracker       │ │
│  │ - Telegram Sender   │      │ - Market Data Service    │ │
│  │ - IVTS Monitor      │      │ - Greeks Monitor         │ │
│  │ - Greeks Monitor    │      │ - IBKR Client            │ │
│  │                     │      │                          │ │
│  │  supervisor.db      │      │  options.db              │ │
│  │  (SQLite + WAL)     │      │  (SQLite + WAL)          │ │
│  └──────────┬──────────┘      └───────────┬──────────────┘ │
│             │                             │                 │
│             └─────────────┬───────────────┘                 │
│                           │                                 │
└───────────────────────────┼─────────────────────────────────┘
                            │
                            │ HTTPS POST (events)
                            ▼
                ┌───────────────────────────┐
                │  Cloudflare Worker        │
                │                           │
                │  - Event Ingestion        │
                │  - D1 Storage             │
                │  - REST API               │
                │  - Rate Limiting          │
                │  - CORS Handling          │
                │                           │
                │  trading-db (D1/SQLite)   │
                └───────────┬───────────────┘
                            │
                            │ REST API (HTTPS)
                            ▼
                ┌───────────────────────────┐
                │  React Dashboard          │
                │                           │
                │  - Real-time Monitoring   │
                │  - Campaign Management    │
                │  - Risk Analytics         │
                │  - Alert Viewer           │
                │  - Settings Management    │
                │                           │
                │  (Zustand + React Query)  │
                └───────────────────────────┘


External Services:
┌──────────────────┐              ┌──────────────────┐
│  IBKR TWS/Gateway│◄─────────────┤ Both Services    │
│  (Paper/Live)    │  TCP Socket  │ (EClient)        │
└──────────────────┘              └──────────────────┘

┌──────────────────┐              ┌──────────────────┐
│  Telegram API    │◄─────────────┤ Supervisor       │
│                  │  HTTPS       │ Service          │
└──────────────────┘              └──────────────────┘
```

---

## Components

### 1. TradingSupervisorService (Windows Service)

**Responsibility**: System health monitoring and alerting

**Background Workers**:
- `HeartbeatWorker` - Records service health metrics every 30s
- `OutboxSyncWorker` - Publishes events to Cloudflare Worker (retry on failure)
- `LogReaderWorker` - Tails execution service logs for error detection
- `TelegramWorker` - Sends critical alerts to Telegram
- `IvtsMonitorWorker` - Monitors implied volatility term structure (optional)
- `GreeksMonitorWorker` - Monitors position Greeks for risk alerts

**Key Features**:
- Machine metrics collection (CPU, RAM, disk)
- IBKR connection monitoring
- Transactional outbox pattern for reliable event delivery
- Exponential backoff retry logic
- Telegram rate limiting (30 messages/min)

**Database**: `supervisor.db` (SQLite with WAL mode)

**Tables**:
- `service_heartbeats` - Service health records
- `sync_outbox` - Events pending sync to Worker
- `alert_history` - All system alerts
- `log_reader_state` - File position for log tailing
- `ivts_snapshots` - Implied volatility data
- `positions_snapshot` - Cross-service position view

---

### 2. OptionsExecutionService (Windows Service)

**Responsibility**: Strategy execution and order management

**Background Workers**:
- `IbkrConnectionWorker` - Manages IBKR connection lifecycle with reconnection
- `CampaignMonitorWorker` - Executes strategy entry/exit logic

**Key Features**:
- Strategy loading from JSON files (with validation)
- Campaign state machine (Pending → Active → Closed)
- Order placement with safety validation (paper mode enforcement)
- Position tracking with real-time Greeks calculation
- Market data caching with event-driven updates
- Order tracking for audit trail

**Database**: `options.db` (SQLite with WAL mode)

**Tables**:
- `campaigns` - Strategy instances
- `positions` - Open positions with Greeks
- `order_tracking` - Order audit trail

---

### 3. Cloudflare Worker (API Gateway)

**Responsibility**: Event aggregation and API for dashboard

**Technology**: Hono framework + D1 database

**Endpoints**:
- `POST /events` - Ingest events from services (authenticated)
- `GET /health` - Worker health check
- `GET /api/heartbeats` - Latest service health
- `GET /api/alerts` - Alert history with filtering
- `GET /api/positions` - Current positions
- `GET /api/campaigns` - Strategy campaigns

**Key Features**:
- Rate limiting (100 req/min per IP)
- CORS with configurable origin
- D1 prepared statements (SQL injection prevention)
- TypeScript strict mode with full type safety

**Database**: `trading-db` (Cloudflare D1, SQLite-compatible)

---

### 4. React Dashboard (Frontend)

**Responsibility**: Real-time monitoring and control UI

**Technology**: React 19 + Vite + TypeScript + Tailwind CSS v4

**Key Libraries**:
- `@tanstack/react-query` - Server state management
- `zustand` - UI state (theme, settings)
- `recharts` - Data visualization
- `ky` - HTTP client

**Pages**:
- Overview - System health summary
- Trading - Active campaigns and positions
- Market - Market data and volatility
- Portfolio - Position management
- Risk - Greeks monitoring and risk metrics
- Analytics - Performance charts
- Strategies - Strategy library
- Alerts - Alert history and management
- Settings - Configuration

**Key Features**:
- Dark/light theme with localStorage persistence
- Real-time data polling (configurable interval)
- Anti-flash script for FOUC prevention
- Responsive design (mobile-friendly)
- TypeScript strict mode with `noUncheckedIndexedAccess`

---

## Data Flow

### Event Flow (Supervisor → Worker → Dashboard)

```
1. HeartbeatWorker records metrics
   ↓
2. Insert into supervisor.db (service_heartbeats + sync_outbox)
   ↓
3. OutboxSyncWorker picks up pending events
   ↓
4. POST to Cloudflare Worker (/events endpoint)
   ↓
5. Worker stores in D1 database
   ↓
6. Dashboard polls GET /api/heartbeats
   ↓
7. Dashboard renders health status
```

### Order Flow (Strategy → IBKR → Tracking)

```
1. CampaignMonitorWorker evaluates entry rules
   ↓
2. OrderPlacer validates safety constraints
   ↓
3. IBKR client places order (via TWS socket)
   ↓
4. TWS callback confirms execution
   ↓
5. Insert into options.db (positions + order_tracking)
   ↓
6. Supervisor GreeksMonitor detects position
   ↓
7. Alert created if Greeks exceed thresholds
```

### Alert Flow (Detection → Telegram + Dashboard)

```
1. Worker detects issue (e.g., LogReaderWorker finds ERROR log)
   ↓
2. Insert into alert_history + sync_outbox
   ↓
3. TelegramWorker queues critical alerts
   ↓
4. Send to Telegram API (with rate limiting)
   ↓
5. OutboxSyncWorker syncs alert to D1
   ↓
6. Dashboard displays in Alerts page
```

---

## Technology Stack

### Backend (.NET Services)

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Runtime framework |
| C# | 12.0 | Primary language |
| SQLite | 3.x | Local databases |
| Dapper | 2.1.28 | Data access (micro-ORM) |
| Serilog | 3.1.1 | Structured logging |
| IBApi | 10.19.2 | Interactive Brokers API |
| Telegram.Bot | 19.0.0 | Telegram alerting |
| xUnit | 2.6.6 | Unit testing |

### Frontend (Dashboard)

| Technology | Version | Purpose |
|------------|---------|---------|
| React | 19.x | UI framework |
| TypeScript | 5.9 | Type safety |
| Vite | 8.x | Build tool |
| Tailwind CSS | 4.2 | Styling |
| React Query | 5.x | Server state |
| Zustand | 5.x | Client state |
| Recharts | 2.x | Charts |
| Vitest | 3.x | Testing |

### Infrastructure

| Technology | Version | Purpose |
|------------|---------|---------|
| Cloudflare Workers | - | Serverless API |
| Cloudflare D1 | - | Edge database |
| Hono | 4.x | Worker framework |
| Windows Server | 2019+ | Service host |

---

## Design Patterns

### 1. Transactional Outbox Pattern

**Purpose**: Reliable event publishing without distributed transactions

**Implementation**: `sync_outbox` table + `OutboxSyncWorker`

Events are written to database and outbox table in same transaction. Worker polls outbox and publishes to external service with retry logic.

**Benefits**:
- Guarantees at-least-once delivery
- No lost events on worker crash
- Automatic retry with exponential backoff

---

### 2. Repository Pattern

**Purpose**: Separation of data access from business logic

**Implementation**: All database access via repository interfaces

```csharp
public interface IHeartbeatRepository
{
    Task SaveHeartbeatAsync(string serviceName, /* ... */, CancellationToken ct);
    Task<HeartbeatRecord?> GetLatestAsync(string serviceName, CancellationToken ct);
}
```

**Benefits**:
- Testable (mock repositories in tests)
- Swappable (could replace SQLite with Postgres)
- Clear contracts

---

### 3. Circuit Breaker Pattern

**Purpose**: Prevent cascade failures

**Implementation**: `OrderPlacer` with `_circuitState` and retry limits

After N consecutive failures, circuit opens (reject all requests). After cooldown period, allow one test request (half-open). On success, close circuit.

**Benefits**:
- Fast-fail when IBKR is down
- Automatic recovery when service restores
- Protects downstream systems

---

### 4. Background Service Pattern

**Purpose**: Long-running work in Windows Services

**Implementation**: All workers extend `BackgroundService`

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try { /* work */ }
        catch (Exception ex) { _logger.LogError(ex, "..."); }
        await Task.Delay(interval, stoppingToken);
    }
}
```

**Benefits**:
- Graceful shutdown on service stop
- Exception isolation (one worker crash doesn't kill service)
- Testable with `CancellationTokenSource`

---

### 5. State Machine Pattern

**Purpose**: Model campaign lifecycle with explicit transitions

**Implementation**: `Campaign` record with `State` enum and transition methods

```csharp
public enum CampaignState { Pending, Active, Closed }

public Campaign Activate() => this with { State = CampaignState.Active };
public Campaign Close(string reason) => this with { State = CampaignState.Closed };
```

**Benefits**:
- Immutable state (records with `with` expression)
- Impossible states prevented at compile time
- Audit trail of state changes

---

## Safety Architecture

### Multi-Layer Safety Validation

**Layer 1: Configuration Defaults**
- `TradingMode.Paper = 0` (default enum value)
- Default port 7497 (paper TWS)
- Immutable config records (cannot mutate after creation)

**Layer 2: Configuration Validation**
- `SupervisorConfigurationValidator` and `OptionsConfigurationValidator`
- Reject live port (7496, 4002) + `TradingMode.Live` combination
- Fail-fast at service startup (before connecting to IBKR)

**Layer 3: Runtime Validation**
- `OrderPlacer.ValidateOrderSafetyAsync()` checks config before every order
- Reject orders if `TradingMode.Live` is detected
- Reject orders exceeding position limits or capital constraints

**Layer 4: Git Protection**
- `.gitignore` blocks `strategies/private/` and `appsettings.Production.json`
- Installation scripts warn if deploying with live configuration
- Pre-commit hooks (optional) verify no secrets in commit

**Layer 5: Audit Trail**
- All orders recorded in `order_tracking` table
- Timestamp, user, strategy, status, fill price
- Cannot delete (append-only audit log)

### IBKR Connection Safety

**Multi-layer port validation**:
```csharp
// 1. Reject live ports entirely
if (config.Port == 7496 || config.Port == 4002)
    throw new ArgumentException("Live ports forbidden");

// 2. Require paper mode
if (config.TradingMode != TradingMode.Paper)
    throw new ArgumentException("Only paper mode allowed");

// 3. Validate before every connection attempt
await ValidateConfigAsync(cancellationToken);
```

---

## Database Schema

Schema source of truth is split by component — each side has its own migration history:

| Component | Schema location | Migration runner |
|-----------|-----------------|------------------|
| Cloudflare Worker (D1, cloud) | `infra/cloudflare/worker/migrations/*.sql` | `bunx wrangler d1 migrations apply` |
| TradingSupervisorService (SQLite, local) | `src/TradingSupervisorService/Migrations/` (C#) | `SupervisorMigrations` on service startup |
| OptionsExecutionService (SQLite, local) | `src/OptionsExecutionService/Migrations/` (C#) | `OptionsMigrations` on service startup |

When adding a column or table, update the migration in the correct
location — cross-database changes (rare) require coordinated migrations
on both sides.

Both .NET services use SQLite with:
- WAL mode (concurrent readers + 1 writer)
- Foreign key enforcement enabled
- Automatic migrations on startup
- Idempotent migration system (safe to run multiple times)

---

## Deployment Architecture

### Production Deployment

```
Windows Server (on-premises or cloud)
├── TradingSupervisorService (Windows Service, Auto-start)
├── OptionsExecutionService (Windows Service, Auto-start)
├── IBKR Gateway (running, logged in to paper account)
├── data/
│   ├── supervisor.db
│   └── options.db
├── logs/
│   ├── supervisor-YYYYMMDD.log
│   └── options-execution-YYYYMMDD.log
└── strategies/
    └── private/
        └── my-strategy.json

Cloudflare (edge network)
├── Worker (trading-system)
└── D1 Database (trading-db)

Static Hosting (Cloudflare Pages or S3)
└── React Dashboard (SPA)
```

### Disaster Recovery

**Database Backups**:
- SQLite files backed up nightly (simple file copy)
- WAL checkpoint before backup ensures consistency
- Store backups in versioned S3 bucket or Windows backup system

**Service Recovery**:
- Services auto-restart on failure (sc.exe failure policy)
- IBKR client auto-reconnects with exponential backoff
- Outbox pattern ensures no lost events

**Configuration Recovery**:
- All config in source control (except secrets)
- Secrets in Windows Credential Manager or Azure Key Vault
- Strategy files in `strategies/private/` (backed up separately)

---

## Performance Characteristics

### Latency

- Heartbeat write: <10ms (SQLite local write)
- Alert creation: <20ms (local write + outbox insert)
- Order placement: 50-200ms (network round-trip to TWS)
- Dashboard API: <100ms (D1 query + HTTPS)
- Market data update: 100-500ms (IBKR tick frequency)

### Throughput

- Heartbeats: 2/min (60/min across all services)
- Alerts: ~10/min typical, 100/min peak (rate limited)
- Orders: <1/min typical (human-like trading pace)
- Dashboard API: 1-10 req/sec (polling + user navigation)

### Resource Usage

**TradingSupervisorService**:
- CPU: <5% idle, 10-15% during outbox sync
- RAM: 50-100 MB
- Disk: <1 MB/day (logs rotate daily)

**OptionsExecutionService**:
- CPU: <5% idle, 20-40% during market data updates
- RAM: 100-200 MB (market data cache)
- Disk: <5 MB/day (position updates + audit trail)

**Cloudflare Worker**:
- CPU: <1ms per request (edge compute)
- Memory: <128 MB (stateless)
- Database: <100 MB (6 months of data)

---

## Monitoring and Observability

### Logging

**Format**: Serilog structured JSON logs

**Levels**:
- `Debug` - Verbose internal state (disabled in production)
- `Information` - Normal operations (heartbeat recorded, order placed)
- `Warning` - Recoverable errors (IBKR disconnect, retry attempt)
- `Error` - Unrecoverable errors (invalid config, order rejection)
- `Critical` - Service-wide failures (database unavailable)

**Sinks**:
- File (daily rolling, 30-day retention)
- Console (development only)

### Metrics

**System Metrics** (from `IMachineMetricsCollector`):
- CPU usage (%)
- RAM usage (%)
- Disk free space (GB)
- Service uptime (seconds)

**Business Metrics**:
- Active campaigns
- Open positions
- Total P&L
- Order success rate
- Alert count by severity

### Alerts

**Critical Alerts** (sent to Telegram):
- Service heartbeat missing (>2 min)
- IBKR disconnection (>5 min)
- Order rejection
- Position Greeks exceeding thresholds
- Disk space <10 GB

**Warning Alerts** (dashboard only):
- Log errors detected
- Retry exhausted
- Configuration drift
- Market data stale

---

## Security

### Authentication

- **Worker API**: Bearer token in `Authorization` header
- **Telegram**: Bot token in appsettings (encrypted at rest)
- **IBKR**: Username/password in TWS (not stored by system)

### Authorization

- Worker API key stored as Cloudflare secret (not in code)
- Services run as local service account (minimal Windows permissions)
- Dashboard has no authentication (internal network only, or add OAuth)

### Data Protection

- Databases encrypted at rest (Windows BitLocker on server)
- Logs do not contain secrets (Serilog destructuring)
- Git ignores sensitive files (`.gitignore` enforcement)

### Network Security

- IBKR connection over encrypted TCP (TWS built-in encryption)
- Worker API over HTTPS only (Cloudflare automatic TLS)
- Telegram API over HTTPS (TLS 1.3)

---

## Scalability

### Current Limits

- **Services**: Single instance per machine (not clustered)
- **Database**: SQLite supports ~10K writes/sec, ~100K reads/sec (sufficient)
- **Campaigns**: Max ~100 concurrent (limited by IBKR market data subscriptions)
- **Positions**: Max ~500 (IBKR API client limit)

### Future Scaling Options

**If needed** (not required for current scope):

1. **Horizontal Service Scaling**: Replace SQLite with Postgres, run multiple service instances with shared DB
2. **Event Streaming**: Replace outbox polling with RabbitMQ or Kafka
3. **Separate Market Data Service**: Dedicated service for IBKR market data with pub/sub to execution service
4. **Multi-Account**: Support multiple IBKR accounts with account-specific workers

---

## References

- [Getting Started Guide](./GETTING_STARTED.md)
- [Configuration Reference](./CONFIGURATION.md)
- [Strategy Format](./STRATEGY_FORMAT.md)
- [Telegram Integration](./telegram-integration.md)
- [Deployment Guide](./DEPLOYMENT_GUIDE.md)
- [Contributing Guide](./CONTRIBUTING.md)

---

*Last updated: 2026-04-05 | Trading System v1.0*
