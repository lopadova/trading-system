# Dashboard Integration Tests — TASK-21

> Integration tests for Trading System Dashboard with real Cloudflare Worker API

---

## Overview

This test suite provides comprehensive integration testing for the dashboard frontend + Cloudflare Worker backend stack. Unlike unit tests that use mocks, these tests make **real API calls** to verify end-to-end functionality.

## Test Categories

### 1. API Integration Tests (`integration-api.test.ts`)
- **42 test cases** covering all Cloudflare Worker endpoints
- Real HTTP requests using `ky` client
- Authentication and authorization flows
- Error handling (404, 500, auth failures)
- CORS headers verification
- Response format validation

**Key Endpoints Tested:**
- `GET /api/health` — Health check (no auth)
- `GET /api/positions/active` — Active positions list
- `GET /api/positions/:id` — Single position detail
- `GET /api/alerts` — Alerts list with filtering
- `GET /api/alerts/unresolved` — Unresolved alerts only
- `GET /api/heartbeats` — Service heartbeats
- `GET /api/heartbeats/:service` — Specific service heartbeat

### 2. React Query Integration Tests (`integration-react-query.test.tsx`)
- **12 test cases** for React Query hooks
- Real data fetching with caching
- Query invalidation and refetching
- Mutations and optimistic updates
- Loading and error states
- Type safety verification

**Hooks Tested:**
- `usePositions()` — Fetch and filter positions
- `useAlerts()` — Fetch and filter alerts
- `useResolveAlert()` — Mutation to resolve alerts

### 3. Zustand Store Tests (`integration-zustand.test.ts`)
- **10 test cases** for state persistence
- localStorage persistence and hydration
- Theme state management
- Partialize selective persistence
- Error handling for corrupted data
- Performance testing for rapid updates

---

## Running Tests

### Prerequisites

```bash
# Install dependencies
bun install

# Ensure Cloudflare Worker is running locally
cd ../infra/cloudflare/worker
bun run dev  # Runs on http://localhost:8787
```

### Run All Tests

```bash
# Run all tests once
bun test

# Run in watch mode (reruns on file changes)
bun test:watch

# Run with UI (Vitest UI)
bun test:ui

# Run with coverage report
bun test:coverage
```

### Run Specific Test Suites

```bash
# API integration tests only
bun test integration-api

# React Query tests only
bun test integration-react-query

# Zustand store tests only
bun test integration-zustand
```

### Filter by Test ID

```bash
# Run specific test by TEST-21-XX identifier
bun test -t "TEST-21-01"

# Run tests matching pattern
bun test -t "authentication"
```

---

## Environment Configuration

Tests use environment variables for configuration:

```bash
# .env.test (create this file)
VITE_API_URL=http://localhost:8787
VITE_API_KEY=test-api-key-12345
```

**Default values (if not set):**
- `VITE_API_URL`: `http://localhost:8787`
- `VITE_API_KEY`: `test-api-key-12345`

---

## Test Structure

Each integration test follows this pattern:

```typescript
it('TEST-21-XX: Description of what is being tested', async () => {
  // Arrange: Set up test data and configuration
  const response = await apiClient.get('api/endpoint', {
    headers: { 'X-Api-Key': API_KEY }
  })

  // Assert: Verify response status
  expect(response.status).toBe(200)

  // Assert: Verify response structure
  const data = await response.json<ExpectedType>()
  expect(data).toHaveProperty('expectedField')
})
```

**Test ID Format:** `TEST-21-XX` where XX is a zero-padded number (01-42)

---

## Integration Test Scenarios

### Scenario 1: Fetch Positions and Render in UI
**Tests:** TEST-21-06, TEST-21-21, TEST-21-22
1. API call to `/api/positions/active`
2. React Query hook fetches and caches data
3. Component renders position list
4. Filter by symbol/status works correctly

### Scenario 2: Filter Positions by Symbol/Status
**Tests:** TEST-21-07, TEST-21-22
1. API supports query parameters (`?symbol=SPY&status=open`)
2. React Query hook passes filters correctly
3. Filtered results match criteria

### Scenario 3: Fetch Alerts and Display by Severity
**Tests:** TEST-21-10, TEST-21-11, TEST-21-25, TEST-21-26
1. API call to `/api/alerts`
2. Filter by severity (`?severity=critical`)
3. React Query hook fetches and displays
4. Summary statistics are correct

