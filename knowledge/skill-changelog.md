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
