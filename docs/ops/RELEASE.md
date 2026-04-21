---
title: "Release Procedure"
tags: ["ops", "deployment", "release"]
aliases: ["Release", "Release Process"]
status: current
audience: ["operator", "developer"]
phase: "phase-7"
last-reviewed: "2026-04-21"
related:
  - "[[GO_LIVE]]"
  - "[[DR]]"
  - "[[BRANCH_PROTECTION]]"
  - "[[Trading System - Deployment Guide|DEPLOYMENT_GUIDE]]"
---

# Release Procedure

This file documents how to cut a release that ships code to **production**.
Released in Phase 7.7 together with `GO_LIVE.md`. Read them together.

## TL;DR

```bash
# On main, on the commit you want to ship:
git checkout main && git pull --ff-only origin main
git tag -a v1.2.3 -m "Release 1.2.3: <one-line summary>"
git push origin v1.2.3
```

That single tag push triggers production deploys for:

- **Cloudflare Worker** → `https://trading-bot.padosoft.workers.dev`
- **Cloudflare Pages dashboard** → `https://trading-dashboard.pages.dev`

Staging is auto-deployed separately on every merge/push to `main`, so
production always trails staging by at least one merge.

---

## 1. Deploy model (post Phase 7.7)

| Event                                | Target     | Triggered workflows |
|--------------------------------------|------------|---------------------|
| `push` / merge to `main`             | staging    | `cloudflare-deploy.yml::deploy-worker-staging`, `deploy-dashboard-staging`, `ci.yml::deploy-staging` |
| `push` tag `v*.*.*`                  | production | `cloudflare-deploy.yml::deploy-worker`, `deploy-dashboard`, `ci.yml::deploy` |
| `workflow_dispatch` (manual, Actions UI) | production | same as tag push — **emergency-only** override |
| Pull request to `main`               | none       | tests only |

Windows Service binaries (supervisor + options-execution) are
**published as GitHub Actions artifacts** on every push to `main` via
`dotnet-build-test.yml::publish-services`. They are NOT auto-installed on
the service host — the operator pulls the artifact and runs the install
scripts manually per `docs/ops/GO_LIVE.md` § 2.4.

## 2. Tag format

**Required**: `vX.Y.Z` or `vX.Y.Z-suffix` (semver-style).

Examples:

| Tag          | Valid | Notes |
|--------------|-------|-------|
| `v1.0.0`     | yes   | First stable |
| `v1.2.3`     | yes   | Patch release |
| `v1.2.3-rc1` | yes   | Pre-release candidate (still deploys prod) |
| `v1.0.0-hotfix.20260421` | yes | Emergency hotfix with date disambiguation |
| `1.2.3`      | no    | Missing `v` prefix, workflow pattern `v*.*.*` won't match |
| `release-1.2` | no   | Wrong pattern |

**Recommendation**: use plain `vX.Y.Z` for scheduled releases and
`vX.Y.Z-hotfix.YYYYMMDD` for emergency hotfixes so the audit trail is
clear after-the-fact.

The tag MUST be annotated (`-a` / `-m`) not lightweight, so the tag
message ends up in the release history. Unannotated tags work but are
harder to audit (no author, no message, no timestamp on the tag itself).

## 3. Pre-release checklist

Walk these before typing `git tag`:

- [ ] `main` is green on the last CI run (`gh run list --branch=main --limit=3`).
- [ ] Staging deploy for the intended commit **succeeded** (check
      the latest `deploy-worker-staging` + `deploy-dashboard-staging`
      runs — green).
- [ ] Paper validation is still active OR has completed per
      `docs/ops/PAPER_VALIDATION.md` (if this is the first live
      release).
- [ ] Data-freshness workflow is green on its most recent run.
- [ ] The `production` GitHub Environment is configured per
      `docs/ops/BRANCH_PROTECTION.md § 4` — reviewer required.
- [ ] You have the DPAPI-wrapped `TradingMode=live` blob ready on the
      service host per `docs/ops/GO_LIVE.md § 2.3`.
- [ ] You have the DPAPI-wrapped `TradingMode=paper` rollback blob saved
      somewhere you can paste it in < 10 seconds (see GO_LIVE.md).

If any is red, do NOT cut the tag — the automation does not know about
paper validation or Windows Service binary readiness, so the burden is
on the operator.

## 4. Cutting the tag

```bash
# 1. Make sure you're on main at the commit you want to ship.
git checkout main
git pull --ff-only origin main

# 2. Verify HEAD is the commit you meant.
git log --oneline -1

# 3. Annotated tag + descriptive message. Use semver.
#    Format: "Release X.Y.Z: <imperative summary>"
git tag -a v1.0.0 -m "Release 1.0.0: Phase 7 production go-live"

# 4. Push the tag — this triggers the prod-deploy workflows.
git push origin v1.0.0
```