### Scenario 4: Resolve Alert and Verify Update
**Tests:** TEST-21-29, TEST-21-30
1. Mutation to resolve alert
2. Query invalidation triggers refetch
3. Alert status updates in UI

### Scenario 5: Auth Header Validation
**Tests:** TEST-21-03, TEST-21-04, TEST-21-05
1. Request without API key → 401
2. Request with invalid API key → 401
3. Request with valid API key → 200

### Scenario 6: Rate Limiting Behavior
**Tests:** (Future enhancement)
1. Rapid requests should respect rate limits
2. 429 response when limit exceeded
3. Retry-After header handling

### Scenario 7: CORS Headers Verification
**Tests:** TEST-21-02, TEST-21-17, TEST-21-18
1. All responses include CORS headers
2. OPTIONS preflight requests handled
3. Allowed origins match configuration

### Scenario 8: Error Boundary Handling
**Tests:** TEST-21-15, TEST-21-16, TEST-21-24
1. 404 errors handled gracefully
2. 500 errors show error message
3. React Query error state triggers error UI

---

## Common Issues and Troubleshooting

### Issue: Tests fail with "Connection refused"
**Cause:** Cloudflare Worker is not running locally
**Solution:**
```bash
cd ../infra/cloudflare/worker
bun run dev
```

### Issue: Tests fail with 401 Unauthorized
**Cause:** API_KEY mismatch between test and worker
**Solution:** Ensure `VITE_API_KEY` in `.env.test` matches worker's `API_KEY` secret

### Issue: Tests timeout
**Cause:** Worker is slow to respond or database query is slow
**Solution:** Increase timeout in `vitest.config.ts`:
```typescript
test: {
  testTimeout: 15000, // 15 seconds
}
```

### Issue: React Query tests fail with "No QueryClient"
**Cause:** Component not wrapped with QueryClientProvider
**Solution:** Use `createWrapper` helper in tests (see examples)

### Issue: localStorage tests fail
**Cause:** jsdom doesn't fully implement localStorage API
**Solution:** Tests use a mock localStorage (see `integration-zustand.test.ts`)

---

## Test Coverage Goals

**Current Coverage (T-21):**
- API Endpoints: 100% (all 8 endpoints)
- React Query Hooks: 100% (usePositions, useAlerts, useResolveAlert)
- Zustand Store: 90% (theme persistence, partialize, error handling)
- Error Scenarios: 80% (404, 401, malformed requests)

**Future Enhancements:**
- [ ] Rate limiting tests (TEST-21-43+)
- [ ] WebSocket integration tests (for real-time updates)
- [ ] Cross-tab synchronization tests
- [ ] Offline mode tests (Service Worker)
- [ ] Performance benchmarks (API response time < 200ms)

---

## Test Data

Tests use **real data** from the Cloudflare Worker's D1 database (local development mode). The worker's test suite (`infra/cloudflare/worker/test/index.test.ts`) pre-populates test data using `beforeAll` hooks.

**Test Data Seeding:**
- Service heartbeats: 1 test service
- Active positions: 1 test position (SPY)
- Alerts: 1 test alert (critical severity)

For isolated tests, create separate test data in individual test cases.

---

## Related Documentation

- Cloudflare Worker Tests: `../infra/cloudflare/worker/test/index.test.ts`
- API Specification: `../infra/cloudflare/worker/README.md`
- React Query Patterns: `../.claude/skills/skill-react-dashboard.md`
- Testing Patterns: `../.claude/skills/skill-testing.md`

---

## Maintenance

**When adding new API endpoints:**
1. Add endpoint tests to `integration-api.test.ts`
2. Add React Query hook tests to `integration-react-query.test.tsx`
3. Update this README with test IDs and scenarios
4. Update test coverage goals

**When modifying existing endpoints:**
1. Update corresponding tests
2. Ensure backward compatibility tests pass
3. Document breaking changes

---

**Last Updated:** 2026-04-05 (TASK-21)
**Total Test Cases:** 42 API + 12 React Query + 10 Zustand = 64 tests
**Estimated Test Runtime:** ~5-10 seconds (with local worker)
