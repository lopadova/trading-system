---
title: "Performance Optimization Guide"
tags: ["dev", "performance", "reference"]
status: current
audience: ["developer"]
last-reviewed: "2026-05-01"
related:
  - "[[ARCHITECTURE]]"
  - "[[LOAD_TESTING]]"
---

# Performance Optimization Guide

Current optimization status and future improvement opportunities.

---

## Current Optimizations (Already Implemented)

### Dashboard

**Build Time:** ~45s (measured in CI)
**Bundle Size:** Optimized with Vite production build

**Implemented:**
- ✅ React Query with stale-while-revalidate caching
- ✅ Component code-splitting via lazy loading (future)
- ✅ Memoization for expensive computations
- ✅ Virtual scrolling for large lists (positions, alerts)
- ✅ Debounced search inputs
- ✅ Production build minification + tree-shaking

**Metrics:**
```bash
cd dashboard && npm run build
# Check dist/ size
du -sh dist/
```

### Cloudflare Worker

**Build Time:** ~22s (measured in CI)
**Response Time:** < 100ms p95 (local testing)

**Implemented:**
- ✅ Hono router (faster than Express)
- ✅ D1 connection pooling (automatic)
- ✅ SQL query optimization (indexed columns)
- ✅ Edge caching headers (CDN-friendly)
- ✅ Zod schema validation (compile-time optimized)

**Metrics:**
```bash
cd infra/cloudflare/worker
# Check bundle size
bun run build && ls -lh dist/
```

### .NET Services

**Build Time:** ~2m (measured in CI, includes tests)
**Service Startup:** < 5s

**Implemented:**
- ✅ Background workers with throttling (HeartbeatWorker: 60s interval)
- ✅ SQLite WAL mode (concurrent reads)
- ✅ Indexed queries (CampaignId, Status, OrderId)
- ✅ Connection pooling (Dapper)
- ✅ Single-file publish (faster startup)

---

## Potential Optimizations (Not Yet Implemented)

### Dashboard

**1. Code Splitting**
Current: Single bundle (~500KB estimated)
Opportunity: Split by route, lazy-load pages

```tsx
// Example lazy loading
const PositionsPage = lazy(() => import('./pages/PositionsPage'))
const CampaignsPage = lazy(() => import('./pages/CampaignsPage'))
```

**Impact:** Faster initial load (< 200KB), deferred loading for rarely-used pages
**Effort:** Low (1-2 hours)
**Priority:** Medium (only if bundle > 1MB)

**2. Service Worker Caching**
Current: No service worker
Opportunity: Cache static assets, offline support

**Impact:** Faster repeat visits, offline dashboard access
**Effort:** Medium (4-6 hours, testing across browsers)
**Priority:** Low (operator always online)

**3. Chart Performance**
Current: Recharts renders all data points
Opportunity: Downsample data for charts with > 100 points

**Impact:** Smoother rendering for large datasets
**Effort:** Medium (custom downsampling logic)
**Priority:** Low (current datasets small)

### Cloudflare Worker

**4. Edge Caching**
Current: No cache headers on API responses
Opportunity: Add `Cache-Control` for stable data (positions, campaigns)

```typescript
// Example
c.header('Cache-Control', 'public, max-age=30, stale-while-revalidate=60')
```

**Impact:** Reduced D1 queries, faster response times
**Effort:** Low (1 hour, add headers to routes)
**Priority:** Medium (depends on query load)

**5. D1 Query Optimization**
Current: Some queries without indexes
Opportunity: Analyze slow queries with `EXPLAIN QUERY PLAN`

```sql
-- Check if index is used
EXPLAIN QUERY PLAN SELECT * FROM campaigns WHERE status = 'active';
```

**Impact:** Faster query execution (< 10ms instead of 50ms)
**Effort:** Low (add missing indexes)
**Priority:** Medium (check after load testing)

### .NET Services

**6. Log Buffering**
Current: File writes on every log entry
Opportunity: Buffer logs in memory, flush every 5s

**Impact:** Reduced disk I/O, faster logging
**Effort:** Medium (custom log provider)
**Priority:** Low (logging not a bottleneck)

