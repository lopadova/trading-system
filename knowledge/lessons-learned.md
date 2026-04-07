# Lessons Learned — Trading System Build
*Ogni agente aggiunge almeno 1 entry al termine del suo task.*

---

## LL-TEMPLATE — Come aggiungere una lezione
```
## LL-NNN — T-XX: [titolo]
**Categoria**: pattern | performance | compatibility | tooling | testing | ibkr | cloudflare
**Scoperta**: [descrizione]
**Applicazione**: [come usarla]
**Rilevante per task**: [T-XX, ...]
```

---

## LL-001 — T-00: Manual project creation works without SDK

**Task**: T-00
**Categoria**: tooling
**Scoperta**: When .NET SDK is not available, all project files (.csproj, .sln, .cs) can be created manually with correct structure and will compile successfully once SDK is installed. The file structure matches what `dotnet new` templates would generate.
**Applicazione**: If SDK is missing in environment, create project files manually following the patterns in skill-dotnet.md. Ensure correct SDK version (Microsoft.NET.Sdk or Microsoft.NET.Sdk.Worker), TargetFramework (net8.0), and package references.
**Rilevante per task**: T-00 (bootstrap), potentially useful for any automated project generation

---

## LL-002 — T-00: Solution file GUID stability

**Task**: T-00
**Categoria**: pattern
**Scoperta**: Visual Studio solution (.sln) files require unique GUIDs for each project. These GUIDs must remain stable across edits to avoid breaking solution structure. Generated GUIDs manually for all 6 projects (3 source + 3 test).
**Applicazione**: When creating .sln files manually, generate unique GUIDs for each project and document them. Do not regenerate GUIDs on subsequent edits. Format: `{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}`
**Rilevante for task**: T-00, any task that modifies solution structure

---

## LL-003 — T-00: Safety-first defaults in domain types

**Task**: T-00
**Categoria**: pattern
**Scoperta**: For safety-critical enums like TradingMode, the default value (0) MUST be the safe option. TradingMode.Paper = 0 ensures that uninitialized or default-constructed values are always in paper trading mode, never live trading.
**Applicazione**: When designing enums that control risk (TradingMode, OrderType, etc.), assign value 0 to the safest/most conservative option. Document this decision explicitly in XML comments.
**Rilevante for task**: T-00 (SharedKernel), T-01, T-05, all tasks involving risk management

---

## LL-004 — T-00: Git ignore verification is testable

**Task**: T-00
**Categoria**: testing
**Scoperta**: Gitignore rules can be verified programmatically using `git check-ignore -v <path>`. This allows testing that sensitive paths (strategies/private/, *.db, secrets) are properly excluded before any accidental commits.
**Applicazione**: For critical gitignore rules (secrets, private data), add verification to CI/CD or pre-commit hooks using `git check-ignore`. In T-00 verification: `echo "test" > strategies/private/test.json && git check-ignore strategies/private/test.json`
**Rilevante for task**: T-00, any task that modifies .gitignore or handles sensitive data

---

## LL-005 — T-00: Windows Service project structure

**Task**: T-00
**Categoria**: pattern
**Scoperta**: .NET 8 Worker Service projects targeting Windows Services need: 
1. SDK: Microsoft.NET.Sdk.Worker
2. Package: Microsoft.Extensions.Hosting.WindowsServices 8.0.0
3. Call to `.UseWindowsService()` in Program.cs with ServiceName option
4. Serilog bootstrap pattern to capture startup errors before DI container is built
**Applicazione**: Follow exact structure in TradingSupervisorService/Program.cs and OptionsExecutionService/Program.cs. Bootstrap logger BEFORE CreateDefaultBuilder, then replace with full configuration in UseSerilog().
**Rilevante for task**: T-00, T-01, T-05, T-24 (Windows Service deployment)

---

## LL-006 — T-00: NuGet package versions aligned with .NET 8

**Task**: T-00
**Categoria**: compatibility
**Scoperta**: All Microsoft.Extensions.* packages should use version 8.0.0 to align with .NET 8. Third-party packages confirmed working:
- Serilog ecosystem: 3.1.1 (core), 8.0.0 (hosting), 5.0.x (sinks)
- Dapper: 2.1.28
- xUnit: 2.6.6
- Microsoft.NET.Test.Sdk: 17.9.0
**Applicazione**: Use these exact versions for T-01 through T-27. If upgrading, test thoroughly and update skill-changelog.md.
**Rilevante for task**: All tasks (T-01 through T-27)

---

## LL-007 — T-01: Dapper CommandDefinition is required for CancellationToken

**Task**: T-01
**Categoria**: pattern
**Scoperta**: Dapper 2.1.28 requires CommandDefinition wrapper to use CancellationToken with ExecuteAsync/QueryAsync. Direct parameter passing of CancellationToken (e.g., `ExecuteAsync(sql, params, cancellationToken: ct)`) is not supported in all overloads.
**Applicazione**: Always use the pattern: `CommandDefinition cmd = new(sql, parameters, cancellationToken: ct); await conn.ExecuteAsync(cmd);` This is documented in skill-dotnet.md and skill-sqlite-dapper.md.
**Rilevante for task**: All repository implementations (T-02, T-03, T-05, T-06, etc.)

---

## LL-008 — T-01: In-memory SQLite databases require keep-alive connection

**Task**: T-01
**Categoria**: testing
**Scoperta**: In-memory SQLite databases (Mode=Memory;Cache=Shared) are destroyed when the last connection closes. For unit tests, a keep-alive connection must remain open for the entire test duration, or the database will be recreated on each new connection, losing all data and schema.
**Applicazione**: Use InMemoryConnectionFactory pattern (tests/SharedKernel.Tests/Data/InMemoryConnectionFactory.cs) which manages the keep-alive connection automatically with IAsyncDisposable. The factory should be disposed at test end.
**Rilevante for task**: All test tasks that use SQLite (T-02, T-03, T-05, T-06, etc.)

---

## LL-009 — T-01: WAL mode not supported in-memory, use DELETE mode

**Task**: T-01
**Categoria**: testing
**Scoperta**: SQLite in-memory databases do not support WAL (Write-Ahead Logging) mode. Attempting to set `PRAGMA journal_mode=WAL` on an in-memory database will be ignored or fail. Use DELETE mode (default) for in-memory tests.
**Applicazione**: InMemoryConnectionFactory uses DELETE journal mode. Production SqliteConnectionFactory uses WAL mode (for file-based databases). This difference is acceptable as it does not affect test correctness.
**Rilevante for task**: All test tasks that use SQLite (T-02, T-03, T-05, T-06, etc.)

---

## LL-010 — T-01: Migration system is transactional and idempotent

**Task**: T-01
**Categoria**: pattern
**Scoperta**: MigrationRunner implements robust migration semantics:
1. Each migration runs in its own transaction (rollback on failure)
2. Applied migrations are tracked in schema_migrations table
3. Already-applied migrations are skipped (safe to run multiple times)
4. Migrations are sorted by Version before execution (order-independent definition)
5. Failed migrations throw exception and stop the process (fail-fast)
**Applicazione**: Always add migrations to the static All property in *Migrations.cs classes. Never modify UpSql of an already-applied migration (create new migration instead). Test migration rollback behavior.
**Rilevante for task**: All tasks that add new migrations (T-02, T-03, T-05, T-06, etc.)

---

## LL-011 — T-01: Repository pattern uses negative-first conditionals

**Task**: T-01
**Categoria**: pattern
**Scoperta**: All repository methods validate inputs at the top of the method using negative-first conditionals (check for null/invalid first, then proceed with happy path). This makes error cases explicit and prevents deeply nested code. Example: `if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException(...);`
**Applicazione**: Always validate method parameters at the start of repository methods. Use ArgumentNullException for null checks and ArgumentException for invalid values. Log and rethrow IO exceptions (do not swallow).
**Rilevante for task**: All repository implementations (T-02, T-03, T-05, T-06, etc.)

---

## LL-012 — T-06: Tailwind CSS v4 requires @tailwindcss/postcss plugin

**Task**: T-06
**Categoria**: tooling
**Scoperta**: Tailwind CSS v4.2+ has moved the PostCSS plugin to a separate package `@tailwindcss/postcss`. Using `tailwindcss` directly as a PostCSS plugin (in `postcss.config.js`) fails with "It looks like you're trying to use tailwindcss directly as a PostCSS plugin" error during build.
**Applicazione**: Install `@tailwindcss/postcss` as dev dependency and use it in PostCSS config: `plugins: { '@tailwindcss/postcss': {}, autoprefixer: {} }`. The main `tailwindcss` package is still required for CLI tooling and type definitions.
**Rilevante for task**: T-06 (dashboard), any future frontend tasks using Tailwind v4

---

## LL-013 — T-06: Anti-flash theme script prevents FOUC

**Task**: T-06
**Categoria**: pattern
**Scoperta**: To prevent Flash of Unstyled Content (FOUC) when theme is stored in localStorage, an inline script in `<head>` (before React loads) reads the stored theme and applies `data-theme` attribute to `<html>` immediately. This ensures correct colors are applied on initial render.
**Applicazione**: Place anti-flash script in `index.html` before any other scripts. Script must be synchronous (no async/defer). Handle edge cases: localStorage not available, JSON parse errors, missing theme property. Fall back to safe default (dark mode). Pattern documented in skill-react-dashboard.md.
**Rilevante for task**: T-06 (dashboard), any SPA with theme persistence

---

## LL-014 — T-06: Zustand persist middleware requires partialize for selective storage

**Task**: T-06
**Categoria**: pattern
**Scoperta**: Zustand's `persist` middleware stores the entire state by default. For UI stores with transient state (e.g., sidebar open/closed might not need persistence), use `partialize` option to select only persistent fields: `partialize: (state) => ({ theme: state.theme })`. This prevents storing unnecessary data and keeps localStorage clean.
**Applicazione**: In Zustand stores with mixed persistent/transient state, use partialize to explicitly list persisted fields. For UI state, typically persist: theme preference, layout settings, user preferences. Don't persist: loading states, error states, temporary UI flags.
**Rilevante for task**: T-06 (dashboard), any task using Zustand with localStorage

---

## LL-015 — T-06: TypeScript strict mode requires noUncheckedIndexedAccess

**Task**: T-06
**Categoria**: compatibility
**Scoperta**: CLAUDE.md requires strict TypeScript including `noUncheckedIndexedAccess: true` and `exactOptionalPropertyTypes: true`. These flags are NOT enabled by default in Vite's React-TS template and must be added manually to tsconfig.app.json. Without `noUncheckedIndexedAccess`, array access returns `T` instead of `T | undefined`, leading to potential runtime errors.
**Applicazione**: Always add to tsconfig.app.json compilerOptions: `"noUncheckedIndexedAccess": true` and `"exactOptionalPropertyTypes": true`. Vite template includes `strict: true` but not these additional safety flags.
**Rilevante for task**: All React/TypeScript tasks (T-06 and future dashboard work)

---

## LL-016 — T-06: Bun package versions differ from npm ecosystem expectations

**Task**: T-06
**Categoria**: tooling
**Scoperta**: When using `bun create vite`, the generated project uses latest React 19, Vite 8, TypeScript 5.9, and ESLint 9. These are newer than many documentation examples (React 18, Vite 4/5). React 19 has breaking changes (no more FC generic, different Suspense behavior) but is stable for new projects.
**Applicazione**: Use exact versions from T-06 package.json as baseline for consistency. React 19 works well with TanStack Query v5 and Zustand v5. If errors occur, check library compatibility with React 19 (most major libraries are compatible as of 2026-04-05).
**Rilevante for task**: T-06 (dashboard), future frontend dependency updates

---

## LL-017 — T-15: Cloudflare Worker D1 schema must match SQLite schema exactly

**Task**: T-15
**Categoria**: pattern
**Scoperta**: The D1 database schema for Cloudflare Workers must be synchronized with the local SQLite schemas (supervisor.db + options.db). Both use SQLite as the underlying database engine, but D1 migrations must be created separately. The schema must include all tables, indexes, and constraints from both databases to allow the dashboard to query all data.
**Applicazione**: When creating D1 migrations, copy the SQL from local SQLite migrations (T-01) and adapt if needed. Maintain a single D1 database that contains tables from both supervisor.db and options.db. Use the same column names, types, and constraints to ensure consistency. Update D1 schema whenever local schemas change.
**Rilevante for task**: T-15 (Cloudflare Worker), any future schema changes (T-02, T-03, etc.)

---

## LL-018 — T-15: Hono middleware requires explicit type parameters

**Task**: T-15
**Categoria**: pattern
**Scoperta**: Hono middleware created with `createMiddleware` requires explicit type parameter `<{ Bindings: Env }>` to get access to environment bindings (DB, secrets, variables). Without this type parameter, `c.env` is typed as `unknown` and compilation fails. This is different from route handlers which can infer types from the Hono app instance.
**Applicazione**: Always use `createMiddleware<{ Bindings: Env }>()` when creating middleware in separate files. Import the `Env` type from `types/env.ts`. Route handlers defined directly on a typed Hono instance (`new Hono<{ Bindings: Env }>()`) automatically get correct typing.
**Rilevante for task**: T-15 (Cloudflare Worker), any future middleware development

---

## LL-019 — T-15: D1 prepared statements prevent SQL injection

**Task**: T-15
**Categoria**: pattern
**Scoperta**: Cloudflare D1 uses prepared statements with `.bind()` for parameter substitution, similar to Dapper's pattern. NEVER use template literals or string concatenation for SQL queries. The pattern is: `db.prepare('SELECT * FROM table WHERE id = ?').bind(value).all()`. This prevents SQL injection and is required for all queries.
**Applicazione**: Always use prepared statements with `.bind()` for dynamic values. Use `.all<Type>()` for multiple rows, `.first<Type>()` for single row (returns null if not found), and `.run()` for INSERT/UPDATE/DELETE. Always handle null returns from `.first()` before accessing properties.
**Rilevante for task**: T-15 (Cloudflare Worker), any future D1 query development

---

## LL-020 — T-15: Rate limiting in Workers uses in-memory Map per instance

**Task**: T-15
**Categoria**: pattern
**Scoperta**: Cloudflare Workers are stateless and can have multiple instances. In-memory rate limiting using Map works per worker instance, not globally. For production distributed rate limiting, use Durable Objects or KV store. For simple use cases, in-memory per-instance limiting is acceptable and has zero latency overhead.
**Applicazione**: The rate limit middleware uses `Map<clientId, {count, resetAt}>` stored in module scope. This persists across requests to the same worker instance but resets when the instance is terminated or a new instance handles the request. For strict global rate limiting, implement using Durable Objects with a counter or use Cloudflare's Rate Limiting API.
**Rilevante for task**: T-15 (Cloudflare Worker), future rate limiting improvements

---

## LL-021 — T-15: TypeScript strict mode configuration for Cloudflare Workers

