# Contract Tests

Phase 7.6 deliverable. These tests lock the wire contract between the .NET
services (producers) and the Cloudflare Worker (consumer) on two axes:

1. **Outbox event payloads** (`.NET → Worker`) — the JSON shape of each
   `OutboxEntry.PayloadJson` for every known `EventType`.
2. **Worker ingest responses** (`Worker → .NET`) — the JSON shape of the
   response envelopes for the `POST /api/v1/ingest` endpoint.

The source of truth for each shape is a checked-in fixture under
`tests/Contract/fixtures/`. Two test suites assert against those fixtures:

| Side | Test project | What it does |
|------|--------------|--------------|
| Producer (.NET) | `tests/TradingSupervisorService.ContractTests/` | Serializes each outbox event DTO and structurally compares to the fixture. |
| Consumer (Worker) | `infra/cloudflare/worker/test/contract.test.ts` | Imports each Zod ingest schema and asserts `.safeParse(fixture)` succeeds for the `outbox-events/` fixtures and the response-shape fixtures parse into their documented shape. |

## Why fixtures, not hand-written type assertions

We chose **fixture files** rather than snapshot-based tests or type-level
comparisons for three reasons:

1. **Polyglot boundary.** The contract has to be verifiable from both C#
   and TypeScript — neither side has the other's type system. A JSON
   fixture is the lowest common denominator.
2. **Drift visibility.** If a producer rename changes the shape, the
   .NET test fails with a diff of missing/extra fields; the fixture file
   is the canonical "yes, we meant to change this" record in the PR diff.
3. **Replayability in local debug.** Need to replay a `vix_snapshot`
   event against a local Worker? `curl -d @tests/Contract/fixtures/outbox-events/vix_snapshot.json`
   is a one-liner.

**Trade-off**: fixtures must be updated by hand when the contract changes.
The alternative (snapshot tests that auto-update) makes the "is this drift
intentional?" question invisible. We lean toward explicit.

## Procedure — schema change

If you're intentionally changing a payload shape:

1. Update the producer code (`.NET` DTO or anonymous object literal in
   a worker) AND the Zod schema in `infra/cloudflare/worker/src/routes/ingest.ts`.
2. Update the fixture under `tests/Contract/fixtures/outbox-events/<type>.json`
   AND any affected response fixtures under
   `tests/Contract/fixtures/worker-responses/*.json`.
3. Run both test suites:
   ```bash
   dotnet test tests/TradingSupervisorService.ContractTests --no-build
   cd infra/cloudflare/worker && bunx vitest run test/contract.test.ts
   ```
4. Both MUST pass. If they do not, either the fixture is wrong or the
   code is wrong — investigate before merging.

## Procedure — new event type

1. Add the type constant to `src/TradingSupervisorService/Repositories/OutboxEventTypes.cs`.
2. Add the Zod schema and dispatch case to `infra/cloudflare/worker/src/routes/ingest.ts`.
3. Add a fixture `tests/Contract/fixtures/outbox-events/<new-type>.json`.
4. Add the corresponding producer-side serialization assertion to
   `tests/TradingSupervisorService.ContractTests/OutboxEventContractTests.cs`.
5. Add the Zod parse assertion to `infra/cloudflare/worker/test/contract.test.ts`.

## Why not run .NET tests inside the Worker test project?

Separation by stack is intentional. The .NET test validates that the
producer emits the fixture shape (structural compare of keys + nullability).
The TypeScript test validates that the Zod schema accepts the same fixture
(end-to-end round-trip through the ingest schema). Either side can drift
independently; catching both requires tests in both languages.

## Reference files

- `src/SharedKernel/Safety/OrderAuditEntry.cs` — the `order_audit` DTO.
- `src/TradingSupervisorService/Repositories/HeartbeatRepository.cs` —
  `ServiceHeartbeat` DTO (heartbeat event).
- `src/TradingSupervisorService/Repositories/AlertRepository.cs` —
  `AlertRecord` DTO (alert event).
- `src/TradingSupervisorService/Workers/MarketDataCollector.cs` +
  `BenchmarkCollector.cs` + `GreeksMonitorWorker.cs` — anonymous-object
  emitters for market_quote / vix_snapshot / account_equity /
  benchmark_close / position_greeks.
- `infra/cloudflare/worker/src/routes/ingest.ts` — the Zod schemas.
