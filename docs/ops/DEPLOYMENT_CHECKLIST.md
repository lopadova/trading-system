---
title: "Pre-Deployment Checklist"
tags: ["ops", "deployment", "workflow"]
status: current
audience: ["developer", "operator"]
last-reviewed: "2026-05-01"
related:
  - "[[DEPLOYMENT_GUIDE]]"
  - "[[RELEASE]]"
  - "[[GO_LIVE]]"
  - "[[CI_TROUBLESHOOTING]]"
---

# Pre-Deployment Checklist

Quick checklist before deploying to staging or production environments.

---

## Every Deployment (Staging + Production)

### Code Quality

- [ ] **CI fully green on target branch**
  ```bash
  gh run list --branch <branch> --limit 5
  ```
  - ✅ .NET Build & Test: SUCCESS
  - ✅ Cloudflare Worker Build & Test: SUCCESS
  - ✅ Dashboard Build & Test: SUCCESS
  - ✅ Playwright E2E: SUCCESS (14/14 tests)
  - ✅ Security Checks: SUCCESS

- [ ] **Zero ESLint warnings**
  ```bash
  cd dashboard && npm run lint
  # Must output: 0 errors, 0 warnings
  ```

- [ ] **Zero TypeScript errors**
  ```bash
  cd dashboard && npm run typecheck
  cd infra/cloudflare/worker && bunx tsc --noEmit
  # Both must output: 0 errors
  ```

- [ ] **.NET build clean**
  ```bash
  dotnet build TradingSystem.sln -c Release
  # Must output: Avvisi: 0  Errori: 0
  ```

### Safety Checks

- [ ] **TradingMode verified**
  ```bash
  # Production: depends on environment (paper until go-live)
  # Staging: MUST be "paper"
  grep -A1 '"TradingMode"' src/OptionsExecutionService/appsettings.json
  ```

- [ ] **No hardcoded secrets in code**
  ```bash
  # CI runs this, but verify locally:
  grep -r "password\s*=\s*['\"]" --include="*.cs" --include="*.ts" src/ infra/ dashboard/ | grep -v "appsettings.json" | grep -v "example"
  ```

- [ ] **Sensitive paths in .gitignore**
  ```bash
  grep "strategies/private/" .gitignore
  grep "*.db" .gitignore
  grep "appsettings.*.json" .gitignore
  ```

### Dependencies

- [ ] **Lockfiles committed**
  ```bash
  git status | grep -E "package-lock.json|bun.lockb"
  # Should NOT show modified lockfiles (if they are, commit them first)
  ```

- [ ] **No vulnerable dependencies** (if `npm audit` shows CRITICAL)
  ```bash
  cd dashboard && npm audit --production
  cd infra/cloudflare/worker && npm audit --production
  ```

---

## Staging Deployment Only

- [ ] **GitHub Actions secrets configured** (if deploying via CI)
  - `CLOUDFLARE_API_TOKEN` set in GitHub Secrets
  - `CLOUDFLARE_ACCOUNT_ID` set in GitHub Secrets
  - Verify: deployment jobs should execute (not skip)

- [ ] **Cloudflare Worker secrets provisioned** (if deploying manually)
  ```bash
  bunx wrangler secret list --env staging
  # Should show: API_KEY, ANTHROPIC_API_KEY, TELEGRAM_BOT_TOKEN, etc.
  ```

- [ ] **D1 staging database exists**
  ```bash
  bunx wrangler d1 list | grep trading-system-staging
  ```

---

## Production Deployment Only

**⚠️ Production deployments require additional verification per [[GO_LIVE]] and [[RELEASE]]**

- [ ] **All staging checks above PASS**

- [ ] **Tag-based release** (not direct push to main)
  ```bash
  git describe --tags
  # Should show: v<major>.<minor>.<patch>
  ```

- [ ] **CHANGELOG updated**
  ```bash
  head -20 CHANGELOG.md
  # Should show latest version entry
  ```

- [ ] **Paper validation completed** (for paper → live flip)
  - See [[PAPER_VALIDATION]] and [[GO_LIVE]]
  - 14-day clean run documented
  - Sign-off obtained

- [ ] **Backup verified**
  ```bash
  # Check latest backup timestamp
  ls -lh backups/d1/ | head -5
  ```

- [ ] **Rollback plan confirmed**
  - Previous working version tag identified
  - Rollback procedure tested (< 60s target)
  - On-call contact available

- [ ] **Production secrets provisioned**
  ```bash
  bunx wrangler secret list --env production
  # All required secrets present
  ```

- [ ] **GitHub environment protection enabled**
  - Repository Settings → Environments → production
  - Required reviewers configured
  - Deployment branch limited to tags matching `v*.*.*`

---

## Post-Deployment Verification

### Staging

- [ ] **Worker deployed successfully**
  ```bash
  curl https://ts-staging.padosoft.workers.dev/api/heartbeats | jq
  # Should return JSON, not error
  ```

- [ ] **Dashboard accessible**
  ```bash
  curl -I https://trading-dashboard-staging.pages.dev
  # Should return: HTTP/2 200
  ```

- [ ] **Basic smoke test**
  - Navigate to dashboard staging URL
  - Verify HomePage loads without errors
  - Check browser console for JavaScript errors
  - Verify API widgets show data (not "Something went wrong")

### Production

- [ ] **All staging verifications above**

- [ ] **Sentry error rate normal** (< 5% within 1 hour)
  - Check Sentry dashboard for new error spikes

- [ ] **Logs flowing**
  ```bash
  # Check service logs on production machine
  Get-Content C:\TradingSystem\logs\options-execution-*.log -Tail 50
  ```

- [ ] **No critical alerts triggered**
  - Check Telegram/Discord for alert notifications
  - Verify no "service down" or "high delta risk" alerts

---

## Emergency Rollback

If post-deployment verification fails:

### Staging (low stakes)
```bash
# Redeploy previous version
git checkout <previous-commit>
cd infra/cloudflare/worker && bunx wrangler deploy --env staging
cd dashboard && bunx wrangler pages deploy dist/ --project-name trading-dashboard-staging
```

### Production (time-critical)
1. **STOP**: Notify on-call immediately
2. **Follow [[DR]] § 3 (Rollback)**
3. **Timeline**: Must complete rollback in < 60 seconds
4. **Post-rollback**: Root cause analysis (create incident report)

---

## Quick Reference

**Before any deployment:**
```bash
# 1-liner check
npm run lint && npm run typecheck && dotnet build -c Release && gh run list --limit 3
```

**Staging deploy (manual):**
```bash
cd infra/cloudflare/worker && bunx wrangler deploy --env staging
cd dashboard && npm run build && bunx wrangler pages deploy dist/ --project-name trading-dashboard-staging
```

**Production deploy (manual - use with caution):**
```bash
# Prefer CI/CD via git tag: git tag v1.2.3 && git push origin v1.2.3
cd infra/cloudflare/worker && bunx wrangler deploy
cd dashboard && npm run build && bunx wrangler pages deploy dist/ --project-name trading-dashboard
```

---

*Last updated: 2026-05-01 after PR #21 (E2E fixes + CI deployment tolerance)*