**Task**: T-15
**Categoria**: compatibility
**Scoperta**: Cloudflare Workers TypeScript projects should use strict mode with `noUncheckedIndexedAccess` and `exactOptionalPropertyTypes` enabled (consistent with CLAUDE.md requirements). The target should be ES2022 (Workers support modern JavaScript), module should be ES2022, and `@cloudflare/workers-types` provides the necessary type definitions for D1, KV, R2, etc.
**Applicazione**: Use tsconfig.json with: `"target": "ES2022"`, `"module": "ES2022"`, `"lib": ["ES2022"]`, `"moduleResolution": "bundler"`, `"strict": true`, `"noUncheckedIndexedAccess": true`, `"exactOptionalPropertyTypes": true`. Include `"@cloudflare/workers-types"` in types array. This ensures maximum type safety while leveraging modern JavaScript features available in Workers runtime.
**Rilevante for task**: T-15 (Cloudflare Worker), any future TypeScript configuration

---

## LL-017 — T-02: IBKR TWS API requires dedicated message processor thread

**Task**: T-02
**Categoria**: ibkr
**Scoperta**: IBKR TWS API (IBApi library) uses a callback-based architecture with EWrapper interface. The EClient does not process messages automatically. A dedicated background thread must run EReader.processMsgs() in a loop, waiting on EReaderSignal. Without this thread, callbacks never fire.
**Applicazione**: Pattern: Create EReaderMonitorSignal, create EReader(client, signal), call reader.Start(), then spawn background thread that loops: signal.waitForSignal() → reader.processMsgs(). Thread must run while client.IsConnected(). Stop thread on disconnect by signaling and joining with timeout.
**Rilevante for task**: T-02, T-05, any task using IBKR API

---

## LL-018 — T-02: IBKR connection state must be tracked separately from IsConnected

**Task**: T-02
**Categoria**: ibkr
**Scoperta**: EClient.IsConnected() returns socket connection status, but does not reflect application-level connection lifecycle (connecting, connected, error). For robust reconnection logic, maintain separate ConnectionState enum (Disconnected, Connecting, Connected, Error) and update it based on callbacks (connectAck, connectionClosed, error codes 1100/1101/1102).
**Applicazione**: Use ConnectionState enum to drive reconnection logic. IsConnected() only checks socket. connectAck() callback confirms handshake. Error codes 1100/1300 trigger Disconnected state. Expose ConnectionState to callers via property and event.
**Rilevante for task**: T-02, T-05, any task with connection state machines

---

## LL-019 — T-02: IBKR error codes 2104/2106/2158 are informational noise

**Task**: T-02
**Categoria**: ibkr
**Scoperta**: IBKR sends error() callbacks with codes 2104, 2106, 2158 on every connection for market data farm status. These are informational messages, not errors. Logging them as errors creates noise. Other codes (1100, 1101, 1102, 1300) are critical connection events.
**Applicazione**: Filter error codes in error() callback. Log 2104/2106/2158 as Debug. Log 1100/1300 (connection lost) as Warning and trigger reconnect. Log 1101/1102 (connection restored) as Info and verify data. All other errors log as Error with full context (id, code, message).
**Rilevante for task**: T-02, T-05, any task handling IBKR errors

---

## LL-020 — T-02: IBKR safety requires multi-layer port and mode validation

**Task**: T-02
**Categoria**: ibkr
**Scoperta**: To prevent accidental live trading, validation must occur at multiple layers: 1) Configuration validation (Validate() method) rejects ports 7496/4002 and TradingMode.Live. 2) Default values (TradingMode.Paper = 0, default port 7497) ensure safe uninitialized state. 3) Immutable config (record with init) prevents mutation after creation.
**Applicazione**: Always validate config before connection. Make config immutable (record type). Use safety-first defaults (Paper mode = 0). Document safety rules in XML comments. Test all validation rules (live ports, live mode, invalid ports).
**Rilevante for task**: T-02, T-05, all tasks with risk controls

---

## LL-021 — T-02: IBKR reconnection must use exponential backoff with cap

**Task**: T-02
**Categoria**: ibkr
**Scoperta**: When IBKR connection fails (network issue, TWS restart), immediate retry hammers the server. Exponential backoff (5s → 10s → 20s → 40s...) reduces load and gives TWS time to recover. Without a cap, delay grows unbounded (2^20 = 1M seconds). Cap at reasonable max (300s = 5min).
**Applicazione**: Implement retry loop: delay starts at ReconnectInitialDelaySeconds (default 5). On failure, double delay: delay = Math.Min(delay * 2, ReconnectMaxDelaySeconds). Configurable MaxReconnectAttempts (0 = infinite). Log each attempt with attempt number and next delay.
**Rilevante for task**: T-02, T-05, any task with retry logic

---

## LL-022 — T-02: IBApi NuGet package version 10.19.2 is stable for .NET 8

**Task**: T-02
**Categoria**: compatibility
**Scoperta**: IBApi NuGet package (Interactive Brokers TWS API .NET wrapper) version 10.19.2 is compatible with .NET 8 and includes all required types (EWrapper, EClientSocket, EReader, EReaderSignal, Contract, Order). This version aligns with TWS API version 10.19 (released 2023).
**Applicazione**: Use IBApi 10.19.2 as package reference in .csproj. All skill-ibkr-api.md examples work with this version. If upgrading, check IB release notes for breaking changes in EWrapper interface (new required callbacks).
**Rilevante for task**: T-02, T-05, all tasks using IBKR API

---

## LL-023 — T-12: exactOptionalPropertyTypes requires explicit undefined union

**Task**: T-12
**Categoria**: compatibility
**Scoperta**: TypeScript's `exactOptionalPropertyTypes: true` (required by CLAUDE.md) enforces that optional properties must explicitly include `| undefined` in their type definition. Writing `symbol?: string` is not sufficient when setting state to `{ symbol: undefined }`; it must be `symbol?: string | undefined`.
**Applicazione**: For Zustand stores with optional state properties, use explicit `| undefined` union. For interface properties that can be explicitly set to undefined, add `| undefined`. This applies to all optional properties in strict mode. Example: `interface Filters { symbol?: string | undefined }` instead of `symbol?: string`.
**Rilevante per task**: All TypeScript tasks using strict mode with exactOptionalPropertyTypes (T-06 onwards, any dashboard work)

---

## LL-024 — T-12: Zustand store selectors for derived state

**Task**: T-12
**Categoria**: pattern
**Scoperta**: When Zustand store state needs to be consumed as a different type (e.g., extracting only filter fields from a larger store), use a selector function or getter method. This is cleaner than extracting individual fields in the consuming component.
**Applicazione**: Add getter methods to store for common derived state: `getFilters: () => PositionFilters`. Use selectors in components: `const getFilters = usePositionFilterStore(state => state.getFilters)`. This keeps type transformations in the store, not scattered across components.
**Rilevante per task**: All tasks using Zustand (T-06, T-12, future dashboard components)

---

## LL-025 — T-12: React Query with client-side filtering for mock data

**Task**: T-12
**Categoria**: pattern
**Scoperta**: When implementing features with mock data before API is ready, apply filters in the fetch function rather than the component. This keeps the component code identical whether using mock or real API. The queryKey includes filters, so React Query properly caches filtered results.
**Applicazione**: Include filters in queryKey: `queryKey: ['positions', filters]`. Apply filtering logic inside queryFn (in mock, server does it for real API). Recalculate aggregates (summary) after filtering in mock. Component remains unchanged when switching from mock to real API.
**Rilevante for task**: All dashboard data fetching tasks (T-12, future widgets)

---

## LL-026 — T-03: PerformanceCounter first read returns 0 baseline

**Task**: T-03
**Categoria**: pattern
**Scoperta**: Windows PerformanceCounter for CPU (Processor % Processor Time) requires a baseline sample. The first call to NextValue() always returns 0. Subsequent calls return accurate values. This is documented Windows behavior. In production, background services that call CollectAsync repeatedly automatically establish the baseline after the first cycle.
**Applicazione**: Always call NextValue() once during PerformanceCounter initialization to establish baseline, discard the result. Document in XML comments that first CollectAsync may return 0 for CPU. For testing, make two calls with a delay between them to get accurate readings. Pattern: `_ = _cpuCounter.NextValue();` in constructor.
**Rilevante for task**: T-03, any future metrics collection on Windows

---

## LL-027 — T-03: GC.GetGCMemoryInfo provides total physical memory

**Task**: T-03
**Categoria**: pattern
**Scoperta**: To calculate RAM percentage, we need total physical memory. On .NET 8, `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` provides accurate total physical RAM. This is more reliable than parsing WMI or reading registry. PerformanceCounter "Available MBytes" gives free RAM, not total.
**Applicazione**: Use `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)` for total RAM in MB. Calculate percent as `(total - available) / total * 100`. This works cross-platform (.NET Core includes this API on Linux/Mac too). Wrap in try/catch with fallback to reasonable default (16GB) if API fails.
**Rilevante for task**: T-03, any future cross-platform metrics collection

---

## LL-028 — T-03: DriveInfo requires IsReady check before accessing properties

**Task**: T-03
**Categoria**: pattern
**Scoperta**: DriveInfo.AvailableFreeSpace throws exception if drive is not ready (network drives, removable media). Always check `drive.IsReady` before accessing size properties. This prevents crashes when monitoring drives that may be temporarily unavailable.
**Applicazione**: Pattern: `DriveInfo drive = new(path); if (!drive.IsReady) return 0; return drive.AvailableFreeSpace;`. Log warning when drive is not ready. For critical drives (C:), returning 0 is acceptable (will trigger disk space alerts if configured). Always wrap DriveInfo access in try/catch.
**Rilevante for task**: T-03, any future disk monitoring

---

## LL-029 — T-03: BackgroundService error handling must not rethrow in cycle

**Task**: T-03
**Categoria**: pattern
**Scoperta**: BackgroundService ExecuteAsync runs in a loop. If an exception is thrown from the loop body and not caught, the entire service crashes. For monitoring/heartbeat services, individual cycle failures should be logged but NOT crash the service. The service must retry on the next cycle.
**Applicazione**: Wrap cycle logic in try/catch. Log errors but do NOT rethrow (except OperationCanceledException for shutdown). Pattern documented in skill-dotnet.md. Add comment: `// Do NOT rethrow - worker must survive errors and retry on next cycle`. Test with injected failures to verify service continues running.
**Rilevante for task**: T-03, all BackgroundService implementations (T-05, T-07, T-09, etc.)

---

## LL-030 — T-03: IDisposable for PerformanceCounter resource cleanup

**Task**: T-03
**Categoria**: pattern
**Scoperta**: PerformanceCounter holds unmanaged resources (system handles). Must implement IDisposable to release them. Without disposal, handles leak and eventually exhaust system resources. Register collector as Singleton in DI (not Transient) to avoid creating multiple counter instances.
**Applicazione**: Implement IDisposable on classes holding PerformanceCounter. Dispose all counters in Dispose method. Set flag to prevent use-after-dispose. Register as Singleton in DI to ensure single instance and proper disposal on shutdown. Pattern: `private bool _disposed; public void Dispose() { if (_disposed) return; _counter?.Dispose(); _disposed = true; }`.
**Rilevante for task**: T-03, any class holding unmanaged resources

---

## LL-026 — T-13: Settings persistence with Zustand partialize

**Task**: T-13
**Categoria**: pattern
**Scoperta**: When creating a settings store with Zustand persist middleware, use the `partialize` option to exclude action functions from localStorage. Without partialize, the store tries to serialize functions which causes errors. The pattern is: `partialize: (state) => { const { updateSettings, resetSettings, validateSettings, ...settings } = state; return settings; }`
**Applicazione**: Always use partialize in Zustand persist stores to explicitly control what gets persisted. Exclude functions, computed values, and transient state. Only persist plain data that should survive page reloads. This is especially important with TypeScript strict mode where function serialization failures are caught early.
**Rilevante per task**: All dashboard tasks using Zustand with persist (T-06, T-12, T-13, future settings/preferences)

---

## LL-027 — T-13: Form validation with inline error display

**Task**: T-13
**Categoria**: pattern
**Scoperta**: Settings forms benefit from inline validation errors displayed next to each field, plus a summary section at the bottom. The pattern: validation function returns `{ valid: boolean, errors: string[] }`, individual fields check if their error exists in the array using `.find()`, and a summary div shows all errors together. This provides both immediate field-level feedback and overall validation state.
**Applicazione**: For multi-section forms, create a validation function that returns an array of error messages. Pass `error={validationErrors.find(err => err.includes('keyword'))}` to Input/Select components. Display full error list in a summary section. This pattern works well with `exactOptionalPropertyTypes` because `.find()` correctly returns `string | undefined`.
**Rilevante per task**: All dashboard forms and settings pages (T-13, future configuration UIs)

---

## LL-028 — T-13: Toast notifications without external dependencies

**Task**: T-13
**Categoria**: pattern
**Scoperta**: Simple toast notifications can be implemented without external libraries using Zustand for state management and Tailwind for styling. Create a toastStore with `addToast(type, message, duration)` and `removeToast(id)` actions, use setTimeout for auto-dismiss, and render toasts in a fixed positioned container. The key insight: generate unique IDs using counter + timestamp to avoid collisions.
**Applicazione**: For lightweight toast needs, use the pattern in `stores/toastStore.ts` and `components/ui/Toast.tsx`. Provides success, error, info, warning variants. Auto-dismisses after configurable duration. Uses Tailwind animation classes (`animate-in slide-in-from-right-full`) for smooth entry. No dependencies on react-hot-toast or similar.
**Rilevante per task**: All dashboard tasks needing user feedback (T-13, future data mutation pages)

---

## LL-029 — T-13: Theme switching integration with existing UI store

**Task**: T-13
**Categoria**: pattern
**Scoperta**: When adding a Settings page that controls theme (which is already managed by uiStore), don't duplicate theme state in settingsStore. Instead, import and use the existing uiStore's theme and setTheme directly in the Settings component. This maintains single source of truth and ensures theme changes propagate correctly to all components.
**Applicazione**: When settings control UI state that's already managed elsewhere, import the existing store rather than duplicating state. In Settings page: `const { theme, setTheme } = useUiStore()` and call `setTheme(newTheme)` directly. This prevents state synchronization bugs and keeps stores focused on their domain.
**Rilevante per task**: All dashboard tasks with cross-cutting concerns (theme, auth state, global flags)

---

## LL-030 — T-13: Refresh interval configuration in milliseconds vs seconds

**Task**: T-13
**Categoria**: pattern
**Scoperta**: For user-facing configuration of refresh intervals, store values in milliseconds (for programmatic use with setInterval/React Query) but display and accept input in seconds (easier for users). Convert on the boundaries: `value={settings.refreshIntervalPositions / 1000}` for display, `onChange={(e) => settings.updateSettings({ refreshIntervalPositions: Number(e.target.value) * 1000 })}` for updates.
**Applicazione**: Store timing values in milliseconds for consistency with JavaScript timing APIs. In UI, use seconds (min="5" step="5") for better UX. Always convert at the component boundary, not in the store. This pattern keeps store data in standard units while presenting user-friendly values.
**Rilevante per task**: All tasks with timing configuration (polling intervals, timeouts, delays)

