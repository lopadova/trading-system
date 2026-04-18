# Dashboard Testing Guide

## Problem: Bun vitest doesn't support DOM environments

Bun's vitest implementation doesn't properly load jsdom/happy-dom, causing all React component tests to fail with:

```
ReferenceError: document is not defined
```

## Solution: Use Node.js vitest

```bash
# Run tests with Node vitest (proper DOM support)
npm test

# Watch mode
npm run test:watch

# Coverage
npm run test:coverage

# UI mode
npm run test:ui
```

## Why not Bun for dashboard tests?

- **Worker tests** (`infra/cloudflare/worker`): Use Bun ✅ (no DOM needed)
- **Dashboard tests** (`dashboard/`): Use Node.js npm ✅ (React requires DOM)

## Test files excluded

Integration tests requiring worker running on localhost:8787 are excluded by default:
- `test/integration-api.test.ts`
- `test/integration-react-query.test.tsx`
- `test/integration-zustand.test.ts`

To run integration tests, start the worker first:
```bash
cd ../infra/cloudflare/worker
npm run dev      # Worker runs on localhost:8787
```

Then in another terminal:
```bash
cd dashboard
npm test test/integration-api.test.ts
```
