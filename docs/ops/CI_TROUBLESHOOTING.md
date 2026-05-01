---
title: "CI/CD Troubleshooting Guide"
tags: ["ops", "ci-cd", "troubleshooting"]
status: current
audience: ["developer", "operator"]
last-reviewed: "2026-05-01"
related:
  - "[[DEPLOYMENT_GUIDE]]"
  - "[[SECRETS]]"
---

# CI/CD Troubleshooting Guide

Common issues and solutions for GitHub Actions CI/CD workflows.

---

## Deployment Workflows Failing with "CLOUDFLARE_API_TOKEN required"

**Symptom:**
```
ERROR: In a non-interactive environment, it's necessary to set a 
CLOUDFLARE_API_TOKEN environment variable for wrangler to work.
```

**Cause:**
Deployment workflows (`ci.yml`, `cloudflare-deploy.yml`) require Cloudflare credentials that are not configured in GitHub Secrets.

**Solution:**

### Option A: Configure Secrets (for production deployments)

1. Generate Cloudflare API token:
   - Go to https://dash.cloudflare.com/profile/api-tokens
   - Create token with permissions: `Workers Scripts:Edit`, `Pages:Edit`, `Account Settings:Read`

2. Add to GitHub Secrets:
   ```
   Repository Settings → Secrets and variables → Actions → New repository secret
   
   Name: CLOUDFLARE_API_TOKEN
   Value: <your-token>
   
   Name: CLOUDFLARE_ACCOUNT_ID
   Value: <your-account-id>
   ```

3. Push to trigger workflows - deployments will activate

### Option B: Accept Skipped Deployments (for development)

**As of 2026-05-01**, workflows automatically skip deployment jobs when secrets are missing:

- ✅ All test jobs run normally
- ⏭️ Deployment jobs skip with warning: `⚠️ CLOUDFLARE_API_TOKEN not configured - skipping deployment`
- ✅ CI passes green

This is the expected behavior for:
- Forks without Cloudflare access
- Development branches
- Initial repository setup

**No action required** - CI will remain green.

---

## Playwright E2E Tests Failing

### Test: `theme-toggle.spec.ts` - Theme Persistence

**Symptom:**
```
Expected: "dark"
Received: "light"
```

**Cause:**
Multiple theme stores with conflicting localStorage keys:
- `themeStore`: uses `ts-theme` key
- `uiStore`: uses `trading-ui` key (legacy)

**Fix Applied (2026-05-01):**
- Aligned `index.html` anti-flash script with `themeStore` (`ts-theme` key)
- Removed conflicting theme initialization from `main.tsx`
- All theme logic now uses single store

**If regression occurs:**
1. Verify `dashboard/index.html` line 16 reads `'ts-theme'`
2. Verify `dashboard/src/stores/themeStore.ts` STORAGE_KEY is `'ts-theme'`
3. Verify `dashboard/src/main.tsx` does NOT initialize from `uiStore.theme`

### Test: `strategy-wizard.spec.ts` - Route Initialization

**Symptom:**
```
Error: element(s) not found
Locator: getByRole('heading', { name: /nuova strategia/i })
```

**Cause:**
`App.tsx` initialized route state with hardcoded `'/'`, ignoring `window.location.pathname`.

**Fix Applied (2026-05-01):**
```tsx
// OLD (broken direct navigation)
const [currentRoute, setCurrentRoute] = useState<Route>('/')

// NEW (supports direct navigation + E2E tests)
const [currentRoute, setCurrentRoute] = useState<Route>(
  window.location.pathname as Route || '/'
)
```

**If regression occurs:**
1. Check `dashboard/src/App.tsx` line ~47
2. Ensure `useState` reads `window.location.pathname`
3. Run `npx playwright test strategy-wizard` to verify

---

## Verifying CI is Fully Green

**Pre-merge checklist:**

```bash
# Check all workflow runs
gh run list --branch <branch-name> --limit 10

# Check specific PR checks
gh pr checks <PR-number>

# Expected results:
✅ .NET Build & Test: pass
✅ Cloudflare Worker Build & Test: pass  
✅ Dashboard Build & Test: pass
✅ Playwright E2E: pass (14/14 tests)
✅ Security Checks: pass
⏭️ Deploy jobs: skipping (if secrets not configured) OR pass
```

**Required for merge:**
- All test jobs MUST pass
- Deployment jobs can be skipped (if no secrets) or pass

**NOT required:**
- `data-freshness.yml` - scheduled workflow, unrelated to code changes

---

## Related Documentation

- [[SECRETS]] - Full secret configuration guide
- [[DEPLOYMENT_GUIDE]] - Deployment procedures
- [[RUNBOOK]] - Operational playbooks
- Phase 7.6 E2E test contracts: `dashboard/tests/e2e/fixtures/api-mock.ts`

---

*Last updated: 2026-05-01 after PR #21 (deployment secrets optional + E2E fixes)*