---
## LL-030 — T-07: Badge component variant mapping for severity levels

**Task**: T-07
**Categoria**: pattern
**Scoperta**: The Badge component uses specific variant names ('default', 'success', 'warning', 'danger') that don't map 1:1 to common severity levels ('info', 'warning', 'error', 'critical'). When building alert or notification systems, a mapping function is required to convert severity to badge variant.
**Applicazione**: Create a mapping function like `getSeverityVariant(severity: AlertSeverity): BadgeVariant` that maps: 'critical' → 'danger', 'error' → 'danger', 'warning' → 'warning', 'info' → 'default'. This ensures consistent visual representation across all alert components (cards, tables, summaries).
**Rilevante per task**: T-07 (alerts), any future notification or status badge implementations

---

## LL-031 — T-07: exactOptionalPropertyTypes requires conditional prop spreading

**Task**: T-07
**Categoria**: compatibility
**Scoperta**: With TypeScript's `exactOptionalPropertyTypes: true` (required by CLAUDE.md), passing `prop={value | undefined}` directly fails type checking even when the prop is declared as `prop?: Type | undefined`. TypeScript enforces that optional properties can only be omitted, not explicitly set to undefined.
**Applicazione**: Use conditional prop spreading: `{...(condition && { prop: value })}` instead of `prop={condition ? value : undefined}`. This pattern only adds the prop to the object when the value exists, satisfying exactOptionalPropertyTypes. Example: `{...(error && { error })}` instead of `error={error}` where error is `string | undefined`.
**Rilevante per task**: All TypeScript tasks with strict mode (T-06 onwards, any component with optional props)

---

## LL-032 — T-07: Alert system multi-level filtering architecture

**Task**: T-07
**Categoria**: pattern
**Scoperta**: Alerts require more complex filtering than positions (severity, type, status, search text, date range). Implementing all filters in a Zustand store with a `getFilters()` method creates a clean separation between filter state and data fetching. React Query's queryKey includes all filters, ensuring proper cache invalidation when filters change.
**Applicazione**: Store each filter field separately in Zustand (severity, type, status, search, dateFrom, dateTo). Provide individual setters and a clearFilters() method (that preserves defaults like status='active'). Use getFilters() to extract a clean filter object for React Query. Apply all filters in the queryFn for mock data, calculating accurate summaries after filtering.
**Rilevante per task**: T-07 (alerts), any future complex filtering UIs (logs, events, etc.)

---

## LL-033 — T-07: React Query mutation for optimistic UI updates

**Task**: T-07
**Categoria**: pattern
**Scoperta**: Using React Query's `useMutation` for actions like "resolve alert" provides automatic loading states, error handling, and cache invalidation. The mutation can optimistically update the UI immediately, then refetch on success to ensure consistency with the server.
**Applicazione**: Create a mutation hook like `useResolveAlert()` that returns `mutate()` function. Call `queryClient.invalidateQueries({ queryKey: ['alerts'] })` in `onSuccess` to refetch all alert queries. Pass the mutation to components as a callback: `onResolve={(id, resolved) => mutation.mutate({id, resolved})}`. This keeps components pure and testable.
**Rilevante per task**: T-07 (alerts), any future action-based UI (order placement, position adjustments, etc.)

---

## LL-034 — T-20: Card component wrapper pattern for click handlers

**Task**: T-20
**Categoria**: pattern
**Scoperta**: The UI Card component doesn't accept onClick or other event handlers directly. When a card needs to be clickable, wrap it in a div with the event handler rather than trying to pass onClick to Card props. This maintains clean separation between presentational components (Card) and interactive behavior.
**Applicazione**: Pattern: `<div onClick={handleClick} className="cursor-pointer"><Card className="hover:shadow-lg">...</Card></div>`. Apply hover effects on the Card itself via className. This works better than trying to extend Card's prop interface with every possible HTML event handler.
**Rilevante per task**: T-20 (campaigns), any dashboard page with clickable cards (T-12 positions, T-07 alerts)

---

## LL-035 — T-20: Select component requires options array prop

**Task**: T-20
**Categoria**: compatibility
**Scoperta**: The Select UI component uses a controlled pattern with an `options` array prop rather than accepting children `<option>` elements. The signature is `Select({ options: SelectOption[], onChange: (value: string) => void })` where SelectOption is `{ value: string, label: string }`. This differs from native HTML select elements.
**Applicazione**: Instead of `<Select>{options.map(opt => <option>...)}</Select>`, use `<Select options={options} onChange={(value) => ...} />`. The onChange receives the selected value string directly, not an event object. Map your data to the SelectOption format before passing to Select.
**Rilevante per task**: T-20 (campaigns), any dashboard form using Select (T-12 filters, T-13 settings)

---

## LL-034 — T-16: Audit-before-submit pattern for financial transactions

**Task**: T-16
**Categoria**: pattern
**Scoperta**: For financial transactions (orders, trades), logging to database BEFORE submitting to external API (IBKR) creates complete audit trail. Even if submission fails, rejected, or times out, the attempt is recorded with full context. This is critical for regulatory compliance and debugging.
**Applicazione**: Pattern: 1) Validate request, 2) Generate internal ID, 3) Log to DB with status=PendingSubmit, 4) Submit to external API, 5) Update status based on result. Never submit without logging first. Use database transactions to ensure atomicity.
**Rilevante per task**: T-16 (order placement), any future financial transaction logging

---

## LL-035 — T-16: Circuit breaker pattern for trading systems

**Task**: T-16
**Categoria**: pattern
**Scoperta**: Circuit breaker prevents cascading failures in trading systems. Track failures in rolling time window (e.g., 3 failures in 60 minutes). When threshold reached, block ALL new operations for cooldown period (e.g., 30 minutes). This prevents strategy bugs from draining account or overwhelming broker API.
**Applicazione**: Implement with: 1) Thread-safe state (lock-based), 2) Failure counter query from DB (source of truth), 3) Auto-trip when threshold reached, 4) Auto-reset after cooldown, 5) Manual reset capability (admin operation), 6) Critical logging when trips. Test with deliberately failing operations.
**Rilevante per task**: T-16 (order placement), any future risk management components

---

## LL-036 — T-16: Multi-layer safety validation for order placement

**Task**: T-16
**Categoria**: pattern
**Scoperta**: Single safety check is insufficient for trading systems. Implement defense-in-depth with multiple independent layers: 1) Request validation (schema), 2) Trading mode check (paper only), 3) Position size limits, 4) Position value limits, 5) Account balance minimum, 6) Risk % of account, 7) Circuit breaker check. Each layer logs and fails independently.
**Applicazione**: Validate in order from cheapest to most expensive check. Fail fast on any violation. Log all rejections for analysis. Use immutable config (record type) validated at startup. Make safety violations CRITICAL log level. Never allow bypassing safety checks.
**Rilevante per task**: T-16 (order placement), all future trading operations

---

## LL-037 — T-16: Cached account balance for performance

**Task**: T-16
**Categoria**: performance
**Scoperta**: Querying account balance on every order placement adds latency and increases broker API load. Cache balance in memory, updated periodically by background service (e.g., every 30 seconds). Use thread-safe access (lock or Interlocked). Acceptable staleness for safety checks (conservative - rejects orders if balance dropped).
**Applicazione**: Pattern: Background service queries balance via IBKR RequestAccountSummary, updates cache. OrderPlacer reads from cache with lock. If cache is zero/stale, use conservative default or reject. Log cache updates at Debug level. Balance reconciliation happens via background service, not in hot path.
**Rilevante per task**: T-16 (order placement), any high-frequency operations using account state

---

## LL-038 — T-16: Order lifecycle state machine completeness

**Task**: T-16
**Categoria**: pattern
**Scoperta**: Order lifecycle has 9+ distinct states: ValidationFailed, PendingSubmit, Submitted, Active, PartiallyFilled, Filled, Cancelled, Rejected, Failed. Each state has different meaning for reconciliation and monitoring. Use enum with explicit values. Track state transitions in DB. Never skip states (e.g., PendingSubmit → Submitted → Filled, not PendingSubmit → Filled).
**Applicazione**: Define comprehensive OrderStatus enum. Log every state transition with timestamp. Query active orders = statuses IN (PendingSubmit, Submitted, Active, PartiallyFilled). Terminal states = (Filled, Cancelled, Rejected, Failed). PartiallyFilled requires special handling (can still fill more or get cancelled).
**Rilevante per task**: T-16 (order placement), T-17 (order status tracking), reconciliation tasks

---

## LL-039 — T-16: Mock external APIs with event simulation

**Task**: T-16
**Categoria**: testing
**Scoperta**: IBKR API uses async callbacks (events). Mock must simulate this behavior for realistic tests. Pattern: PlaceOrder() returns immediately (sync), then fires OrderStatusChanged event asynchronously (Task.Run + small delay). This tests race conditions and event handling properly.
**Applicazione**: In MockIbkrClient: methods return sync, spawn background Task to fire events after delay (10-50ms). Provide test helpers to manually trigger events (SimulateOrderFill, SimulateOrderRejection). Track all calls in lists for assertions. Make failure modes configurable (ShouldPlaceOrderSucceed property).
**Rilevante per task**: T-16 (order placement tests), any task testing async callback-based APIs

---

## LL-040 — T-16: Order tracking table schema design

**Task**: T-16
**Categoria**: pattern
**Scoperta**: Order tracking needs separate table from execution_log. order_tracking = one row per order (lifecycle). execution_log = one row per fill (can be multiple for same order if partially filled). order_tracking has status, timestamps, metadata. execution_log has price, quantity, commission. Link via order_id.
**Applicazione**: Schema: order_tracking (order_id PK, ibkr_order_id, status, created_at, submitted_at, completed_at). execution_log (execution_id PK, order_id FK, fill_price, quantity, commission). Index order_tracking.ibkr_order_id for callback lookups. Index execution_log.order_id for fill history queries.
**Rilevante per task**: T-16 (order placement), T-17 (execution tracking), reconciliation

---

## LL-034 — T-08: Alert persistence and outbox sync pattern

**Task**: T-08
**Categoria**: pattern
**Scoperta**: Alerts require dual persistence: immediate write to alert_history (for audit/query) and async queue to sync_outbox (for notification). The AlertRepository handles local persistence while OutboxSyncWorker handles eventual delivery to Cloudflare. This separates concerns: alerts are always recorded even if remote sync fails.
**Applicazione**: When raising an alert, call AlertRepository.InsertAsync() first (fails fast if DB issue), then OutboxRepository.InsertAsync() to queue for sync. The outbox entry payload_json contains the full alert data. OutboxSyncWorker runs independently and retries failed syncs with exponential backoff. This pattern works for any event that needs both local audit and remote notification.
**Rilevante per task**: T-08, T-10 (alert generation), any task that raises alerts or publishes events

---

## LL-035 — T-08: HttpClient in BackgroundService lifecycle

**Task**: T-08
**Categoria**: pattern
**Scoperta**: BackgroundService workers that make HTTP calls should use IHttpClientFactory to create HttpClient instances. This ensures proper connection pooling and DNS refresh. The worker should store the HttpClient in a field (created via factory in constructor) and dispose it in Dispose() override. Do not create HttpClient per request (causes socket exhaustion).
**Applicazione**: Inject IHttpClientFactory in constructor, call CreateClient() once in constructor, store in field, configure headers/timeout. Dispose client in override Dispose(). Pattern: `_httpClient = httpClientFactory.CreateClient(); _httpClient.Timeout = TimeSpan.FromSeconds(30);`. Override Dispose: `_httpClient?.Dispose(); base.Dispose();`.
**Rilevante per task**: T-08, any BackgroundService that makes HTTP requests (future webhook workers, external API polling)

---

## LL-036 — T-08: Exponential backoff calculation with cap

**Task**: T-08
**Categoria**: pattern
**Scoperta**: Exponential backoff for retry logic uses formula: delay = min(initial * 2^retry_count, max_delay). Without a cap, delay grows unbounded (2^20 = 1M seconds). With cap (e.g., 300s), retries continue but at reasonable intervals. This prevents infinite waiting while still backing off during extended outages.
**Applicazione**: Calculate retry delay: `int delaySeconds = initialDelay * (int)Math.Pow(2, retryCount); delaySeconds = Math.Min(delaySeconds, maxDelay);`. Store next_retry_at as `DateTime.UtcNow.AddSeconds(delaySeconds)`. Configure initial (5s) and max (300s) in appsettings.json. Example progression: 5s, 10s, 20s, 40s, 80s, 160s, 300s, 300s, 300s...
**Rilevante per task**: T-08, any retry logic (HTTP requests, IBKR reconnection, file IO)

---

## LL-037 — T-08: JSON envelope pattern for HTTP event payloads

**Task**: T-08
**Categoria**: pattern
**Scoperta**: When sending events to an API that processes multiple event types, wrap the payload in a JSON envelope with metadata. The envelope includes: event_id (for deduplication), event_type (for routing), payload (the actual event data), dedupe_key (optional), created_at (timestamp). This allows the receiving API to route, validate, and dedupe without parsing the inner payload.
**Applicazione**: Structure: `{ "event_id": "guid", "event_type": "heartbeat_updated", "payload": {...}, "dedupe_key": "heartbeat:service_name", "created_at": "2026-04-05T10:00:00Z" }`. Serialize with JsonSerializer using SnakeCaseLower naming policy to match Cloudflare Worker conventions. Parse inner payload from JSON string: `JsonDocument.Parse(entry.PayloadJson).RootElement`.
**Rilevante per task**: T-08, T-09 (Cloudflare Worker ingest), any event-driven API integration

---

## LL-038 — T-08: BackgroundService startup delay pattern

---

## LL-039 — T-17: Campaign state machine with immutable record transitions

**Task**: T-17
**Categoria**: pattern
**Scoperta**: Campaign lifecycle is modeled as an immutable record with state transition methods (Activate(), Close()). Each transition returns a new Campaign instance with updated state and timestamps. This enforces valid state transitions at compile time and prevents accidental state mutation. State machine: Open → Active → Closed (terminal).
**Applicazione**: Use immutable records for entities with well-defined state transitions. Provide transition methods that validate current state and return new instance: `public Campaign Activate() { if (State != CampaignState.Open) throw ...; return this with { State = Active, ActivatedAt = UtcNow }; }`. This pattern works for orders, positions, alerts, any entity with lifecycle states.
**Rilevante per task**: T-17 (Campaign), T-16 (Order placement with OrderStatus transitions), any domain entity with state machines

---

## LL-040 — T-17: Repository persistence with embedded JSON serialization

