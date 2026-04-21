---
title: "Paper-run day log — YYYY-MM-DD"
tags: ["ops", "runbook", "testing"]
aliases: ["Paper-run Template"]
status: reference
audience: ["operator"]
phase: "phase-7.7"
last-reviewed: "2026-04-21"
related:
  - "[[PAPER_VALIDATION]]"
  - "[[DAILY_OPS]]"
---

# Paper-run day log — YYYY-MM-DD

> Template. Copy to `docs/ops/paper-run/YYYY-MM-DD.md` at EOD during
> the 14-day paper validation. One file per trading day, committed
> to git so the completion report has evidence.

**Operator**: <name>
**Paper-run day**: <N of 14>
**Trading mode**: paper
**Market regime**: <normal / elevated-VIX / holiday-reduced-hours / ...>

---

## Morning round (08:45 local)

- Semaphore status: green / orange / red — `<composite value>`
- Overnight alerts: <count + one-line per alert>
- Data freshness workflow (last 3 runs): green / red
- .NET CI on main: green / red
- Services status: both Running / <anomaly>
- TradingMode assertion: paper confirmed / <anomaly>

## Midday round (12:30 local)

- Semaphore still: green / orange / red
- Open positions: <count>, matches expectation: yes / no
- Outbox `pending` count: <N>

## End-of-day round (17:30 local)

### Orders placed today

| time  | strategy_id         | symbol | side | outcome      | latency_ms |
|-------|---------------------|--------|------|--------------|------------|
| 09:35 | spx-weekly-iron-cdr | SPXW   | sell | filled       | 243        |
| ...   | ...                 | ...    | ...  | ...          | ...        |

### Alerts received today

| time  | severity | channel       | subject                     | action taken                        |
|-------|----------|---------------|-----------------------------|-------------------------------------|
| 10:12 | warn     | Telegram      | "IVTS approaching 1.10"     | noted, no action                    |

### Positions reconciliation (Worker vs TWS)

Result: match / drift

If drift: describe (symbol, qty diff, avg_price diff) and how resolved.

### Daily P&L

- `pnlPct`: <x.xx>%
- `accountValue`: <$xxxxx>
- `yesterdayClose`: <$xxxxx>

### SLO budget impact today

- S1 (availability): <N minutes down / 0 min>
- S2 (order P95 latency): <N slow orders / 0>
- S3 (data freshness): <N stale windows / 0>
- S4 (ingest success): <N rejected / 0>

## Anomalies / action items

- <bullet list>

## Sign-off

Day PASSES validation criteria: yes / no

If NO, why: <one line>

Impact on the 14-day clock: continue / restart
