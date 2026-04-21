---
title: "Dashboard E2E (Playwright)"
tags: ["dev", "dashboard", "testing"]
status: current
audience: ["developer"]
phase: "phase-7.6"
last-reviewed: "2026-04-21"
---

# Dashboard E2E (Playwright)

Phase 7.6 deliverable. Five critical-journey specs that exercise the top
user flows without requiring a live Cloudflare Worker.

## Run

```bash
# Install browsers on first run (one-off)
cd dashboard
npx playwright install --with-deps chromium

# Full suite against a local dev server (Vite is spun up automatically)
npm run test:e2e

# Point at a preview URL instead (no local server spin-up)
PLAYWRIGHT_BASE_URL=https://preview.trading-dashboard.pages.dev \
  SKIP_WEBSERVER=1 \
  npx playwright test

# Single spec, headed, single viewport — useful for debugging
npx playwright test tests/e2e/theme-toggle.spec.ts \
  --project=chromium-1920x1080 \
  --headed
```

## Specs

| Spec | What it verifies |
|---|---|
| `asset-filter.spec.ts` | Zustand in-memory asset bucket survives within-session sidebar nav. |
| `theme-toggle.spec.ts` | Light/dark flip persists to localStorage and survives reload. |
| `sidebar-navigation.spec.ts` | Every nav item loads with no console errors and no asset 404s. |
| `semaphore-widget.spec.ts` | Widget renders `green|orange|red` driven by the API response. |
| `strategy-wizard.spec.ts` | Wizard opens to step 1; graceful path to the review screen. |

## API mocks

All HTTP to `/api/*` is intercepted by `fixtures/api-mock.ts`. The fixture
returns deterministic canned JSON for known endpoints and a 204 for
everything else — which means unmocked widgets render their empty state
rather than crash. When a route shape changes on the backend, the
corresponding contract test in `tests/Contract/` will fail first; update
the mock here when that happens.

## Viewport matrix

Desktop-only: 1920×1080 and 1366×768. Mobile is out of scope for the kit.

## CI

See `.github/workflows/playwright-e2e.yml`. Runs on PR-merge-to-main and
on manual dispatch. Not yet wired as a required check — Phase 7.7 gates
that.