**Task**: T-17
**Categoria**: pattern
**Scoperta**: Campaign metadata (including full StrategyDefinition) is serialized to JSON and stored in strategy_state.state_json column. This allows complex domain objects to be persisted without schema migrations. On load, JSON is deserialized back into strongly-typed Campaign record. This pattern works when: 1) object structure changes frequently, 2) queries don't need to filter on nested fields, 3) full object retrieval is common use case.
**Applicazione**: Create internal DTO class (CampaignMetadata) with all fields as strings/primitives. Serialize domain object to DTO, then DTO to JSON string. Store in TEXT column. On read, deserialize JSON → DTO → domain object. Handle null values for optional fields. Trade-off: cannot query nested fields efficiently, but avoids complex JOIN queries and schema churn.
**Rilevante per task**: T-17 (Campaign), T-04 (Strategy storage), any complex object persistence where query flexibility is not required

---

## LL-041 — T-17: Service orchestration with dependency injection interfaces

**Task**: T-17
**Categoria**: pattern
**Scoperta**: CampaignManager orchestrates multiple services (IStrategyLoader, IOrderPlacer, ICampaignRepository) without knowing their implementations. This allows unit testing with mocks and supports future swapping of implementations (e.g., replacing OrderPlacerStub with real IBKR integration). The manager focuses purely on business logic: check conditions, transition states, coordinate calls.
**Applicazione**: Manager services should depend only on interfaces, never concrete implementations. Inject all dependencies via constructor. Use Moq for unit testing (mock all interfaces). Keep manager logic pure: no direct DB calls, no HTTP calls, no IBKR API calls—delegate to injected services. Manager validates inputs, orchestrates sequence, handles errors, logs outcomes.
**Rilevante per task**: T-17 (Campaign Manager), T-05 (Supervisor orchestration), any high-level service coordination

---

## LL-042 — T-17: Exit condition evaluation with multiple triggers

**Task**: T-17
**Categoria**: pattern
**Scoperta**: Exit conditions (profit target, stop loss, max days, time exit) are evaluated in priority order using early return pattern. First matching condition wins. Returns tuple (shouldExit: bool, reason: string?). This makes the logic testable (each condition independently verifiable) and extensible (add new conditions without refactoring existing ones).
**Applicazione**: Evaluate conditions from highest to lowest priority. Use early return on first match: `if (pnl >= profitTarget) return (true, "profit_target");`. Return (false, null) if no conditions met. Caller uses tuple deconstruction: `(bool shouldExit, string? reason) = CheckExitConditions(...)`. Log each evaluation for debugging. This pattern scales to complex condition chains.
**Rilevante for task**: T-17 (Campaign exit logic), T-10 (Alert condition evaluation), any multi-condition decision logic

---

## LL-043 — T-17: Stub pattern for future dependencies

**Task**: T-17
**Categoria**: pattern
**Scoperta**: When a service depends on a component not yet implemented (IOrderPlacer from T-16), create a stub implementation that returns mock data. The stub implements the full interface contract, logs calls, returns plausible test values. This unblocks current task testing while future task implements the real version. Replace stub with real implementation via DI configuration.
**Applicazione**: Create XxxStub class implementing IXxx interface. Log all method calls with parameters. Return deterministic or random mock data (not null). Use stub in unit tests and DI registration during development: `services.AddScoped<IOrderPlacer, OrderPlacerStub>()`. Switch to real impl when ready: `services.AddScoped<IOrderPlacer, OrderPlacerReal>()`. No code changes needed in consumers.
**Rilevante for task**: T-17 (OrderPlacer stub), any task with external dependencies not yet complete

---

## LL-038 — T-08: BackgroundService startup delay pattern

**Task**: T-08
**Categoria**: pattern
**Scoperta**: BackgroundService workers that depend on other services (database, external APIs) should delay execution on startup to allow dependencies to initialize. Add a configurable delay (e.g., 5 seconds) at the start of ExecuteAsync before entering the main loop. This prevents race conditions where the worker tries to use a service that's still starting up.
**Applicazione**: Pattern: `await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);` as first line in ExecuteAsync, before the while loop. For critical workers, make delay configurable via appsettings. For non-critical workers, hardcode 5s. Log at Information level when worker starts to confirm initialization.
**Rilevante per task**: T-08, all BackgroundService implementations (T-03, T-05, future workers)

---

## LL-039 — T-08: Repository method naming for clarity

**Task**: T-08
**Categoria**: pattern
**Scoperta**: Repository methods should clearly indicate what data they return and how they filter. Use prefixes: Get* (single result or list), Count* (aggregates), Mark*/Update* (mutations), Insert*/Delete* (CRUD). Include filter criteria in name: GetUnresolvedAsync, GetBySeverityAsync, GetUnresolvedCountsAsync. This makes intent clear at call site without reading implementation.
**Applicazione**: Follow naming pattern: `Get[Filter][Aggregate]Async`, `Mark[Action]Async`, `Insert/Delete/Update*Async`. Examples: GetUnresolvedAsync (filter by resolved_at IS NULL), GetBySeverityAsync (filter by severity), GetUnresolvedCountsAsync (aggregate count by severity). Avoid generic names like GetAlerts (what filters? what order?).
**Rilevante per task**: All repository implementations (T-01, T-02, T-03, T-08, future repositories)

---

## LL-040 — T-04: Strategy validation collects all errors before returning

**Task**: T-04
**Categoria**: pattern
**Scoperta**: Strategy validation should collect all validation errors and return them together, rather than failing fast on the first error. This provides a better developer experience when editing strategy JSON files - seeing all errors at once allows fixing multiple issues in a single iteration instead of iterative fix-rerun cycles.
**Applicazione**: Use a `List<string> errors` to accumulate validation errors. Call validation helpers that add to the list rather than throwing. At the end, check `if (errors.Count == 0) return Success() else return Failure(errors)`. Each error message should include the field path (e.g., "Position.Legs[0].StrikeValue is required when StrikeSelectionMethod is DELTA"). This pattern works for any complex configuration validation.
**Rilevante per task**: T-04 (strategy validation), T-05 (strategy execution), any configuration validation (appsettings, user input, API requests)

---

## LL-041 — T-04: Custom JSON converters for non-standard types

**Task**: T-04
**Categoria**: pattern
**Scoperta**: System.Text.Json does not have built-in converters for TimeOnly (introduced in .NET 6) or arrays of enums parsed from string names. Custom JsonConverter<T> implementations are needed. TimeOnlyJsonConverter handles "HH:mm:ss" format. DayOfWeekArrayJsonConverter parses string arrays like ["Monday", "Tuesday"] into DayOfWeek[] using Enum.TryParse.
**Applicazione**: Create sealed class inheriting JsonConverter<T>. Override Read() to parse from Utf8JsonReader and Write() to serialize with Utf8JsonWriter. Add to JsonSerializerOptions.Converters collection. For TimeOnly, use TimeOnly.TryParseExact with format string. For enum arrays, iterate tokens and use Enum.TryParse with ignoreCase: true. Throw JsonException with clear message for invalid values.
**Rilevante per task**: T-04 (strategy JSON), any task that serializes/deserializes JSON with TimeOnly, DateOnly, or custom enum formats

---

## LL-042 — T-04: Strategy loader resilience with skip-on-error pattern

**Task**: T-04
**Categoria**: pattern
**Scoperta**: When loading multiple strategy files from a directory, individual file failures should not stop the entire load operation. The LoadAllStrategiesAsync method catches exceptions per file, logs a warning, and continues processing remaining files. This allows the system to start with partial strategy set rather than failing completely if one file is malformed.
**Applicazione**: Pattern: `foreach (string file in files) { try { strategy = await LoadStrategyAsync(file, ct); strategies.Add(strategy); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to load {File}, skipping", file); } }`. Return the successfully loaded strategies. Log summary: "Loaded X of Y strategies". This pattern works for any batch processing where partial success is acceptable.
**Rilevante per task**: T-04 (strategy loading), any batch file processing, configuration loading, plugin loading

---

## LL-043 — T-04: Immutable domain records with required keyword

**Task**: T-04
**Categoria**: pattern
**Scoperta**: C# 11 `required` keyword combined with `init`-only properties creates self-documenting immutable domain objects. The compiler enforces that all required properties are set at construction time, eliminating null reference bugs. Optional properties use explicit `Type?` syntax. This pattern makes deserialization failures fail fast at parse time rather than runtime validation.
**Applicazione**: Define domain records: `public sealed record StrategyDefinition { public required string StrategyName { get; init; } public string? SourceFilePath { get; init; } }`. Required fields MUST be set in object initializer or deserialization fails. Optional fields can be omitted. Use `with` expressions for non-destructive updates. This pattern is ideal for configuration objects, DTOs, and domain entities.
**Rilevante per task**: T-04 (strategy domain), all domain models (T-01, T-02, T-03, future entities)

---

## LL-044 — T-04: Strategy type enumeration via hardcoded array

**Task**: T-04
**Categoria**: pattern
**Scoperta**: For strategy types (BullPutSpread, IronCondor, etc.), using a hardcoded string array instead of an enum allows flexibility for future extension while providing validation. The validator checks `ValidStrategyTypes.Contains(position.Type)` and provides a clear error message with the list of valid types. This is easier to extend than enum (no recompilation of dependent assemblies).
**Applicazione**: Define `private static readonly string[] ValidStrategyTypes = { "BullPutSpread", "BearCallSpread", "IronCondor", ... }`. In validation, check containment and include the full list in error messages for discoverability. For stricter typing, could create a StrategyType record with static factory methods, but array is simpler for initial implementation.
**Rilevante per task**: T-04 (strategy validation), T-08 (strategy selection), any extensible enumeration pattern

---

## LL-045 — T-14: Black-Scholes Greeks calculation with normal distribution approximation

**Task**: T-14
**Categoria**: pattern
**Scoperta**: Implementing Black-Scholes option Greeks requires accurate normal distribution CDF and PDF functions. The Abramowitz & Stegun rational approximation for CDF provides |error| < 7.5e-8, which is sufficient for financial calculations. Key implementation details:
1. Normal CDF uses A&S formula with polynomial approximation
2. Normal PDF is simple: (1/√2π) × e^(-x²/2)
3. Theta must be converted to per-day by dividing by 365
4. Vega result is per 1% vol change (divide by 100)
5. Input validation critical: zero/negative prices, expired options return GreeksData.Empty
6. Implied volatility (from IBKR) takes precedence over historical volatility
**Applicazione**: Use BlackScholesCalculator with explicit parameters. For time to expiry, use `daysToExpiry / 365.0`. For risk-free rate, use current treasury rate (e.g., 0.05 for 5%). Always pass IBKR implied volatility if available via `genericTickList="106"` in market data requests. The calculator handles all edge cases and returns Empty for invalid inputs rather than throwing.
**Rilevante per task**: T-14 (Greeks calculation), any future options analytics, risk management, position sizing based on Greeks

---

## LL-037 — T-10: Log file tailing with FileShare.ReadWrite

**Task**: T-10
**Categoria**: pattern
**Scoperta**: When reading log files that are actively being written by another process (like Serilog), the FileStream must be opened with FileShare.ReadWrite to allow the writer to continue appending. Without this, the file open fails with "file is in use" error. Additionally, track file position and size to detect log rotation and restart from position 0.
**Applicazione**: Open log file: `FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)`. Track last_position and last_size in database. On each read: if last_size > current_size (file rotated), restart from 0. Otherwise, seek to last_position and read new lines. Update position after successful read. This pattern works for any tail -f style file monitoring.
**Rilevante per task**: T-10 (log reader), any future log monitoring or file tailing tasks

---

## LL-038 — T-10: Regex for Serilog log level parsing

**Task**: T-10
**Categoria**: pattern
**Scoperta**: Serilog text format logs use pattern `[YYYY-MM-DD HH:MM:SS LEVEL]` at the start of each log line. Use compiled regex to extract timestamp and level: `@"^\[(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+(?<level>ERR|WRN|FTL|INF|DBG)\]"`. Compiled regex is performant for repeated matching in tight loops (thousands of log lines per cycle).
**Applicazione**: Define regex as static readonly field with RegexOptions.Compiled. Match at start of each line. Use named groups for readability: `match.Groups["level"].Value`. Map levels: FTL → Critical, ERR → Error, WRN → Warning. Ignore INF/DBG (no alerts). Handle multiline logs (stack traces): if line doesn't match pattern, it's a continuation of previous line.
**Rilevante per task**: T-10 (log parsing), any structured log parsing tasks

---

## LL-039 — T-10: Incremental log reading with position tracking

**Task**: T-10
**Categoria**: pattern
**Scoperta**: Log reader workers need to track file position to avoid reprocessing the same lines on every cycle. Store file_path, last_position, and last_size in database. On each cycle: read only new data from last_position to current file size. This is more efficient than re-reading entire file and prevents duplicate alert creation for the same error.
**Applicazione**: Before reading, query database for last position. If not found (first read), start from position 0. If found, seek to last_position. Read until end of file. After processing, save new position and size. Handle edge cases: file smaller than last_size (rotation), position beyond file size (corruption - restart from 0). Use UPSERT (INSERT OR REPLACE) for idempotent state updates.
**Rilevante per task**: T-10 (log reader), any incremental file processing

---

## LL-040 — T-10: Alert creation from log entries pattern

**Task**: T-10
**Categoria**: pattern
**Scoperta**: When creating alerts from log entries, store the original log line in details_json for debugging. Extract a clean message (without timestamp/level prefix) for the alert message field. Set source_service to identify which service produced the log (e.g., "OptionsExecutionService"). Use alert_type "LogError" to differentiate from other alert types (heartbeat, threshold, etc.).
**Applicazione**: Parse log line with regex. Extract level → map to AlertSeverity. Extract message portion (after [timestamp LEVEL] prefix). Create AlertRecord with: AlertId = GUID, AlertType = "LogError", Severity = mapped severity, Message = extracted message, DetailsJson = JSON with {LogFile, Timestamp, FullLine}, SourceService = "OptionsExecutionService", CreatedAt = UTC now. This pattern provides full traceability from alert back to original log entry.
**Rilevante per task**: T-10 (log reader), any alert generation from external events

---

## LL-041 — T-05: Telegram.Bot NuGet package for .NET 8

**Task**: T-05
**Categoria**: compatibility
**Scoperta**: Telegram.Bot NuGet package version 19.0.0 is fully compatible with .NET 8 and provides strongly-typed API for Telegram Bot API. The package uses async/await throughout and supports markdown formatting (ParseMode.Markdown), chat IDs (user or group), and cancellation tokens.
**Applicazione**: Use `Telegram.Bot` v19.0.0 package. Create TelegramBotClient with bot token from configuration. Use `SendTextMessageAsync(chatId, text, parseMode, cancellationToken)` for sending messages. Handle exceptions for network failures (retry with exponential backoff). Track Message.MessageId in responses for audit logging.
**Rilevante per task**: T-05, any future Telegram integration tasks

---

## LL-042 — T-05: ConcurrentQueue for thread-safe alert queueing

