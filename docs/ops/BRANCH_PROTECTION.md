---
title: "Branch Protection Settings"
tags: ["ops", "dev", "release"]
aliases: ["BRANCH_PROTECTION", "Branch Protection"]
status: current
audience: ["operator", "developer"]
phase: "phase-7.7"
last-reviewed: "2026-04-21"
related:
  - "[[RELEASE]]"
  - "[[Contributing Guide|CONTRIBUTING]]"
---

# Branch Protection Settings

> Phase 7.7 deliverable. Exact GitHub UI settings Lorenzo applies to the
> `main` branch (and the `production` GitHub Environment) after this PR
> merges. These rules turn "we have CI" into "CI is the authority on
> what enters main".
>
> Audience: Lorenzo (repo owner). Everyone else: read-only reference.
>
> Last updated: 2026-04-21.

---

## 1. Why this doc exists

CI that runs on every PR but doesn't BLOCK merge is a suggestion.
Branch protection converts the suggestion into a rule. Without
this, a tired operator can (and will, eventually) bypass a red CI
check at 23:47 on a Friday. The rule lives here so the lazy path
stays the safe path.

Single-maintainer project exceptions are called out in § 3.

---

## 2. Settings to apply on `main`

Path: **GitHub → trading-system → Settings → Branches → Add rule**
(or edit an existing rule).

**Branch name pattern**: `main`

### 2.1 Require a pull request before merging

- [x] Require a pull request before merging
  - [x] **Require approvals: 0**
    - Solo-op project. Self-approve is allowed.
    - See § 3.1 for rationale.
  - [x] Dismiss stale pull request approvals when new commits are pushed
  - [ ] Require review from Code Owners
    - Not used: single maintainer, no CODEOWNERS file.
  - [x] Require approval of the most recent reviewable push
  - [x] Require conversation resolution before merging
    - Prevents merging with unresolved review comments.

### 2.2 Require status checks to pass before merging

- [x] Require status checks to pass before merging
  - [x] **Require branches to be up to date before merging**
    - Forces a rebase / merge-from-main before green — catches
      merge-conflict regressions.
  - **Required status checks** (type these exactly; they are the
    names that appear as GitHub Check runs):
    - `.NET Build & Test` (from `dotnet-build-test.yml`)
    - `Dashboard Build & Test` (from `ci.yml`)
    - `Cloudflare Worker Build & Test` (from `ci.yml`)
    - `Security Checks` (from `ci.yml`)
    - `Test Cloudflare Worker` (from `cloudflare-deploy.yml`)
    - `Test Dashboard` (from `cloudflare-deploy.yml`)
  - **Optional status checks** (not required — keep them informational
    for now):
    - `Playwright E2E` (flake risk; Phase 7.6 compromise)
    - `Data Freshness Check` (polls external state — not a PR signal)

### 2.3 Require conversation resolution

(Also listed under 2.1, re-affirming here because it appears as a
separate checkbox in the UI.)

- [x] Require conversation resolution before merging

### 2.4 Require signed commits

- [ ] Require signed commits
  - Not enabled. Claude Code commits aren't currently signed; adding
    this rule would block the AI workflow. Revisit in Phase 8+ if
    signing is wired for agent commits.

### 2.5 Require linear history

- [x] Require linear history
  - No merge commits on main. Every PR either squash-merges or
    rebase-merges. Keeps `git log --oneline` readable and bisect-friendly.

### 2.6 Require deployments to succeed

- [ ] Require deployments to succeed before merging
  - Not enabled. Deploys happen after merge, not as a merge gate
    (see `ci.yml` → `deploy-staging`).

### 2.7 Lock branch

- [ ] Lock branch
  - Not enabled; `main` remains writable under the PR rule.

### 2.8 Do not allow bypassing the above settings

- [x] **Do not allow bypassing the above settings**
  - Critical. This is the "include administrators" equivalent in the
    new UI. Without it, the repo owner can push directly and the
    whole protection is theater. See § 3.2 for why admin inclusion
    matters even for a solo-op.

### 2.9 Restrict who can push to matching branches

- [ ] Restrict who can push to matching branches
  - Not enabled. Pushes to `main` only happen via merged PRs
    anyway (per § 2.1); restricting further adds no value for a
    solo-op.

### 2.10 Allow force pushes

- [ ] Allow force pushes
  - **Do not enable.** Never force-push to main. If a commit needs
    to be retracted, use `git revert` to create a new commit. This
    keeps the history forward-only, which is what CI, observability,
    and the DR backup chain all assume.

### 2.11 Allow deletions

- [ ] Allow deletions
  - **Do not enable.** `main` should never be deletable.

---

## 3. Single-maintainer caveats

### 3.1 Self-approve is OK

Requiring `>= 1` approval with a single maintainer means Lorenzo
cannot merge anything until a second person exists — which would
block all work. The workaround is `Require approvals: 0`. Effective
self-review discipline is enforced via:

