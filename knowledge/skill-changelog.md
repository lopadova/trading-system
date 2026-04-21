---
title: "Skill Changelog"
tags: ["knowledge-base", "skills", "reference"]
aliases: ["skill-changelog"]
status: current
audience: ["ai-agent", "developer"]
last-reviewed: "2026-04-21"
related:
  - "[[errors-registry]]"
  - "[[lessons-learned]]"
---

# Skill Changelog
*Traccia ogni aggiornamento a .claude/skills/*.md*

---

## TEMPLATE
```
## YYYY-MM-DD — T-XX
**Skill**: skill-NNN.md | **Sezione**: ... | **Tipo**: fix|aggiunta|versione
**Problema risolto**: ... | **Impatto**: task T-XX, T-YY devono rileggere
```

---

## 2026-04-05 — T-00

**Skill**: N/A (no modifications needed)
**Sezione**: N/A
**Tipo**: versione
**Problema risolto**: T-00 completed successfully. All skill files (skill-dotnet.md, skill-windows-service.md, skill-self-improvement.md) were read and applied correctly. Package versions documented in skill-dotnet.md are confirmed working and used in all .csproj files.
**Impatto**: No skill changes required. Future tasks should continue using documented patterns.

---

## 2026-04-05 — T-01

**Skill**: skill-testing.md (added InMemoryConnectionFactory pattern)
**Sezione**: Test Helpers
**Tipo**: aggiunta
**Problema risolto**: Added InMemoryConnectionFactory pattern for SQLite unit tests. This helper creates isolated in-memory databases with a keep-alive connection pattern to prevent database destruction during test execution.
**Impatto**: All future tasks that need to test SQLite repositories (T-02, T-03, etc.) should use this pattern. The helper is now available in tests/SharedKernel.Tests/Data/InMemoryConnectionFactory.cs.

---

## 2026-04-05 — T-06

**Skill**: skill-react-dashboard.md
**Sezione**: Setup Progetto
**Tipo**: fix
**Problema risolto**: Tailwind CSS v4 requires `@tailwindcss/postcss` package instead of using `tailwindcss` directly in PostCSS config. Build fails with "trying to use tailwindcss directly as a PostCSS plugin" error.
**Impatto**: All tasks using Tailwind CSS v4 (T-06 and future dashboard work) must install `@tailwindcss/postcss` and use it in `postcss.config.js`: `plugins: { '@tailwindcss/postcss': {}, autoprefixer: {} }`

---

## 2026-04-05 — T-02

**Skill**: skill-ibkr-api.md
**Sezione**: Setup Connessione, Pattern EWrapper, Error Codes
**Tipo**: aggiunta + fix
**Problema risolto**: Completed full IBKR wrapper implementation with connection management, message processor thread pattern, exponential backoff reconnection, and comprehensive safety validation. Added NuGet package version (IBApi 10.19.2), connection state management pattern, and error code handling. Updated all patterns to be production-ready.

---

## 2026-04-07 — Legacy Tests Fix

**Skill**: skill-testing.md
**Sezione**: xUnit Async Patterns, Moq Async Patterns, Nullable Assertions, Namespace Conflict Resolution
**Tipo**: aggiunta
**Problema risolto**: During legacy .NET tests fix (224 compilation errors → 0), discovered critical patterns missing from skill:
- xUnit async test methods MUST return `async Task`, never `void` (ERR-007)
- Moq async methods require `.ReturnsAsync()`, not `.Returns()` (ERR-009)
- Nullable value type assertions need explicit null check before `.Value` (ERR-010)
- Namespace+class name collision requires type alias pattern (ERR-003)
- Worker constructor parameter order verification pattern
**Impatto**: All future test code must follow these patterns. Added 4 new sections to skill-testing.md with detailed before/after examples.

---

## 2026-04-07 — Legacy Tests Fix

**Skill**: skill-dotnet.md
**Sezione**: Repository API Evolution, DTO Naming Convention, Worker Constructor Patterns
**Tipo**: aggiunta
**Problema risolto**: During legacy .NET tests fix, documented evolution from generic CRUD repositories to domain-driven repositories:
- Repository API Evolution: Generic `InsertAsync` → Domain-specific `SaveCampaignAsync` (ERR-004)
- DTO Naming Convention: Explicit "Record" or "Entry" suffix distinguishes DTOs from domain entities (ERR-006)
- Worker Constructor Patterns: Document parameter order rationale to prevent breaking changes (ERR-005)
**Impatto**: All future repository implementations must follow domain-driven pattern. All DTOs must use Record/Entry suffix. All workers must document constructor parameter order.
**Migration guide**: Added old→new pattern examples for easy reference during refactoring.
**Impatto**: All tasks using IBKR API (T-02, T-05, future trading logic) must use these updated patterns. Message processor thread is REQUIRED for callbacks to fire. Connection state must be tracked separately from IsConnected(). Safety validation (paper trading only) is mandatory.

---

## 2026-04-05 — T-04

**Skill**: No skill file modifications needed
**Sezione**: N/A
**Tipo**: confirmation
**Problema risolto**: T-04 implemented strategy loader and validator following existing patterns in skill-dotnet.md. All patterns (immutable records, negative-first conditionals, early returns, try/catch with logging, Dapper-style validation) worked as documented. No errors or incompatibilities discovered. Added 5 new lessons learned (LL-040 through LL-044) documenting new patterns for strategy validation, JSON converters, and resilient file loading.
**Impatto**: No skill changes required. Future tasks can reference lessons learned for strategy validation patterns, custom JSON converters, and batch file loading with error resilience.

---
## 2026-04-05 — T-14

**Skill**: No skill file modification required
**Sezione**: N/A
**Tipo**: confirmation
**Problema risolto**: Implemented Black-Scholes Greeks calculator following skill-dotnet.md patterns exactly. All coding standards (early return, negative-first conditionals, typed signatures, record immutables, try/catch with logging) were applied correctly. No deviations or issues found with existing skill documentation.
**Impatto**: Future options analytics tasks (T-XX) can reference BlackScholesCalculator implementation as example of mathematical finance code in C#. Pattern: normal distribution functions, Greeks formulas with inline documentation referencing academic literature.

---

## 2026-04-05 — T-05

**Skill**: No skill file modification required
**Sezione**: N/A
**Tipo**: confirmation
**Problema risolto**: Implemented Telegram alerting service using Telegram.Bot NuGet package v19.0.0. All patterns from skill-dotnet.md (BackgroundService, ConcurrentQueue, try/catch with logging, configuration validation, graceful degradation) applied successfully. Service implements rate limiting (ConcurrentQueue for timestamp tracking), retry logic with exponential backoff, and markdown formatting for Telegram messages.
**Impatto**: Future tasks requiring external API integration can reference TelegramAlerter as example of resilient service design with queueing, rate limiting, and retry logic. Telegram.Bot 19.0.0 confirmed compatible with .NET 8.

---

## 2026-04-05 — T-11

**Skill**: No skill file modification required
**Sezione**: N/A
**Tipo**: confirmation
**Problema risolto**: T-11 implemented IVTS monitoring following existing patterns in skill-dotnet.md (BackgroundService, repository with Dapper, CommandDefinition for CancellationToken). All coding standards (early return, negative-first conditionals, try/catch with logging, immutable records, typed signatures) were applied correctly. No errors or incompatibilities discovered. Added 5 new lessons learned (LL-051 through LL-055) documenting IVTS monitoring patterns, IVR calculation, term structure analysis, worker enable/disable pattern, and alert repository reuse.
**Impatto**: No skill changes required. Future volatility monitoring tasks can reference T-11 implementation for IVTS patterns, IVR calculation, and term structure inversion detection. IVTS worker demonstrates disabled-by-default pattern for optional monitoring features.

---


## 2026-04-05 — T-09

**Skill**: No skill file modifications needed
**Sezione**: N/A
**Tipo**: confirmation
**Problema risolto**: T-09 completed OptionsExecutionService Program.cs integration following all existing patterns from skill-dotnet.md and skill-windows-service.md. All DI registrations, IBKR client setup, background workers, database migrations, and configuration validation patterns worked exactly as documented. No errors or incompatibilities discovered.
**Impatto**: No skill changes required. Future service integration tasks can reference Program.cs as example of complete DI setup with multiple background workers, database migrations, and external API client management (IBKR). Added 6 new lessons learned (LL-045 through LL-050) documenting service lifetime management, configuration validation, callback wiring, and background worker patterns.

---

## 2026-04-05 — T-19

**Skill**: No skill file modification required
**Sezione**: N/A
**Tipo**: confirmation
**Problema risolto**: T-19 implemented Greeks Monitor Worker following all existing patterns from skill-dotnet.md (BackgroundService, repository with Dapper, CommandDefinition, try/catch with logging, configuration validation). All coding standards (early return, negative-first conditionals, typed signatures, immutable records) were applied correctly. No errors or incompatibilities discovered. Added 5 new lessons learned (LL-056 through LL-060) documenting cross-database access pattern, Greeks filtering with WHERE delta IS NOT NULL, configuration threshold validation, structured alert JSON, and absolute value comparison for negative Greeks (Theta).
**Impatto**: No skill changes required. Future risk monitoring tasks can reference T-19 implementation for multi-database access, Greeks threshold monitoring, and structured alert creation patterns. GreeksMonitorWorker demonstrates best practices for monitoring optional calculated fields (Greeks) with NULL filtering.

---

## 2026-04-05 — T-23

**Skill**: No skill file modifications needed
**Sezione**: N/A
**Tipo**: confirmation
**Problema risolto**: T-23 implemented configuration validators for both services following all existing patterns from skill-dotnet.md (early return, negative-first conditionals, immutable records, typed signatures, verbose comments). All coding standards were applied correctly. Added 5 new lessons learned (LL-056 through LL-060) documenting configuration validation patterns: fail-fast startup validation, in-memory IConfiguration for testing, critical vs warning separation, cross-field validation, and safety-critical port validation.
**Impatto**: No skill changes required. Future configuration validation tasks can reference T-23 implementation and lessons learned for patterns. Configuration validation is now a reusable component in SharedKernel.Configuration namespace.

---


## 2026-04-05 — T-21

**Skill**: No skill file modifications needed
**Sezione**: N/A
**Tipo**: confirmation
**Problema risolto**: T-21 completed dashboard integration tests following all existing patterns from skill-react-dashboard.md and skill-testing.md. All patterns (Vitest setup, React Query testing with QueryClientProvider wrapper, localStorage mocking for Zustand tests, ky client for API testing, TypeScript strict mode) worked exactly as documented. No errors or incompatibilities discovered. Created 64 test cases covering API endpoints, React Query hooks, and Zustand store persistence.
**Impatto**: No skill changes required. Future integration testing tasks can reference T-21 test files as examples. Added 5 new lessons learned (LL-061 through LL-065) documenting integration test patterns: backend requirement, @testing-library/react setup, QueryClient wrapper pattern, localStorage mock pattern, and test file organization.

---

## 2026-04-07 — OutboxSyncWorker Test Fix

**Skill**: skill-testing.md (BackgroundService Testing — Configurable Timing)
**Sezione**: New section added after Namespace Conflict Resolution
**Tipo**: aggiunta
**Problema risolto**: BackgroundService workers with hardcoded startup delays (e.g., 5 seconds) prevent unit tests from executing within test timeouts. Test waits 100ms, worker waits 5000ms before entering main loop, so mock method never gets called and verification fails. Solution: Make ALL timing values (startup delay, interval) configurable via IConfiguration with production defaults. Tests override to 0 for immediate execution.
**Pattern aggiunto**:
- Worker implementation: Load `_startupDelaySeconds` from config with default 5
- Worker ExecuteAsync: Check `if (_startupDelaySeconds > 0)` before delay
- Test setup: `SetupConfigValue("OutboxSync:StartupDelaySeconds", "0")`
- Production config: Default 5s for dependency initialization
**Impatto**: ALL BackgroundService tests must configure timing values. Applies to HeartbeatWorker, OutboxSyncWorker, AlertDispatchWorker, CampaignMonitorWorker, OrderExecutorWorker, and future workers. Benefits: Fast tests (0ms vs 5000ms), reliable verification, flexible integration testing. Related: ERR-012, LESSON-157, LL-038.

---

## 2026-04-07 — Test Coverage Sprint

**Skills Updated**: 
- skill-testing.md v3.1 → v3.2 (3 new sections)
- skill-dotnet.md v2.0 → v2.1 (2 new sections)

**Sezioni Aggiunte**:

**skill-testing.md**:
1. **Culture-Invariant Test Data — CRITICAL**: Test failures on non-US locales due to culture-specific decimal formatting (Italian: "0,85" vs US: "0.85"). Pattern: ALWAYS use `CultureInfo.InvariantCulture` for production string formatting (alerts, logs, CSV, API). Test with multiple cultures or run CI with `DOTNET_CLI_UI_LANGUAGE=it-IT` to catch bugs.

2. **Windows Antivirus Handling in Tests**: Error 0x800711C7 blocks unsigned test DLLs. Root causes: Windows Defender, AVIRA Security, Smart App Control, Enterprise WDAC (each requires different solution). Permanent fix: Strong-name signing. Temporary: `unlock-and-test-all.ps1`. Alternatives: GitHub Actions (Linux), WSL2, Docker. Detection scripts and documentation references included.

3. **File-Based Testing: StreamReader Buffering Gotcha**: LogReaderWorker tests failed because `StreamReader` buffers 1KB+ chunks, making `FileStream.Position` unreliable for line-by-line tracking. Loop condition `fs.Position < endPosition` fails for small test files (< 1KB). Fix: Use `StreamReader.EndOfStream` only, track position using file size externally. ALWAYS test file readers with < 100 byte files to catch buffering bugs.

**skill-dotnet.md**:
1. **Culture-Invariant Formatting — CRITICAL PRODUCTION RULE**: String interpolation `$"{number:F2}"` uses `CurrentCulture`, breaking logs/API on Italian Windows ("0,85"). NEVER use interpolation for production paths. Pattern: `string.Format(CultureInfo.InvariantCulture, ...)` for alerts, logs, CSV, JSON, SQL, API payloads. Use `CurrentCulture` ONLY for UI display to end users.

2. **BackgroundService CancellationToken Pattern**: Passing `stoppingToken` to database writes causes silent data loss during shutdown (OperationCanceledException swallowed by error handler). Rule: Use `CancellationToken.None` for critical writes (alerts, state updates, audit logs). Use `stoppingToken` for cancelable operations (file I/O, HTTP, delays).

**Problema risolto**: 
- ERR-015: Culture-specific formatting in GreeksMonitorWorker (4 alert messages fixed)
- ERR-016: Windows Defender/AVIRA blocking 49 tests (documentation + scripts created)
- ERR-017: StreamReader buffering in LogReaderWorker (file position tracking fixed)

**Test Results**:
- BEFORE: 227/229 passing (99.1%), 2 LogReaderWorker tests failing, 49 AVIRA-blocked
- AFTER: 229/229 passing (100%) without AVIRA, 278/278 (100%) with strong-name signing

**Files Created**:
- `WINDOWS_DEFENDER_UNLOCK.md` - Complete antivirus troubleshooting guide
- `DEVELOPMENT_SETUP.md` - Environment setup with IDE, AVIRA, strong-name signing
- `scripts/unlock-and-test-all.ps1` - All-in-one unlock script (temporary solution)
- `scripts/unlock-with-avira.ps1` - AVIRA-specific handler
- `scripts/setup-strong-name-signing.ps1` - Strong-name signing automation

**Knowledge Base Updates**:
- Added ERR-015, ERR-016, ERR-017 to errors-registry.md
- Added LL-177 (Culture-Invariant), LL-178 (Antivirus), LL-179 (StreamReader) to lessons-learned.md

**Impatto**: 
- CRITICAL: All production code creating alerts/logs/CSV/API MUST use InvariantCulture
- CI/CD: Run tests with non-US culture to catch formatting bugs
- Development: Document antivirus requirements, provide strong-name signing setup
- Testing: NEVER mix StreamReader + FileStream.Position, test file readers with < 100 byte files
- BackgroundService: Audit all workers for CancellationToken usage in database operations

**Related**: ERR-015, ERR-016, ERR-017, LL-177, LL-178, LL-179

---

## 2026-04-17 — Code Quality Review

**Skill**: skill-dotnet.md
**Sezione**: Build Standards (NEW section)
**Tipo**: aggiunta
**Problema risolto**: 
- ERR-018: Compiler warnings left unresolved in codebase (3 CS0067 warnings in test fakes)
- Established zero-warning build policy to prevent warning accumulation
- Documented when and how to use `#pragma warning disable` (sparingly, only for interface requirements)

**New Section Added**: "Build Standards — Zero-Warning Policy"
- ✅ Rule: Build MUST show "Avvisi: 0, Errori: 0" before marking task as DONE
- ✅ Common warnings and fixes table (CS0067, CS0649, CS8618, CS0612/CS0618)
- ✅ `#pragma warning disable` usage guidelines (when to use, when NOT to use)
- ✅ CI/CD enforcement pattern (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)
- ✅ Code review checkpoint: Block PRs with unresolved warnings

**Knowledge Base Updates**:
- Added ERR-018 to errors-registry.md (MEDIUM severity)
- Added LL-180 to lessons-learned.md (quality | standards | tooling)

**Skill Version**: 2.1 → 2.2

**Impatto**: 
- ALL tasks MUST verify `dotnet build` shows 0 warnings before marking as DONE
- Add "0 warnings" check to task completion checklist
- Code review: Block PRs with unresolved warnings
- CI/CD: Treat warnings as errors in Release builds

**Related**: ERR-018, LL-180

---

## 2026-04-20 — Dashboard redesign feature complete (feat/dashboard-redesign, phases 1-5)

**Skill changes**: None (this feature did not modify any `.claude/skills/*.md`; it followed existing skill guidance for React + Cloudflare Worker work without needing new patterns).

**Knowledge changes**:
- `knowledge/lessons-learned.md`: +LESSON-181 (CSS vars + Tailwind @theme bridge), +LESSON-182 (anchor gitignore patterns to repo root).
- `knowledge/errors-registry.md`: no new CRITICAL errors discovered during the feature.

**Related**: branch `feat/dashboard-redesign`, plans `docs/superpowers/plans/2026-04-20-dashboard-redesign{,-part2,-part3,-part4}.md`.