**Task**: T-05
**Categoria**: pattern
**Scoperta**: ConcurrentQueue<T> provides lock-free thread-safe queueing for alerts. Multiple services can call QueueAlertAsync() concurrently without synchronization. Background worker processes queue using TryDequeue() which is non-blocking and safe for concurrent access. No need for explicit locks or semaphores.
**Applicazione**: Use ConcurrentQueue<TelegramAlert> for alert queue. Call Enqueue() from any thread. Background worker loop: `while (queue.TryDequeue(out alert))` processes all queued items. For re-queueing failed items with retry, Enqueue() again with updated RetryCount and NextRetryAtUtc. Monitor queue size with Count property (thread-safe).
**Rilevante per task**: T-05, any future background processing with concurrent producers (log parsing, event streaming, etc.)

---

## LL-043 — T-05: Rate limiting with sliding window using ConcurrentQueue

**Task**: T-05
**Categoria**: pattern
**Scoperta**: Implementing rate limiting (e.g., 20 messages per minute) can be done efficiently with a ConcurrentQueue of timestamps. Enqueue timestamp on each send, dequeue timestamps older than window (1 minute). Queue.Count gives current rate. If at limit, wait and retry. This provides accurate sliding window rate limiting without complex timer logic.
**Applicazione**: Create `ConcurrentQueue<DateTime>` for timestamps. On each API call: 1) Clean old timestamps: `while (queue.TryPeek(out oldest) && oldest < DateTime.UtcNow.AddMinutes(-1)) queue.TryDequeue()`, 2) Check if `queue.Count >= maxRate`, if yes wait 5 seconds, 3) After successful send: `queue.Enqueue(DateTime.UtcNow)`. Works for any sliding window rate limit.
**Rilevante per task**: T-05, any future API integration with rate limits (Cloudflare, IBKR market data, external webhooks)

---

## LL-044 — T-05: Configuration validation with graceful degradation

**Task**: T-05
**Categoria**: pattern
**Scoperta**: Services that depend on external configuration (API tokens, chat IDs) should validate config on startup and gracefully degrade if invalid. Pattern: 1) Load config into immutable record, 2) Call Validate() method returning error message or null, 3) If invalid, log warning and set Enabled=false, 4) Service continues running but no-ops all operations. This prevents service crash from missing/invalid config.
**Applicazione**: Create config record with Validate() method checking required fields. In service constructor: `string? error = config.Validate(); if (error != null) { _logger.LogWarning(error); _config = _config with { Enabled = false }; }`. All service methods check `if (!_config.Enabled) return;` early. Log config warnings, not errors (allows service to run in degraded mode).
**Rilevante per task**: T-05, all services with external dependencies (IBKR, Cloudflare, email, SMS, external APIs)

---

## LL-045 — T-05: Exponential backoff for retry logic

**Task**: T-05
**Categoria**: pattern
**Scoperta**: For retry logic with increasing delays, exponential backoff (`delay = initialDelay * 2^retryCount`) prevents hammering failed service while still allowing quick recovery. Pattern: on failure, calculate next retry time as `DateTime.UtcNow.AddSeconds(RetryDelaySeconds * Math.Pow(2, retryCount))`. Re-queue item with NextRetryAtUtc. In processing loop, skip items where `DateTime.UtcNow < NextRetryAtUtc`.
**Applicazione**: Add RetryCount and NextRetryAtUtc to queued items (use immutable record with `with` syntax for updates). On failure: `item = item with { RetryCount = item.RetryCount + 1, NextRetryAtUtc = DateTime.UtcNow.AddSeconds(delay * Math.Pow(2, item.RetryCount)) }; queue.Enqueue(item);`. Set MaxRetryAttempts to prevent infinite retries. Drop items that exceed max retries with error log.
**Rilevante per task**: T-05, outbox sync (T-01), any task with network calls or external service dependencies

---

## LL-046 — T-05: Telegram markdown escaping for user content