GitHub Actions:

1. Starts `cloudflare-deploy.yml` and `ci.yml` (and `dotnet-build-test.yml`
   if the relevant paths are touched — check the Actions tab).
2. Runs the test jobs (`test-worker`, `test-dashboard`, `dotnet-build-test`).
3. On success, triggers the **production** deploy jobs which enter a
   pending state waiting for the `production` environment reviewer.
4. You (as the configured reviewer) click **Review deployments** in the
   Actions UI and approve — the deploy runs.

> **Reviewer note**: even though this is a solo-op project, the
> required-reviewer rule on the `production` environment is what
> enforces "human in the loop before prod". Do NOT disable it.
> Approving is one click — the rail is cheap.

## 5. Verifying the release

After the deploy jobs complete:

```bash
# Staging should already be at the new code (merged before the tag).
curl -sS -H "X-Api-Key: $STAGING_KEY" \
  "https://ts-staging.padosoft.workers.dev/api/health" | jq .

# Production should now match.
curl -sS -H "X-Api-Key: $PRODUCTION_KEY" \
  "https://trading-bot.padosoft.workers.dev/api/health" | jq .

# Version endpoint — confirm the commit SHA matches your tag's commit.
curl -sS -H "X-Api-Key: $PRODUCTION_KEY" \
  "https://trading-bot.padosoft.workers.dev/api/meta" | jq .
```

Dashboard smoke:

1. Open `https://trading-dashboard.pages.dev/`.
2. Semaphore widget renders a color.
3. Performance chart has live data points with a timestamp within the
   last market-data cycle.

If any check fails, **rollback** per `docs/ops/GO_LIVE.md § 4` — the
sub-60s paper-mode flip applies to Cloudflare deploys too via the
`cloudflare/pages-action` rollback + `wrangler rollback` paths.

## 6. Rolling back a bad release

Two independent surfaces:

**Cloudflare Worker** — use `wrangler rollback`:

```bash
cd infra/cloudflare/worker
bunx wrangler rollback --env production
# Pick the previous deployment from the list shown.
```

**Cloudflare Pages dashboard** — use the Cloudflare dashboard UI:

1. Cloudflare dashboard → Pages → `trading-dashboard` project.
2. Deployments tab → find the previous good deployment.
3. Three-dot menu → **Rollback to this deployment**.

Then delete (or mark as withdrawn) the bad tag:

```bash
# Optional but highly recommended for audit clarity.
git tag -d v1.0.0                # local
git push origin :refs/tags/v1.0.0 # remote

# Cut a new tag FROM the good commit with an incremented patch version.
# NEVER re-tag the same version — it confuses caches, CI artifacts, and
# anyone reading `git tag --list`.
git tag -a v1.0.1 -m "Hotfix 1.0.1: revert <bad-change>"
git push origin v1.0.1
```

## 7. Emergency workflow_dispatch (the escape hatch)

`workflow_dispatch` on the production jobs still works. Use it ONLY if
the tag workflow is itself broken (e.g. a bug in the deploy job that
you've just fixed on main but haven't cut a tag for yet).

1. GitHub → Actions → `CI/CD` (or `Cloudflare Worker & Dashboard Deploy`).
2. **Run workflow** → Branch: `main` → **Run workflow**.
3. Approve the production environment gate when it prompts.

This deploys whatever is on the selected ref — it bypasses the "every
prod deploy is a tagged release" discipline. Treat every use as an
incident: capture the reason in the post-live retro (`GO_LIVE.md § 6`).

## 8. Changelog discipline (recommended, not required)

When cutting a tag, update `CHANGELOG.md` (if present) or include a
short changelog in the tag message:

```bash
git tag -a v1.2.3 -m "Release 1.2.3
- Fix: semaphore cache TTL doubled under load (#412)
- Feat: add VIX3M historical chart to dashboard (#418)
- Chore: upgrade wrangler to 4.83 (#420)"
```

The tag message is visible in `git log --tags` and via the GitHub
Releases UI, so a readable tag message becomes the source of truth for
"what changed in this release".

## 9. Cross-references

- `docs/ops/GO_LIVE.md` — the service-host-side flip to `TradingMode=live`
  after the CI deploy completes.
- `docs/ops/BRANCH_PROTECTION.md` — the GitHub Environment + protection
  rules this release flow assumes are active.
- `docs/ops/SECRETS.md` § GitHub Actions secrets — the CI-side secrets
  (including `CLOUDFLARE_API_TOKEN`) the deploy jobs need.
- `docs/ops/DR.md` — what to do if a deploy corrupts production data.
- `docs/ops/RUNBOOK.md` Playbook 9 — first-24h-live incident response.
