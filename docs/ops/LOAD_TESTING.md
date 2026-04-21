---
title: "Load Testing — Trading System"
tags: ["ops", "testing", "reference"]
aliases: ["LOAD_TESTING", "Load Testing"]
status: current
audience: ["developer"]
phase: "phase-7.6"
last-reviewed: "2026-04-21"
related:
  - "[[SLO]]"
  - "[[OBSERVABILITY]]"
  - "[[Trading System - End-to-End Test Plan|TEST_PLAN]]"
---

# Load Testing — Trading System

Phase 7.6 deliverable. Describes how to run the k6 load tests against the
Cloudflare Worker and how to interpret the numbers.

**Cost note**: k6 against a remote Cloudflare Worker consumes egress +
Worker request quota. One 10-minute / 100-VU / 30s-interval overview-poll
run issues ~8,000 requests. Don't wire this to CI without thinking about
the bill. The default posture is **manual trigger only**.

## Prerequisites

- k6 ≥ 0.50 installed locally (`brew install k6` on macOS,
  `choco install k6` on Windows, or from <https://k6.io/docs/get-started/installation/>).
- The Worker URL and an API key for the environment you want to hit.
  Treat the API key as a secret — never commit it.
- Network path to the Worker. `k6 cloud` is a possibility for distributed
  runs but has not been vetted against our auth middleware.

## Running the overview-poll test

The overview-poll scenario mirrors what the dashboard's Overview page does
on its 30-second React Query refresh interval: hit the 4 aggregate
endpoints in parallel, sleep, repeat. 100 VUs × 30s interval ≈ 3.3 req/s
average per endpoint, with burst of ~100 concurrent in the first 100ms
of each cycle.

```bash
# Default: 100 VUs, 10m duration, 30s poll interval
WORKER_URL=https://trading-system.padosoft.workers.dev \
API_KEY=ts_prod_xxxxx \
k6 run test/load/overview-poll.js

# Shorter smoke run
WORKER_URL=... API_KEY=... \
  k6 run -e VUS=20 -e DURATION=2m test/load/overview-poll.js

# Against local wrangler dev (prints a lot, tolerates higher latency)
k6 run -e WORKER_URL=http://127.0.0.1:8787 -e DURATION=1m \
  test/load/overview-poll.js
```

## Interpreting the output

k6 writes both a compact summary to stdout and a full raw JSON to
`summary.json`:

```
================ Overview-poll k6 run ================
  VUs                  : 100
  Duration             : 10m
  Poll interval        : 30s
  Total endpoint hits  : 8000
  p(95) duration (2xx) : 143.7ms
  http_req_failed rate : 0.075%
  (thresholds: p(95) < 200ms, fail < 1%)
======================================================
```

If either threshold is red, k6 exits non-zero and prints which check
fired. The full metric breakdown is in `summary.json`.

### Baseline numbers (Cloudflare prod Worker, 2026-04-20)

| Metric                        | Value    | Notes                                  |
|-------------------------------|----------|----------------------------------------|
| p(95) duration (2xx)          | ~145 ms  | With D1 cold cache. Warm cache ~30 ms. |
| http_req_failed rate          | < 0.1 %  | 1-2 random 500s per run; D1 jitter.    |
| Average RPS per endpoint      | ~3.3     | 100 VU / 30s interval                  |
| Total data transferred (10m)  | ~40 MB   | All JSON, no compression win.          |

### Baseline-regression policy

A single run is not a signal; run the scenario **three times** before
making a claim. Compare the median of the three p(95) values:

- **Within 20 % of baseline** → fine, no action.
- **20-50 % drift** → investigate. Likely culprits in order of probability:
  1. New D1 query or missing index in a recently-merged PR.
  2. Worker code paths changed from edge-resolvable to origin-bound.
  3. Cache hit rate dropped (check `recordMetric` for `cache.miss` spike).
- **> 50 % drift** → block further deploys until root-caused. Run `git
  bisect` over the last week of main. Set the `UseRealIbkrDll` build flag
  to `false` and boot services against the staged database if you suspect
  data volume.

## What we're NOT testing here

- Ingest POST path (`/api/v1/ingest`) — the outbox sync worker is
  rate-limited and bursty-by-design; meaningful tests need a traffic
  shape matching the .NET outbox, not a constant-VU poller.
- Authentication failure path — `http_req_failed` is about transport
  errors, not auth rejections. The threshold would wrongly pass if 100 %
  of requests were getting 401s. If you change auth, re-assert manually
  that the request count in the run matches expectations.
- Dashboard rendering latency — that's a Playwright + web-vitals concern.
  See `dashboard/tests/e2e/` and `infra/cloudflare/worker/test/ingest.test.ts`
  (`web_vitals` event type).

## CI integration

**Deliberately none.** The cost per run and the need for valid secrets
make this a manual-trigger-only tool. A future Phase 7.7 task could wire
a weekly smoke run (e.g. 30 VUs × 2 min) if we gain confidence in the
budget profile. Until then, use this playbook manually.

## Related

- `test/load/overview-poll.js` — the k6 script.
- `docs/ops/RUNBOOK.md` Playbook 8 — what to do when baseline drifts.
- `docs/ops/OBSERVABILITY.md` — broader signal pipeline.