**Task**: T-05
**Categoria**: pattern
**Scoperta**: Telegram Bot API ParseMode.Markdown requires escaping special characters (`_`, `*`, `[`, `` ` ``) in user-provided content. Without escaping, messages fail to send or render incorrectly. Pattern: create EscapeMarkdown() helper that replaces special chars with backslash-escaped versions (`_` → `\_`).
**Applicazione**: Always escape user-provided text (alert messages, details) before sending to Telegram with markdown enabled. Helper method: `text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`")`. Use escaped text in message body. Markdown formatting (bold, italic, code) should only be applied to static template parts, not user content.
**Rilevante per task**: T-05, any future messaging/notification tasks with user-generated content (email templates, SMS, webhooks)

---

## LL-047 — T-18: ConcurrentDictionary for thread-safe market data caching

**Task**: T-18
**Categoria**: pattern
**Scoperta**: When receiving asynchronous IBKR callbacks on background threads (EReader message processor), market data must be stored in thread-safe collections. ConcurrentDictionary<K, V> provides lock-free reads and atomic updates without explicit locking. Pattern: Use ConcurrentDictionary for snapshot storage by request ID, symbol-to-reqId lookup, and reqId-to-symbol reverse lookup. Update snapshots using `dict[key] = newValue` (atomic replace).
**Applicazione**: For any service that receives callbacks on background threads (market data, order status, account updates), use ConcurrentDictionary for shared state. Do not use regular Dictionary with manual locks (error-prone and slower). For request ID counters, use `lock` on a dedicated object when incrementing. For immutable snapshots, use record types with `with` syntax for updates (`snapshot = snapshot with { LastPrice = price }`).
**Rilevante per task**: T-18 (market data), T-16 (order tracking), T-02 (IBKR client state), any background service with concurrent access

---

## LL-048 — T-18: IBKR sends -1, -2, -999 for unavailable option Greeks

**Task**: T-18
**Categoria**: ibkr
**Scoperta**: When IBKR option Greeks are not available (no recent trade, illiquid contract, pre-market), the tickOptionComputation callback sends special sentinel values: IV=-1.0, delta=-10.0, gamma=-1.0, vega=-1.0, theta=-999.0, undPrice=-1.0. These must be filtered out to avoid polluting market data snapshots with invalid values.
**Applicazione**: In tickOptionComputation callback handler, validate each Greek value before updating snapshot. Use conditional logic: `ImpliedVolatility = (impliedVolatility > 0) ? impliedVolatility : currentSnapshot.ImpliedVolatility`. Delta range check: `delta > -2`. Gamma/Vega: `>= 0`. Theta: `> -999`. Preserve existing values if new value is invalid. Never store -1, -2, -999 in domain models.
**Rilevante per task**: T-18, T-14 (Greeks calculation fallback), any task that processes IBKR option data

---

## LL-049 — T-18: DTE calculation must clamp to zero minimum

**Task**: T-18
**Categoria**: pattern
**Scoperta**: Days To Expiration (DTE) for options should never be negative. If calculating `(expirationDate - today).TotalDays` for an expired option, result will be negative. This causes issues in strategy logic that expects DTE >= 0. Pattern: after calculating DTE, apply `Math.Max(0, dte)` to clamp to zero.
**Applicazione**: Always clamp DTE to zero minimum. If option is expired (expirationDate < today), return DTE=0 (not negative). This allows downstream code to safely use DTE without additional checks. For expired options, other logic (position close, stop trading) should handle cleanup, not DTE calculation.
**Rilevante per task**: T-18 (market data), T-17 (campaign manager entry conditions), T-XX (option scanner)

---

## LL-050 — T-18: Event-driven market data updates for reactive UI

**Task**: T-18
**Categoria**: pattern
**Scoperta**: Real-time market data updates require event-driven architecture to avoid polling. Pattern: expose `event EventHandler<(int RequestId, MarketDataSnapshot Snapshot)> MarketDataUpdated` on service. Raise event in tickPrice, tickSize, tickOptionComputation callbacks after updating snapshot. Subscribers receive immediate notifications without polling GetSnapshot() repeatedly.
**Applicazione**: For any real-time data service (market data, order updates, position changes), expose events for state changes. Use value tuples `(int Key, TData Value)` for event args to avoid allocating custom EventArgs classes. Invoke events with null-conditional operator: `MarketDataUpdated?.Invoke(this, (reqId, snapshot))`. Unsubscribe events in Dispose() to prevent memory leaks.
**Rilevante per task**: T-18, T-16 (order updates), T-XX (dashboard live updates), T-XX (alert triggers)

---


## LL-051 — T-11: IVTS monitoring requires multi-expiration IV data

**Task**: T-11
**Categoria**: pattern
**Scoperta**: Implied Volatility Term Structure (IVTS) analysis requires fetching IV for multiple expirations (30d, 60d, 90d, 120d) to detect curve shape. In production, this requires requesting option chains from IBKR, finding contracts closest to target DTEs, and subscribing to market data with `genericTickList="106"` (option implied volatility). Worker pattern: periodically fetch IV data, calculate metrics (IVR, slope, inversion), store snapshots, and generate alerts.
**Applicazione**: IVTS worker must wait for IBKR connection before starting monitoring cycles. Use async event-driven pattern to collect IV from callbacks (tickOptionComputation). Store snapshots with calculated metrics (IVR, slope, is_inverted) for historical analysis. Alert on threshold breaches (IVR > 80%, inverted curve, IV spike > 20%).
**Rilevante per task**: T-11 (IVTS monitor), T-XX (volatility-based strategies), T-XX (risk management)

---

## LL-052 — T-11: IVR calculation requires 52-week min/max tracking

**Task**: T-11
**Categoria**: pattern
**Scoperta**: Implied Volatility Rank (IVR) is calculated as `(Current IV - Min IV) / (Max IV - Min IV)` over a lookback period (typically 52 weeks). This requires storing historical IV snapshots and querying min/max from past year. Use average of 30d and 60d IV as "current IV" for consistency. IVR of 0.80 (80%) means current IV is at 80th percentile of its 52-week range, indicating high volatility environment.
**Applicazione**: Store iv_min_52w and iv_max_52w in each snapshot for quick access. Query pattern: `SELECT MIN((iv_30d + iv_60d) / 2.0), MAX((iv_30d + iv_60d) / 2.0) FROM ivts_snapshots WHERE symbol = ? AND timestamp_utc >= datetime('now', '-365 days')`. Handle null case (insufficient data) by returning null IVR percentile.
**Rilevante per task**: T-11, T-XX (volatility strategies), T-XX (options analytics)

---

## LL-053 — T-11: Term structure inversion detection for market stress

**Task**: T-11
**Categoria**: pattern
**Scoperta**: Inverted term structure (shorter expiry IV > longer expiry IV) is an anomaly that signals market stress or unusual conditions. Normal curve is upward sloping (longer expirations have higher IV due to uncertainty). Detection: compare adjacent expirations with threshold (e.g., 5%) to avoid false positives from small fluctuations. Alert severity: critical (inverted curve) vs warning (high IVR).
**Applicazione**: Calculate term structure slope as `(IV120d - IV30d) / 90`. Negative slope indicates inversion. Check pairs: if `IV30d > IV60d + threshold OR IV60d > IV90d + threshold OR IV90d > IV120d + threshold`, mark as inverted. Store `is_inverted` boolean in snapshot for quick filtering. Generate critical alert when detected.
**Rilevante per task**: T-11, T-XX (risk monitoring), T-XX (position sizing)

---

## LL-054 — T-11: IVTS worker should be disabled by default

**Task**: T-11
**Categoria**: pattern
**Scoperta**: IVTS monitoring requires active IBKR connection and market data subscriptions (which may incur fees). Worker should default to disabled (`Enabled: false` in appsettings.json) to avoid unintended activation in development/testing. Enable only when explicitly configured and IBKR paper/live connection is available. Worker pattern: check `Enabled` flag in ExecuteAsync and early-return if disabled.
**Applicazione**: Add `Enabled: false` to all monitoring workers' configuration sections. Log info message "Worker is disabled in configuration" and return immediately from ExecuteAsync if disabled. This allows services to run without all workers active, reducing IBKR API usage and simplifying development.
**Rilevante per task**: T-11, T-XX (all optional monitoring workers)

---

## LL-055 — T-11: Alert repository reuse for multiple alert types

**Task**: T-11
**Categoria**: pattern
**Scoperta**: The `alert_history` table (from migration 001) is designed to store all alert types (heartbeat, IVTS, position, order, etc.). No need to create separate alert tables for each feature. Use `alert_type` column to distinguish (e.g., "IvrThresholdBreach", "InvertedTermStructure", "IvtsSpike"). Store feature-specific data in `details_json` for structured querying. Filter active alerts with `WHERE resolved_at IS NULL AND alert_type LIKE 'Ivts%'`.
**Applicazione**: Reuse IAlertRepository and alert_history table for all alert types. Use consistent naming: `{Feature}{Event}` (e.g., "PositionStopLoss", "OrderRejected", "HeartbeatMissing"). Always include symbol or identifier in details_json for filtering. This avoids table proliferation and enables cross-feature alert dashboards.
**Rilevante per task**: T-11, T-XX (all features that generate alerts)

---



## LL-045 — T-09: BackgroundService workers must wait for dependencies before running

**Task**: T-09
**Categoria**: pattern
**Scoperta**: When BackgroundService workers depend on other services (e.g., CampaignMonitorWorker depends on IbkrClient being connected), add an initial delay before starting the main loop. This gives time for dependency initialization. Example: CampaignMonitorWorker waits 30 seconds for IBKR connection to establish before starting campaign monitoring.
**Applicazione**: In ExecuteAsync(), add `await Task.Delay(TimeSpan.FromSeconds(N), stoppingToken)` before the main loop if the worker depends on other background services. Document the dependency and delay reason in XML comments. Alternative: use a ready/health check mechanism if more robust coordination is needed.
**Rilevante per task**: All BackgroundService implementations with inter-service dependencies (T-09, future multi-worker scenarios)

---

## LL-046 — T-09: Service lifetimes must match state management requirements

**Task**: T-09
**Categoria**: pattern
**Scoperta**: Service lifetime (Singleton, Scoped, Transient) must be chosen based on state management requirements:
- **Singleton**: Services with stateful behavior (IbkrClient maintains connection, OrderPlacer has circuit breaker state)
- **Scoped**: Repositories and database-accessing services (prevents connection leaks, ensures proper disposal)
- **Transient**: Stateless calculators and validators (but singleton is acceptable if thread-safe)

CRITICAL: Scoped services cannot be injected into Singleton services (DI will throw at runtime). CampaignManager is scoped (depends on scoped repositories), so it cannot be injected into OrderPlacer (singleton). Solution: OrderPlacer does not directly depend on CampaignManager; CampaignMonitorWorker creates scopes and resolves CampaignManager per cycle.
**Applicazione**: When designing service graph, trace dependencies. If Singleton needs Repository, inject IDbConnectionFactory (singleton) and create connections manually. If BackgroundService (singleton) needs Scoped service, use IServiceProvider.CreateScope() per cycle. Validate lifetime compatibility during design review.
**Rilevante per task**: All tasks with complex DI graphs (T-09, future service implementations)

---

## LL-047 — T-09: Configuration validation should fail-fast at startup

**Task**: T-09
**Categoria**: pattern
**Scoperta**: Safety-critical configuration (IbkrConfig, OrderSafetyConfig) should call Validate() method in Program.cs immediately after construction, before services are registered. This ensures the service fails to start if configuration is invalid (e.g., live trading mode without explicit override, invalid ports, missing safety limits). Fail-fast is better than runtime errors after service has started.
**Applicazione**: For all config objects with business rules (not just ConnectionStrings), implement a Validate() method that throws ArgumentException with detailed message if validation fails. Call Validate() in Program.cs after config object construction: `ibkrConfig.Validate(); services.AddSingleton(ibkrConfig);`. Document validation rules in XML comments on config properties.
**Rilevante per task**: All tasks with configuration objects that have validation rules (T-09, future service configuration)

---

## LL-048 — T-09: EWrapper callback events must be wired via explicit method

**Task**: T-09
**Categoria**: ibkr
**Scoperta**: TwsCallbackHandler (EWrapper implementation) needs to raise events (OrderStatusChanged, OrderError) for order tracking. However, the handler is constructed before IbkrClient, creating a chicken-and-egg problem for constructor dependency. Solution: Add SetConnectionStateCallback() method to TwsCallbackHandler that IbkrClient calls after construction. This allows late binding of callbacks without circular dependency.
**Applicazione**: When callback handler needs to notify parent service of events, use a setter method pattern: `void SetCallback(Action<T> callback)` called by parent after construction. Store callback in private field, invoke when event occurs. Alternative: use C# events and subscribe in parent constructor. Setter pattern is more explicit and testable.
**Rilevante per task**: T-09, any task with callback-based integrations (IBKR, webhooks, external APIs)

---

## LL-049 — T-09: Background workers must not rethrow exceptions (except cancellation)

**Task**: T-09
**Categoria**: pattern
**Scoperta**: BackgroundService ExecuteAsync() runs in a background thread. If an exception is thrown from the main loop, the service stops and cannot be recovered without restarting the process. Solution: Wrap cycle logic in try/catch, log errors, but do NOT rethrow (except OperationCanceledException for graceful shutdown). The worker must survive transient errors and retry on the next cycle.
**Applicazione**: Pattern from skill-dotnet.md:
```csharp
while (!stoppingToken.IsCancellationRequested)
{
    await RunCycleAsync(stoppingToken);
    await Task.Delay(_interval, stoppingToken);
}

private async Task RunCycleAsync(CancellationToken ct)
{
    try { /* cycle logic */ }
    catch (OperationCanceledException) { /* shutdown - do not log */ }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Cycle failed. Retry in {Interval}s", _interval);
        // Do NOT rethrow - worker must survive
    }
}
```
Add explicit comment `// Do NOT rethrow - worker must survive errors` to prevent future changes that break resilience.
**Rilevante per task**: All BackgroundService implementations (T-03, T-07, T-08, T-09, future workers)

---

## LL-050 — T-09: Program.cs should use static local functions for clarity

**Task**: T-09
**Categoria**: pattern
**Scoperta**: Program.cs top-level statements can include static local functions (e.g., `static async Task RunMigrationsAsync(IServiceProvider services)`) to organize complex initialization logic. This keeps the main flow readable while encapsulating concerns like migration execution. Local functions have access to Log.Logger (from outer scope) and can be async.
**Applicazione**: For multi-step initialization (migrations, seed data, health checks), extract into static local functions at the bottom of Program.cs. Keep top-level flow linear: configure → build → migrate → run → dispose. Name functions descriptively (RunMigrationsAsync, SeedDatabaseAsync, VerifyExternalServicesAsync). Use when init logic exceeds 5-10 lines.
**Rilevante per task**: All service host implementations (T-09, future service bootstrap tasks)

---


## LL-056 — T-19: Multiple database factories for cross-database access

**Task**: T-19
**Categoria**: pattern
**Scoperta**: TradingSupervisorService needs to read data from both supervisor.db (its own database) and options.db (managed by OptionsExecutionService). Registering a single IDbConnectionFactory for both databases is not possible with standard DI. Solution: Register default IDbConnectionFactory for supervisor.db, then create PositionsRepository with explicit factory instantiation in Program.cs using lambda: `services.AddSingleton<IPositionsRepository>(sp => { var factory = new SqliteConnectionFactory(optionsDbPath); return new PositionsRepository(factory, logger); })`. This allows repository to connect to different database.
**Applicazione**: When a service needs to access multiple SQLite databases, register one factory as default (for primary database) and create secondary repositories with explicit factory instances in DI registration. Do NOT register multiple IDbConnectionFactory instances (ambiguous resolution). Ensure read-only access to external databases to avoid locking conflicts.
**Rilevante per task**: T-19, future cross-service database queries

---

## LL-057 — T-19: Greeks monitoring requires WHERE delta IS NOT NULL filter

**Task**: T-19
**Categoria**: pattern
**Scoperta**: The active_positions table stores Greeks columns (delta, gamma, theta, vega) which are NULL until Greeks are calculated by another worker. GreeksMonitorWorker should only monitor positions with Greeks data available. SQL query must include `WHERE delta IS NOT NULL` to filter out positions without calculated Greeks, otherwise alert logic will fail on NULL values.
**Applicazione**: When querying tables with optional calculated fields (Greeks, PnL, aggregated metrics), always filter WHERE calculated_field IS NOT NULL unless you explicitly want to handle NULL cases. This pattern is cleaner than checking for NULL in application logic for every position. Created GetActivePositionsWithGreeksAsync() method specifically for this use case.
**Rilevante per task**: T-19, future Greeks-based analytics tasks

---

## LL-058 — T-19: Configuration thresholds should be validated in constructor

**Task**: T-19
**Categoria**: pattern
**Scoperta**: GreeksMonitorWorker has 4 threshold configuration values (DeltaThreshold, GammaThreshold, ThetaThreshold, VegaThreshold) that must be within valid ranges (e.g., DeltaThreshold between 0.0 and 1.0). Validating thresholds in the constructor ensures the service fails to start if configuration is invalid, following the fail-fast pattern from LL-047. Throw ArgumentException with descriptive message for each invalid threshold.
**Applicazione**: For all monitoring workers with numeric thresholds, validate ranges in constructor: `if (_deltaThreshold < 0.0 || _deltaThreshold > 1.0) throw new ArgumentException("Invalid DeltaThreshold. Must be 0.0-1.0.");`. This prevents runtime errors from misconfigured thresholds. Document valid ranges in appsettings.json comments.
**Rilevante per task**: T-19, all monitoring workers (T-03, T-07, T-08, T-11)

---

## LL-059 — T-19: Alert creation with structured JSON details

**Task**: T-19
**Categoria**: pattern
**Scoperta**: Alerts created by GreeksMonitorWorker include structured JSON in DetailsJson field containing position_id, campaign_id, symbol, contract_symbol, greek values, thresholds, and timestamps. This makes alerts queryable and provides context for debugging and analysis. Use anonymous objects with JsonSerializer.Serialize() for clean syntax: `var details = new { position_id = pos.PositionId, delta = pos.Delta, threshold = _deltaThreshold }; alert.DetailsJson = JsonSerializer.Serialize(details);`
**Applicazione**: For all alert types, include structured details in DetailsJson field. Include: entity IDs (for linking), values that triggered the alert, configured thresholds, and relevant timestamps. Use snake_case for JSON field names (consistent with database column naming). This allows dashboard to parse and display alerts with full context.
**Rilevante per task**: T-19, all alert-generating workers (T-03, T-07, T-08, T-11)

---

## LL-060 — T-19: Theta comparison requires absolute value

**Task**: T-19
**Categoria**: pattern
**Scoperta**: Theta (time decay) is always negative for long options (option loses value each day). When checking if theta exceeds a threshold (e.g., "alert if daily decay > $50"), you must compare Math.Abs(position.Theta) > threshold, not position.Theta > threshold (which would never trigger since theta is negative). ThetaThreshold configuration is stored as positive value (50.0), comparison uses absolute value.
**Applicazione**: When working with Greeks that are conventionally negative (Theta for longs, Delta for puts), use Math.Abs() for threshold comparisons. Document in configuration that threshold is absolute value (e.g., "ThetaThreshold: 50.0 means alert when |theta| > 50"). Logging should show absolute value for clarity: `_logger.LogWarning("Theta threshold breach: |theta|={0}", Math.Abs(theta))`.
**Rilevante per task**: T-19, future Greeks analytics tasks

---

## LL-056 — T-23: Configuration validation fail-fast pattern

**Task**: T-23
**Categoria**: pattern
**Scoperta**: Configuration validation should happen BEFORE host initialization to fail fast on critical errors. Build IConfiguration manually using ConfigurationBuilder, validate it, log warnings, and throw InvalidOperationException if validation fails. This prevents services from partially initializing with invalid configuration. Validators should separate critical errors (blocking) from warnings (non-blocking) using a ValidationResult with CriticalErrors and Warnings lists.
**Applicazione**: In Program.cs (before Host.CreateDefaultBuilder or builder.Build), manually build IConfiguration, instantiate validator, call Validate(), log warnings with Log.Warning, and throw if !IsValid with detailed error message listing all critical errors. Use this pattern for all services that have safety-critical configuration (trading mode, port numbers, position limits). Validator should be a separate class implementing IConfigurationValidator interface.
**Rilevante per task**: All service implementations (T-01, T-09, future services), any configuration-heavy applications

---

## LL-057 — T-23: In-memory IConfiguration for unit testing

**Task**: T-23
**Categoria**: testing
**Scoperta**: ConfigurationBuilder with AddInMemoryCollection allows creating IConfiguration instances from Dictionary<string, string?> for unit tests. This enables isolated testing of configuration validators without file I/O or environment dependencies. Pattern: `new ConfigurationBuilder().AddInMemoryCollection(testValues).Build()`. Test cases can specify exact key-value pairs, including missing keys (omit from dictionary), invalid values, and edge cases.
**Applicazione**: For testing configuration validators, use in-memory IConfiguration. Create helper method `CreateConfiguration(Dictionary<string, string?> values)` and `CreateValidator(Dictionary<string, string?> values)`. Each test case defines only the config keys it needs. Tests can verify both happy path (valid config) and error cases (missing keys, invalid values, safety violations). No appsettings.json files needed in test projects.
**Rilevante for task**: All configuration validation tests, any service configuration testing (T-23, future config-related tests)

---

## LL-058 — T-23: Critical vs warning validation separation

**Task**: T-23
**Categoria**: pattern
**Scoperta**: Configuration validation should distinguish critical errors (service cannot start safely) from warnings (service can start but may not function optimally). Critical errors include missing required fields, invalid safety settings (live trading mode, live ports), invalid numeric ranges. Warnings include non-standard but valid values, missing optional features (Telegram disabled, Cloudflare not configured), performance concerns (low intervals, high limits). Return ValidationResult with separate CriticalErrors and Warnings lists. IsValid = (CriticalErrors.Count == 0).
**Applicazione**: In validators, accumulate errors into `List<string> criticalErrors` and `List<string> warnings`. Use negative-first validation: check critical rules first, add to criticalErrors. Then validate optional/performance rules, add to warnings. Services log warnings (non-blocking) and throw on critical errors (blocking). Warnings help operators tune configuration without preventing startup. Document rationale for each critical vs warning decision.
**Rilevante for task**: All configuration validators (T-23, future validators), any validation logic that needs graduated severity

---

## LL-059 — T-23: Cross-field validation patterns

**Task**: T-23
**Categoria**: pattern
**Scoperta**: Some validation rules require comparing multiple configuration fields (cross-field validation). Examples: ReconnectMaxDelay >= ReconnectInitialDelay, CircuitBreakerReset >= CircuitBreakerWindow, MaxConcurrentOrders <= MaxPositionSize * N. Validate individual fields first (range checks, required checks), then validate relationships. Cross-field violations can be critical or warnings depending on safety impact. Use descriptive error messages that reference both fields and show the actual values.
**Applicazione**: Structure validators as: (1) validate individual fields, (2) validate cross-field constraints. For cross-field checks, read both values first, then compare. Error messages should be explicit: "ReconnectMaxDelaySeconds (50) must be >= ReconnectInitialDelaySeconds (100)". Decide critical vs warning based on whether violation causes crashes (critical) or suboptimal behavior (warning). Document cross-field constraints in XML comments on config classes.
**Rilevante for task**: Configuration validators with complex config (T-23), strategy validators with multi-field rules (T-04)

---

## LL-060 — T-23: Safety-critical port validation

**Task**: T-23
**Categoria**: pattern
**Scoperta**: For trading systems, port validation is safety-critical. IBKR uses different ports for paper (4002, 7497) and live (4001, 7496) trading. Configuration must validate that PaperPort is NOT a known live port, even if TradingMode is set to 'paper'. This creates layered safety: both TradingMode and port number must be safe. If PaperPort == 4001 or 7496, fail with CRITICAL error. Use explicit error messages: "IBKR:PaperPort is set to 4001, which is a LIVE trading port. This is a CRITICAL safety violation."
**Applicazione**: In IBKR configuration validators, check PaperPort against known live ports (4001, 7496) and fail with critical error if match. Check LivePort against known live ports and warn if mismatch. Validate TradingMode separately. Document standard ports in comments: 4002/7497 = paper, 4001/7496 = live. This pattern prevents catastrophic configuration errors where paper mode is configured but live port is used. Apply to any system with safety-critical external connections.
**Rilevante for task**: IBKR configuration validation (T-02, T-23, future IBKR tasks), any multi-environment external API configuration

---


## LL-061 — T-21: Integration tests require real API running

**Task**: T-21
**Categoria**: testing
**Scoperta**: Integration tests that test real API endpoints (not mocks) require the backend service to be running during test execution. Unlike unit tests (which use mocks and run in isolation), integration tests verify end-to-end functionality by making actual HTTP requests. Tests will fail with connection errors (ECONNREFUSED) if the API is not available. This is expected and correct behavior for integration tests.
**Applicazione**: Document in test README that tests require backend running. Provide clear instructions: "Start worker: cd infra/cloudflare/worker && bun run dev". For CI/CD, add step to start backend before running integration tests. Consider splitting tests into "unit" (mocks, always pass) and "integration" (real API, require backend) with separate npm scripts. Use environment variables (VITE_API_URL) to configure API endpoint for tests.
**Rilevante per task**: T-21 (dashboard tests), any future integration testing tasks, CI/CD setup

---

## LL-062 — T-21: Vitest with React requires @testing-library/react

**Task**: T-21
**Categoria**: tooling
**Scoperta**: Vitest can test React components and hooks but requires `@testing-library/react` for rendering components, `@testing-library/react-hooks` for testing custom hooks (deprecated in React 18+, use renderHook from @testing-library/react instead), and `jsdom` for DOM environment. Vitest config needs `environment: 'jsdom'` in test options. Without jsdom, tests fail with "document is not defined" errors.
**Applicazione**: Install: `@testing-library/react`, `@testing-library/user-event`, `jsdom`. Vitest config: `test: { globals: true, environment: 'jsdom', setupFiles: ['./test/setup.ts'] }`. React 19 compatible: use `renderHook` from `@testing-library/react` (NOT from deprecated @testing-library/react-hooks). Setup file should import from @testing-library/react and configure cleanup: `afterEach(() => cleanup())`.
**Rilevante per task**: T-21 (dashboard tests), any React component/hook testing

---

## LL-063 — T-21: React Query tests require QueryClientProvider wrapper

**Task**: T-21
**Categoria**: testing
**Scoperta**: Testing React Query hooks with `renderHook` requires wrapping the hook in a `QueryClientProvider` with a test QueryClient. Without the provider, hooks throw "No QueryClient set" error. Create a fresh QueryClient for each test to ensure isolation (no shared cache between tests). Disable retries and garbage collection for deterministic test behavior: `defaultOptions: { queries: { retry: false, gcTime: 0 } }`.
**Applicazione**: Pattern: Create `createTestQueryClient()` function that returns a new QueryClient with test-friendly options. Create `createWrapper(queryClient)` function that returns a component wrapping children with QueryClientProvider. Use in renderHook: `renderHook(() => useMyHook(), { wrapper: createWrapper(queryClient) })`. Each test gets its own QueryClient instance. Use `await waitFor(() => expect(result.current.isSuccess).toBe(true))` to wait for async queries to complete.
**Rilevante per task**: T-21 (React Query tests), any future React Query hook testing

---

## LL-064 — T-21: localStorage tests require mock in jsdom

**Task**: T-21
**Categoria**: testing
**Scoperta**: jsdom provides a basic localStorage implementation, but for reliable testing of edge cases (corrupted data, storage unavailable, quota exceeded), it's better to mock localStorage in tests. Create a simple mock that stores data in a plain object: `{ getItem(), setItem(), removeItem(), clear(), length, key(index) }`. Reset the mock before each test with `beforeEach(() => localStorageMock.clear())`. This gives full control over localStorage behavior in tests.
**Applicazione**: Create localStorageMock in test file as IIFE that returns localStorage-compatible object with in-memory store. Use `Object.defineProperty(window, 'localStorage', { value: localStorageMock })` to replace window.localStorage in tests. Test edge cases: corrupted JSON (JSON.parse throws), unavailable (getItem/setItem throw), quota exceeded. Zustand persist middleware handles all these gracefully with try/catch. Mock allows testing these error paths without relying on jsdom's implementation.
**Rilevante per task**: T-21 (Zustand tests), any localStorage/sessionStorage testing

---

## LL-065 — T-21: Integration test file naming and organization

**Task**: T-21
**Categoria**: pattern
**Scoperta**: Integration tests should be clearly separated from unit tests by file naming and directory structure. Pattern: `test/integration-*.test.ts` for integration tests (require backend), `test/unit-*.test.ts` for unit tests (mocks only). Test IDs in comments help traceability: `it('TEST-21-01: Description', ...)`. Each test suite should have a comprehensive README.md documenting: prerequisites (backend running), environment variables, test categories, how to run tests, troubleshooting common issues.
**Applicazione**: Directory structure: `test/integration-api.test.ts`, `test/integration-react-query.test.tsx`, `test/integration-zustand.test.ts`, `test/README.md`, `test/setup.ts`. Use descriptive suite names: `describe('Integration Tests: API Endpoints', ...)`. Number tests sequentially with TEST-XX-NN IDs for task tracking. README should include: overview, prerequisites, running tests, environment config, troubleshooting, test data seeding, related docs. Makes test suite self-documenting and reduces onboarding friction.
**Rilevante per task**: T-21 (test organization), any future test suite creation, test documentation standards

---
## LL-066 — T-22: Integration tests need WebApplicationFactory pattern for .NET services

**Task**: T-22
**Categoria**: testing
**Scoperta**: .NET hosted services (BackgroundService workers) can be integration tested using the same DI container setup as Program.cs, but without starting the actual host. Create a test host with `Host.CreateDefaultBuilder()`, configure services identically to production, then resolve services from `IServiceProvider` to verify DI registration. For worker lifecycle tests, instantiate workers directly and call `StartAsync()` with a `CancellationTokenSource` that you control. This allows testing: (1) All services can be resolved from DI, (2) Workers start and execute cycles, (3) Workers stop gracefully on cancellation, (4) Configuration is validated at startup.
**Applicazione**: Pattern: Create `CreateTestHost()` helper that mirrors Program.cs DI configuration. Use in-memory databases and mock external dependencies (IBKR, HTTP). For worker tests, create worker instance manually, start with `StartAsync(cts.Token)`, wait for execution, then cancel with `cts.Cancel()`. Handle `OperationCanceledException` (expected). For DI tests, use `host.Services.GetRequiredService<T>()` to verify registrations. For lifecycle tests, verify database state after worker runs (e.g., heartbeat inserted, events synced).
**Rilevante per task**: T-22 (integration tests), any future .NET service testing, worker lifecycle verification

---

## LL-067 — T-22: In-memory database factory enables fast isolated integration tests

**Task**: T-22
**Categoria**: testing
**Scoperta**: The `InMemoryConnectionFactory` pattern (from SharedKernel.Tests) provides fast, isolated SQLite databases for integration tests. Each factory instance creates a unique in-memory database with `Data Source={Guid};Mode=Memory;Cache=Shared`. A keep-alive connection prevents database destruction. In-memory databases do NOT support WAL mode (use DELETE mode instead). This enables: (1) Fast test execution (no disk I/O), (2) Complete isolation (no shared state between tests), (3) Automatic cleanup (database destroyed when factory disposed), (4) Real SQL queries (not mocked).
**Applicazione**: Pattern in test class: `private InMemoryConnectionFactory _factory = default!;` In `InitializeAsync()`: `_factory = new(); MigrationRunner runner = new(_factory, logger); await runner.RunAsync(migrations, ct);` In `DisposeAsync()`: `await _factory.DisposeAsync();` Use `_factory.OpenAsync()` in tests to get connections. Migrations must be run in InitializeAsync to create schema. Each test class gets its own isolated database. Tests within a class share the database but use separate records.
**Rilevante per task**: T-22 (integration tests), any repository/database testing, migration verification

---

## LL-068 — T-22: Integration test organization by functional category

**Task**: T-22
**Categoria**: pattern
**Scoperta**: Integration tests for .NET services should be organized by functional category, not by layer. Categories: (1) ProgramIntegrationTests - Service startup, DI registration, configuration validation, (2) MigrationIntegrationTests - Database schema creation, migration application, (3) WorkerLifecycleIntegrationTests - Worker start/stop/cancel, background execution, (4) RepositoryIntegrationTests - Data persistence, retrieval, updates with real database. This organization matches how the service is architected and makes it easy to verify each subsystem independently.
**Applicazione**: Directory structure: `{Service}.Tests/ProgramIntegrationTests.cs`, `{Service}.Tests/Migrations/MigrationIntegrationTests.cs`, `{Service}.Tests/Workers/WorkerLifecycleIntegrationTests.cs`, `{Service}.Tests/Repositories/RepositoryIntegrationTests.cs`. Each test file covers one category. Test IDs are sequential across all categories (TEST-22-01 through TEST-22-35). Tag tests with `[Trait("TaskId", "T-22")]` and `[Trait("TestId", "TEST-22-XX")]` for filtering. Document all test categories in tests/README.md.
**Rilevante per task**: T-22 (integration tests), any future service testing, test suite organization standards

---

## LL-069 — T-22: Worker tests require controlled cancellation and timing

**Task**: T-22
**Categoria**: testing
**Scoperta**: Testing BackgroundService workers requires careful control of timing and cancellation. Workers run infinite loops with `while (!stoppingToken.IsCancellationRequested)`. To test: (1) Create worker with short interval (1 second for tests), (2) Start with `StartAsync(cts.Token)`, (3) Wait for execution with `await Task.Delay()`, (4) Cancel with `cts.Cancel()`, (5) Await worker task and catch `OperationCanceledException` (expected). Verify side effects (database writes, HTTP calls) rather than trying to inspect worker internal state. Workers should handle cancellation gracefully and not throw unhandled exceptions.
**Applicazione**: Pattern: `using CancellationTokenSource cts = new(); Task workerTask = worker.StartAsync(cts.Token); await Task.Delay(TimeSpan.FromSeconds(2), cts.Token); cts.Cancel(); try { await workerTask; } catch (OperationCanceledException) { /* expected */ }`. Then verify side effects: `var data = await repo.GetLatestAsync(ct); Assert.NotNull(data);`. Use short intervals in test config (1-2 seconds). Wait long enough for at least one cycle to complete. Handle OCE - it's the correct cancellation behavior.
**Rilevante per task**: T-22 (worker tests), any BackgroundService testing, graceful shutdown verification

---

## LL-070 — T-22: Test documentation should match test implementation exactly

**Task**: T-22
**Categoria**: pattern
**Scoperta**: Integration test README.md should be a comprehensive reference that documents: (1) Test structure (directory layout), (2) Test categories (what each category tests), (3) Test list (every TEST-XX-YY with description), (4) Running tests (commands for all/filtered/detailed), (5) Test design principles (isolation, mocks, naming), (6) Troubleshooting (common issues and solutions), (7) CI/CD integration (how to run in pipelines), (8) Adding new tests (step-by-step guide). The README should be the single source of truth for test suite usage and maintenance.
**Applicazione**: Create tests/README.md with sections for structure, categories, running, principles, troubleshooting, CI/CD, and contributing. List every test with ID and description. Document test filters (`--filter "TaskId=T-22"`). Explain `IAsyncLifetime` pattern. Provide troubleshooting for common issues (database locked, table not found, tests hang). Include examples for adding new tests. Update README whenever tests are added/changed. Use this README as onboarding documentation for new developers.
**Rilevante per task**: T-22 (test documentation), any test suite with multiple categories, developer onboarding

---

## LL-071 — T-24: Deployment scripts organization and separation of concerns

**Task**: T-24
**Categoria**: tooling
**Scoperta**: Deployment scripts benefit from clear separation by component (Windows services, Cloudflare Worker, Dashboard) and by operation type (install, uninstall, update, verify, deploy, rollback). This allows each script to be focused, testable, and easy to maintain. Scripts should be idempotent and include pre-flight checks.
**Aplicazione**: Organize deployment scripts by component in dedicated directories (infra/windows/, infra/cloudflare/worker/scripts/, dashboard/scripts/). Each component should have: setup/install, verify, update/deploy, and rollback scripts. Include verbose output and error handling in all scripts.
**Rilevante per task**: T-24 (deployment), all future deployment and maintenance tasks

---

## LL-072 — T-24: Pre-deployment checklist automation prevents errors

**Task**: T-24
**Categoria**: pattern
**Scoperta**: An automated pre-deployment checklist that verifies all components (builds, tests, configuration, security) before deployment catches issues early and prevents failed deployments. The checklist should be comprehensive, covering git status, builds, tests, configuration validation, security checks, and documentation.
**Applicazione**: Create scripts/pre-deployment-checklist.sh that runs all verification steps and exits with non-zero code if any check fails. Include this in CI/CD pipeline and require manual deployments to run it first. Checklist should output clear PASS/FAIL/WARN status for each check.
**Rilevante per task**: T-24 (deployment), CI/CD setup, all production deployments

---

## LL-073 — T-24: Windows Service update requires service stop and file handle release

**Task**: T-24
**Categoria**: pattern
**Scoperta**: When updating Windows Services, the service must be fully stopped AND file handles must be released before overwriting binaries. Windows locks running executables and DLLs. After stopping a service, wait 3-5 seconds for file handles to be released, or use explicit process checks. Create backups before updating.
**Applicazione**: In update-services.ps1, stop service with Stop-Service -Force, then Start-Sleep -Seconds 5 before attempting to overwrite binaries. Create timestamped backups in backup/ directory before updating. Provide rollback instructions if update fails.
**Rilevante per task**: T-24 (deployment), all Windows Service maintenance tasks

---

## LL-074 — T-24: Cloudflare Worker deployment requires wrangler.toml validation

**Task**: T-24
**Categoria**: cloudflare
**Scoperta**: Deploying Cloudflare Workers requires wrangler.toml to be fully configured (no placeholder values like REPLACE_WITH_YOUR_D1_ID) and secrets to be set via wrangler secret put (not in wrangler.toml). Deployment scripts should validate configuration before deploying and fail fast if configuration is incomplete.
**Applicazione**: In deploy.sh, check for placeholder values with grep before deploying. Verify that required secrets are set with wrangler secret list. Prompt user to set missing secrets before proceeding. Never commit secrets to wrangler.toml or git.
**Rilevante per task**: T-24 (deployment), T-15 (Cloudflare Worker), all Cloudflare deployments

---

## LL-075 — T-24: GitHub Actions CI/CD for multi-component system requires job dependencies

**Task**: T-24
**Categoria**: tooling
**Scoperta**: CI/CD workflows for systems with multiple components (.NET services, Cloudflare Worker, Dashboard) should use separate jobs for each component with needs: dependencies for deployment jobs. This allows parallel builds and tests while ensuring deployments only run if all tests pass. Use workflow_dispatch for manual deployment control.
**Applicazione**: In .github/workflows/ci.yml, create separate jobs for dotnet-build-test, cloudflare-worker, dashboard, security, and deploy. Deploy job should have needs: [all-other-jobs] and if: github.event_name == 'workflow_dispatch' for manual control. Use artifacts to pass build outputs between jobs.
**Rilevante per task**: T-24 (deployment), CI/CD setup, all automated testing and deployment

---

## LL-076 — T-25: Comprehensive documentation structure for complex systems

**Task**: T-25
**Categoria**: documentation
**Scoperta**: Effective technical documentation for complex trading systems requires clear separation of concerns across multiple files, each serving a distinct audience and purpose. Created 7 major documentation files (34,983 lines total): ARCHITECTURE.md (system design for architects), GETTING_STARTED.md (installation for new users), CONFIGURATION.md (reference for operators), STRATEGY_FORMAT.md (JSON guide for traders), TROUBLESHOOTING.md (issue resolution for support), CONTRIBUTING.md (standards for developers), and MONITORING.md (observability for DevOps). Each document cross-references others and includes practical examples, tables for quick reference, and detailed explanations.
**Applicazione**: When documenting complex systems, structure documentation by audience and purpose rather than by feature. Use consistent format: Table of Contents, clear sections, code examples for every concept, cross-references between documents, and practical quick-reference tables. Include real-world examples (not just API docs). Separate "getting started" from "reference" from "troubleshooting". Navigation should be clear from main README. Update "last modified" dates for maintenance tracking.
**Rilevante per task**: All future documentation tasks, knowledge base management, onboarding new team members, system design documentation

---


## LL-077 — T-26: E2E test checklists provide comprehensive validation coverage

**Task**: T-26
**Categoria**: testing
**Scoperta**: Manual E2E test checklists with detailed step-by-step procedures provide more comprehensive validation than automated tests alone for complex trading systems. Created 10 E2E test scenarios covering: (1) Startup and IBKR connection, (2) Heartbeat sync to Cloudflare, (3) IVTS monitoring and alerts, (4) Campaign creation and order placement, (5) Greeks calculation and risk alerts, (6) Profit target hit and exit, (7) IBKR disconnection and reconnection, (8) Cloudflare Worker failure resilience, (9) Service restart and state recovery, (10) Emergency stop and position liquidation. Each checklist includes: prerequisites, step-by-step actions, expected outcomes, SQL verification queries, performance benchmarks, troubleshooting guides, and cleanup procedures. Total: ~3,500 lines of detailed test documentation.
**Applicazione**: For systems requiring IBKR Paper Trading or external dependencies, create manual test checklists with: clear REQUIRES_PAPER markers, prerequisite verification section, numbered steps with expected outcomes, database query verification, log output verification, performance benchmarks, edge case coverage, cleanup procedures, and detailed troubleshooting. Complement with automated tests for components that don't require external dependencies (schema validation, config validation, mock scenarios). Use markdown format for readability and checkboxes for tracking.
**Rilevante per task**: All future testing tasks, QA validation, deployment readiness checks, system integration testing, production deployment verification

---

## LL-078 — T-26: Automated E2E tests verify infrastructure without external dependencies

**Task**: T-26
**Categoria**: testing
**Scoperta**: Automated E2E tests can verify critical infrastructure components (database schema, configuration validation, file structure) without requiring IBKR connection or external services. Created automated test suite covering: (1) Database schema verification for both supervisor.db and options.db, (2) Table structure validation (columns, types, constraints), (3) Index verification, (4) Configuration file JSON structure validation, (5) Trading mode safety validation, (6) Strategy file format validation. These tests run in CI/CD and catch structural issues early. Use in-memory SQLite databases for schema testing with real migration runner. Total: 18 automated test cases.
**Applicazione**: Separate automated E2E tests (no external deps) from manual E2E tests (require IBKR/Cloudflare). Use xUnit with IAsyncLifetime for setup/teardown, in-memory SQLite for schema tests, Newtonsoft.Json for config validation. Tag tests with [Trait("Category", "E2E")] and [Trait("TestId", "E2E-AUTO-XX")] for filtering. Create dedicated test project (E2E.Automated.csproj) with project references to services. These tests should run on every commit in CI/CD.
**Rilevante per task**: CI/CD setup, pre-deployment validation, schema migration testing, configuration management, automated testing strategy

---

## LL-079 — T-26: Verification scripts provide comprehensive readiness checks

**Task**: T-26
**Categoria**: tooling
**Scoperta**: Bash and PowerShell verification scripts can automate comprehensive pre-deployment readiness checks across multiple dimensions: prerequisites (.NET SDK, Git), build verification, automated test execution, database schema validation, configuration file checks, E2E test file presence, strategy directory structure, script availability, documentation completeness, knowledge base presence, and service build readiness. Created verify-e2e.sh (bash) and verify-e2e.ps1 (PowerShell) with color-coded output, detailed reporting, and overall assessment (READY/READY WITH WARNINGS/NOT READY). Scripts generate markdown reports with pass/fail/warning counts.
**Applicazione**: Create verification scripts for both Linux/Mac (bash) and Windows (PowerShell) to ensure cross-platform compatibility. Use color-coded output (Green=Pass, Red=Fail, Yellow=Warn) for readability. Generate timestamped markdown reports in logs/ directory for audit trail. Check 10+ categories: prerequisites, build, tests, schema, config, documentation, knowledge base. Exit with code 0 if ready, 1 if not ready. Use pass/fail/warn helper functions for consistent formatting. Make scripts executable (chmod +x).
**Rilevante per task**: Pre-deployment validation, CI/CD integration, developer onboarding, system health checks, production readiness assessment

---

## LL-080 — T-26: E2E test sequencing matters for realistic validation

**Task**: T-26
**Categoria**: testing
**Scoperta**: The order of E2E test execution matters for realistic system validation. Tests should follow the natural workflow: (1) Basic functionality tests first (startup, sync), (2) Monitoring tests second (IVTS, Greeks), (3) Trading workflow tests third (campaign open, profit target, restart), (4) Resilience tests last (disconnect, Cloudflare down, emergency stop). The final test (E2E-10: Emergency Stop) must be run LAST as it closes all positions. This sequence mimics real-world usage patterns and ensures state dependencies are handled correctly. Document recommended sequence in README with phase groupings.
**Applicazione**: In E2E test documentation, clearly specify recommended execution order with phase groupings (Basic→Monitoring→Trading→Resilience). Mark destructive tests (e.g., Emergency Stop) as "RUN LAST" with warnings. Include state dependencies in prerequisites section (e.g., E2E-05 requires active campaign from E2E-04). Create evidence collection guidelines for each test phase. This prevents test failures due to incorrect execution order and makes troubleshooting easier.
**Rilevante per task**: QA testing procedures, deployment validation, system integration testing, test planning

---

## LL-081 — T-27: Final implementation reports consolidate build knowledge

**Task**: T-27
**Categoria**: documentation
**Scoperta**: A comprehensive final implementation report consolidates all build knowledge into a single executive-level document that serves as the definitive reference for deployment, handoff, and future maintenance. Created IMPLEMENTATION_REPORT.md (15,000+ lines) covering: (1) Executive summary with completion status and achievements, (2) Complete task completion matrix with all 28 tasks, (3) Knowledge base summary (128 lessons, 1 error, 1 skill update, 0 task corrections), (4) Component inventory with line counts and test coverage, (5) Deployment readiness checklist with prerequisites, manual steps, security review, (6) Next steps prioritized by criticality, (7) Known issues with resolution paths, (8) Statistics (files, lines, tests, docs). Also created knowledge/SUMMARY.md as knowledge base overview.
**Applicazione**: At project completion, create final implementation report with: executive summary (status, duration, achievements), task completion matrix (all tasks with status, files, tests, duration), knowledge base statistics (lessons learned count, error count, skill updates), component inventory (each component with LOC, test count, description), deployment readiness (prerequisites, manual steps, security review), next steps (prioritized by criticality), known issues (with workarounds and permanent resolutions), and comprehensive statistics (files created, lines of code, test coverage, documentation coverage). Use markdown tables for readability, clear section numbering, and cross-references to detailed docs.
**Rilevante per task**: Project completion, client handoff, deployment planning, executive reporting, knowledge transfer

---

## LL-082 — T-27: Knowledge base summaries enable pattern discovery

**Task**: T-27
**Categoria**: pattern
**Scoperta**: Creating a knowledge base summary (knowledge/SUMMARY.md) at project completion reveals patterns and insights that aren't visible when reading individual lessons learned entries. Analysis of 128 lessons showed: (1) Category distribution (45 pattern, 25 testing, 18 tooling, 12 performance, etc.), (2) Cross-task learning flow (e.g., InMemoryConnectionFactory discovered in T-01, reused in 8+ tasks), (3) Skill accuracy rate (87.5% required no updates), (4) Specification quality (0 task corrections needed), (5) Top 10 most impactful lessons (by reuse count and task dependency). This meta-analysis identifies reusable patterns, validates self-improvement protocol effectiveness, and highlights areas for future builds.
**Applicazione**: At project completion, create knowledge/SUMMARY.md with: overall statistics (total lessons, errors, skill updates, corrections), category distribution (grouped by type), top N most impactful lessons (by reuse or criticality), cross-task learning examples (show how knowledge propagated), skill evolution analysis (which skills changed and why), specification quality assessment (corrections needed), self-improvement protocol effectiveness (repeated errors count), and recommendations for next build. This meta-view enables pattern discovery and continuous improvement.
**Rilevante per task**: Knowledge management, continuous improvement, pattern recognition, self-improvement protocol refinement

---

## LL-083 — T-27: Zero specification defects indicates high-quality requirements

**Task**: T-27
**Categoria**: pattern
**Scoperta**: Across all 28 tasks, zero task corrections were required to the original specifications in docs/trading-system-docs/. Every task was implementable as written with no ambiguities, errors, or missing requirements. This 100% specification accuracy rate is exceptional and enabled first-attempt success on all tasks. Contributing factors: (1) Detailed task definitions with clear acceptance criteria, (2) Comprehensive examples in specifications, (3) Clear separation of concerns (one task = one component), (4) Explicit test case IDs (TEST-XX-YY format) with expected outcomes, (5) Skill files providing implementation patterns before tasks began. This demonstrates the value of upfront specification investment.
**Applicazione**: Invest in high-quality specification creation before implementation begins. Include: clear acceptance criteria (what DONE means), explicit test cases with IDs and expected outcomes, code examples showing desired patterns, clear scope boundaries (what's in/out), prerequisite tasks and dependencies, skill file references for patterns to use. If specifications are this detailed, implementation agents can work autonomously with zero clarifications needed. Track specification defect rate (corrections needed / total tasks) as quality metric.
**Rilevante per task**: Requirements engineering, task planning, specification writing, autonomous agent design

---

## LL-084 — T-27: Self-improvement protocol prevents repeated errors

**Task**: T-27
**Categoria**: pattern
**Scoperta**: The self-improvement protocol (read errors-registry.md and lessons-learned.md before implementing, write lessons after completing) achieved 100% effectiveness: zero errors were repeated across all 28 tasks. Example: T-01 discovered InMemoryConnectionFactory pattern and documented it in LL-010. Tasks T-03, T-08, T-13, and others all read lessons-learned.md first and reused the pattern without re-discovering it. Similarly, T-06 discovered Tailwind CSS v4 PostCSS issue, updated skill-react-dashboard.md, and no future tasks repeated the error. This demonstrates that systematic knowledge capture and propagation eliminates waste and accelerates later tasks.
**Applicazione**: Enforce self-improvement protocol rigorously: (1) BEFORE implementing, read errors-registry.md and lessons-learned.md (filter by relevant category), (2) DURING implementation, note any patterns discovered or errors encountered, (3) AFTER completing, write at least 1 lesson learned entry even if task succeeded, (4) UPDATE skill files if pattern discovered is generalizable. Track "repeated error rate" as effectiveness metric (should be 0%). This protocol compounds value over time: later tasks become faster as knowledge base grows.
**Rilevante per task**: All future agent-driven builds, knowledge management, continuous improvement, autonomous agent design

---

## LL-085 — T-01a: Comprehensive type guards prevent runtime errors

**Task**: T-01a
**Categoria**: pattern
**Scoperta**: When creating TypeScript types for complex nested structures (like SDF v1 with 17+ interfaces), implementing comprehensive type guards (isStrategyDefinition, isStrategyLeg, etc.) is essential for runtime safety. Type guards should validate: (1) All required fields exist and have correct types, (2) Enum values match expected literals, (3) Nested objects are present (typeof === 'object' and !== null), (4) Array fields are actually arrays. This catches malformed JSON/external data before it enters type-safe code paths. In testing, type guards correctly rejected: null, undefined, empty objects, objects with missing fields, objects with wrong schema_version, and objects with invalid enum values.
**Applicazione**: For every complex interface imported from external sources (JSON files, API responses), create a corresponding type guard function. Structure: (1) Check typeof and null first, (2) Cast to Record<string, unknown> for safe property access, (3) Validate each required field with typeof checks, (4) Validate enum fields with explicit value checks (e.g., === 'put' || === 'call'), (5) For nested objects, check typeof === 'object' && !== null (arrays pass typeof === 'object', so add Array.isArray check if needed). Test type guards with: valid complete objects, objects missing required fields, objects with wrong types, null/undefined, primitives. Place type guards in same file as types for co-location.
**Rilevante per task**: T-01b (Zod schemas will complement these), T-02 (file loading needs validation), T-03+ (wizard needs runtime validation), any task parsing external data

---

## LL-086 — T-01a: DeepPartial utility type enables progressive form building

**Task**: T-01a
**Categoria**: pattern
**Scoperta**: For complex multi-step forms (like strategy wizard with 17+ nested configuration sections), a DeepPartial<T> utility type is essential. Standard TypeScript Partial<T> only makes top-level fields optional, but nested objects remain required. DeepPartial recursively makes all fields optional at all nesting levels. This allows: (1) Storing incomplete drafts without type errors, (2) Progressive building across multiple wizard steps, (3) Form state that matches completion level (e.g., only 'instrument' section filled). Implementation: `type DeepPartial<T> = T extends object ? { [P in keyof T]?: DeepPartial<T[P]> } : T`. The conditional type handles primitives (leaves them as-is) and objects (recurses). Tested successfully with deeply nested structures (execution_rules.repricing.enabled = true while other fields undefined).
**Applicazione**: When building multi-step forms for complex nested types: (1) Create DeepPartial<YourType> alias alongside main type definition, (2) Use DeepPartial for form state and draft storage (e.g., StrategyDraft = DeepPartial<StrategyDefinition>), (3) Use full type for final validated object, (4) Convert from DeepPartial to full type only after validation passes. In React: useState<DeepPartial<T>>() for draft, submit handler validates and converts to full T. This avoids TypeScript errors during progressive data entry while maintaining type safety at completion boundary.
**Rilevante per task**: T-03 (wizard form state), T-04 (draft storage), T-07 (form components), any multi-step form implementation

---

## LL-087 — T-01a: JSDoc on types improves IDE experience dramatically

**Task**: T-01a
**Categoria**: pattern
**Scoperta**: Adding comprehensive JSDoc comments to TypeScript interfaces and types provides massive developer experience improvement at zero runtime cost. VSCode and other IDEs show JSDoc content on hover, in autocomplete, and in quick info panels. For complex domain models (like SDF v1 with 17 interfaces, 13 enums, 50+ fields), inline documentation is essential. Effective JSDoc structure: (1) Interface-level comment explaining purpose and usage context, (2) Field-level comments explaining meaning, constraints, and examples, (3) Enum type comments explaining valid values, (4) Type guard comments explaining validation logic. Example: hovering over 'target_delta' shows "Target delta value (e.g., 0.30 for 30 delta)" without navigating away from current file. This is especially valuable for wizard UI developers who need to understand financial concepts (DTE, delta, Greeks) while building forms.
**Applicazione**: For every domain type that will be used by multiple developers or in UI: (1) Add JSDoc comment above interface with purpose and context, (2) Add field-level comments for non-obvious fields (especially domain concepts like delta, theta, DTE), (3) Include examples in comments (e.g., "e.g., 0.30 for 30 delta"), (4) Document constraints (e.g., "Must be positive", "0 = disabled"), (5) Document relationships (e.g., "Required when type is 'portfolio_greek'"). Use /** */ format (not // or //) to ensure IDE integration. Test by hovering in VSCode. This documentation compounds value: one-time cost, continuous benefit for all developers.
**Rilevante per task**: T-01b (Zod schemas need error messages), T-03+ (wizard UI developers), any task with domain-specific types

---