1. The `docs/ops/DAILY_OPS.md` checklist (catches post-merge regressions).
2. Mandatory CI checks (catch pre-merge regressions automatically).
3. The honor-based requirement that every PR opens the Files Changed
   tab before clicking Merge, even if nobody else will review.

If a collaborator joins the project (future), bump `Require approvals`
to 1 immediately.

### 3.2 Include administrators

Counter-intuitively, admin inclusion is MORE important for a solo-op
than for a team. A team has social friction that stops someone
from force-pushing at 2 AM; a solo-op has only the rail. Leaving
`Do not allow bypassing` UNchecked is an open invitation to future-self
to bypass CI "just this once" — the same once-is-fine reasoning
that made `if: false && ...` accumulate on deploy jobs during Phase 7.

Lorenzo explicitly keeps this rule ON. If the rule genuinely needs
to bend (e.g. a hotfix during a live incident), the operator
toggles it OFF, makes the minimal push, and toggles it back ON.
That 30-second dance is the point — it forces deliberation.

---

## 4. GitHub Environments

Path: **Settings → Environments**.

### 4.1 `production` environment

Used by the deploy jobs re-enabled in Phase 7.7 — see
`.github/workflows/ci.yml` → `deploy`,
`.github/workflows/cloudflare-deploy.yml` → `deploy-worker`,
`deploy-dashboard`.

Configure:

- **Environment name**: `production`.
- **Required reviewers**: `lopadova` (Lorenzo). With 1 reviewer
  required, production deploys wait for manual approval in the
  Actions UI — even if Lorenzo is the one who pushed the
  workflow_dispatch OR the release tag. The extra click is
  deliberate: it's the second chance to cancel a bad deploy.
- **Wait timer**: 0 minutes. (The reviewer gate is the throttle.)
- **Deployment branches and tags**: `Selected branches and tags`.
  Add TWO rules:
  - **Branch**: `main` — for `workflow_dispatch` emergency runs.
  - **Tag**: `v*.*.*` — the normal release path (see
    `docs/ops/RELEASE.md`).
  Without the tag rule the tag push will START the workflow but the
  `production` environment gate will REJECT the deploy ref and fail
  the job. This is the single most common gotcha when setting this
  up — verify both rules are listed before cutting the first tag.
- **Environment secrets**: `CLOUDFLARE_API_TOKEN`,
  `CLOUDFLARE_ACCOUNT_ID` (if used by the dashboard deploy step),
  `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`. Full reference in
  `docs/ops/SECRETS.md` § GitHub Actions secrets.

### 4.2 `staging` environment

Used by the auto-deploy jobs in Phase 7.7 — see
`.github/workflows/ci.yml` → `deploy-staging`,
`.github/workflows/cloudflare-deploy.yml` → `deploy-worker-staging`,
`deploy-dashboard-staging`.

Configure:

- **Environment name**: `staging`.
- **Required reviewers**: none. Staging is for catching issues
  BEFORE they need a reviewer; gating it defeats the purpose.
- **Wait timer**: 0 minutes.
- **Deployment branches**: `Selected branches` → `main` only.
- **Environment secrets**: `CLOUDFLARE_API_TOKEN`,
  `CLOUDFLARE_ACCOUNT_ID`. Staging uses the same API token
  as production because wrangler scopes per-environment at
  the binding level (see `wrangler.toml` `[env.staging]`).
  Phase 8+ could introduce a staging-specific token; for now,
  same token is acceptable.

---

## 5. Verification

After applying these settings:

1. Open a test PR that intentionally breaks CI (e.g. add a failing
   test in `tests/TradingSupervisorService.Tests/`). The PR MUST be
   unmergeable — the Merge button is grey with the reason stated.
2. Fix the test, push. PR becomes mergeable.
3. Merge. Observe:
   - `dotnet-build-test.yml` runs and succeeds → `publish-services`
     job fires (re-enabled in Phase 7.7).
   - `ci.yml` runs → `deploy-staging` fires, `deploy` does NOT
     (because event is `push`, not `workflow_dispatch`).
   - `cloudflare-deploy.yml` runs → `deploy-worker-staging` +
     `deploy-dashboard-staging` fire.
4. Manually trigger a production deploy via Actions →
   `CI/CD` → "Run workflow" → branch: `main`. Observe the
   production environment's pending-approval prompt. Approve.
   Deploy runs.

If any step deviates, fix the settings before relying on them.

---

## 6. Related docs

- `docs/ops/GO_LIVE.md` — precondition "CI green on main" assumes
  these rules are active.
- `.github/workflows/ci.yml`, `dotnet-build-test.yml`,
  `cloudflare-deploy.yml` — the workflows these rules gate.
- `.claude/rules/code-quality.md` — the zero-tolerance policy
  that assumes CI actually blocks merges.