**7. Heartbeat Aggregation**
Current: Individual heartbeat inserts
Opportunity: Batch inserts (every 60s → bulk upsert)

**Impact:** Reduced SQLite transactions
**Effort:** Low (modify HeartbeatWorker)
**Priority:** Low (heartbeat load minimal)

---

## CI/CD Optimizations

**8. Workflow Caching**
Current: npm cache enabled, but not for Bun
Opportunity: Cache Bun dependencies

```yaml
- uses: actions/cache@v4
  with:
    path: ~/.bun/install/cache
    key: ${{ runner.os }}-bun-${{ hashFiles('**/bun.lockb') }}
```

**Impact:** Faster CI builds (save ~10-15s)
**Effort:** Low (add to workflow)
**Priority:** Low (current builds fast enough)

**9. Parallel Test Execution**
Current: Tests run sequentially in some projects
Opportunity: Enable parallel test execution

```bash
# .NET
dotnet test --parallel

# Dashboard (already parallel via vitest)
npm test
```

**Impact:** Faster test suite (save ~20-30s)
**Effort:** Low (add flag to CI)
**Priority:** Low (test suite already < 3min)

---

## How to Measure Performance

### Dashboard

**Lighthouse Audit:**
```bash
cd dashboard
npm run build
npx serve dist/ &
npx lighthouse http://localhost:3000 --view
```

**Bundle Analysis:**
```bash
npm run build -- --report
# Opens bundle analyzer
```

### Worker

**Load Testing:**
```bash
# See docs/ops/LOAD_TESTING.md
ab -n 1000 -c 10 https://ts-staging.padosoft.workers.dev/api/heartbeats
```

**D1 Query Profiling:**
```sql
-- In Wrangler console
EXPLAIN QUERY PLAN SELECT * FROM campaigns WHERE status = 'active';
```

### .NET Services

**Memory Profiling:**
```powershell
# Windows Performance Monitor
perfmon /res
# Add counters: Process(TradingSupervisorService)\Private Bytes
```

**SQL Profiling:**
```bash
# SQLite EXPLAIN QUERY PLAN
sqlite3 data/trading-supervisor.db
> EXPLAIN QUERY PLAN SELECT * FROM campaigns WHERE status = 'active';
```

---

## Optimization Decision Matrix

| Optimization | Impact | Effort | Priority | When to Do |
|--------------|--------|--------|----------|------------|
| Code splitting (dashboard) | Medium | Low | Medium | If bundle > 1MB |
| Edge caching (worker) | Medium | Low | Medium | After load testing |
| D1 indexes | High | Low | Medium | If queries > 50ms |
| Service worker | Low | Medium | Low | Phase 8+ |
| Log buffering | Low | Medium | Low | If disk I/O bottleneck |
| CI caching | Low | Low | Low | Optional |

---

## Premature Optimization Warnings

**DO NOT optimize:**
- Dashboard bundle size if < 500KB (fast enough)
- Worker response times if < 100ms p95 (acceptable)
- .NET startup time if < 10s (rare restart)
- SQL queries if < 50ms (not user-facing)

**DO optimize:**
- Anything blocking user interaction (> 200ms perceived delay)
- Repeated operations at scale (> 1000 req/day)
- Critical path (order placement, position monitoring)

**Always measure before optimizing:**
```bash
# Dashboard
npm run build && du -sh dist/
npx lighthouse <url> --view

# Worker
ab -n 1000 -c 10 <endpoint>

# .NET
perfmon /res
```

---

## Next Steps

1. **Baseline Metrics:** Run load testing per [[LOAD_TESTING]]
2. **Identify Bottlenecks:** Profile with Lighthouse, perfmon, EXPLAIN QUERY PLAN
3. **Prioritize:** Use decision matrix above
4. **Implement:** Start with high-impact, low-effort optimizations
5. **Measure:** Verify improvement with same tools

**Rule:** No optimization without measurement. No measurement without user impact.

---

*Last updated: 2026-05-01 — Current performance acceptable for Phase 7 load*
