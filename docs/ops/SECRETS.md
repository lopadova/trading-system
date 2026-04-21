---
title: "Secrets Management"
tags: ["ops", "security", "runbook"]
aliases: ["SECRETS", "Secrets", "Secret Rotation"]
status: current
audience: ["operator"]
phase: "phase-7.5"
last-reviewed: "2026-04-21"
related:
  - "[[GO_LIVE]]"
  - "[[DR]]"
  - "[[RUNBOOK]]"
  - "[[Configuration Reference|CONFIGURATION]]"
---

# Secrets Management

> Operator-facing guide to the Trading System secret pipeline (Phase 7.5).
> How secrets flow, how to provision them, how to rotate them, how to recover
> from lost key material. Keep this open on the second monitor during any
> rotation or incident involving auth.
>
> Last updated: 2026-04-20.

---

## Secret map

| Secret                 | Consumer           | Storage (prod)                       | Storage (staging)                    |
|------------------------|--------------------|--------------------------------------|--------------------------------------|
| `API_KEY`              | Worker + .NET      | Cloudflare secret + DPAPI appsettings | Cloudflare secret + DPAPI appsettings |
| `ANTHROPIC_API_KEY`    | Worker             | Cloudflare secret                    | Cloudflare secret                    |
| `TELEGRAM_BOT_TOKEN`   | Worker + .NET      | Cloudflare secret + DPAPI appsettings | Cloudflare secret + DPAPI appsettings |
| `DISCORD_BOT_TOKEN`    | Worker + .NET      | Cloudflare secret + DPAPI appsettings | Cloudflare secret + DPAPI appsettings |
| `DISCORD_PUBLIC_KEY`   | Worker + .NET      | Cloudflare secret + DPAPI appsettings | Cloudflare secret + DPAPI appsettings |
| `BOT_WHITELIST`        | Worker             | Cloudflare secret                    | Cloudflare secret                    |
| `SENTRY_DSN`           | Worker + Dashboard | Cloudflare secret (env-distinct)     | Cloudflare secret (env-distinct)     |
| `Smtp:Password`        | .NET supervisor    | DPAPI appsettings                    | DPAPI appsettings                    |
| `Cloudflare:ApiKey`    | .NET (both)        | DPAPI appsettings                    | DPAPI appsettings                    |

**Two storage layers**:

1. **Cloudflare Worker** secrets live inside Cloudflare (never on disk locally).
   Pushed via `bunx wrangler secret put` or the bulk script
   `scripts/provision-secrets.sh <env>`.

2. **.NET service** secrets live inside `appsettings.<Environment>.json`
   (gitignored) but wrapped via Windows DPAPI: the JSON file contains
   `DPAPI:<base64>` markers, not cleartext. The runtime decrypts transparently
   through `EncryptedConfigProvider` (see
   `src/SharedKernel/Configuration/EncryptedConfigProvider.cs`).

---

## Initial provisioning — Cloudflare Worker

### One-time setup

```bash
# 1. Copy the template.
cp secrets/.env.example secrets/.env.production

# 2. Fill in real values. Use a good editor (no cloud sync).
$EDITOR secrets/.env.production

# 3. Push all secrets in one shot.
./scripts/provision-secrets.sh production
# Windows equivalent:
.\scripts\Provision-Secrets.ps1 -Environment production

# 4. Verify
bunx wrangler secret list --config infra/cloudflare/worker/wrangler.toml
```

### Staging setup

```bash
# Provision staging D1 (idempotent — no-op if already wired).
./scripts/provision-d1-staging.sh

# Copy template and fill.
cp secrets/.env.example secrets/.env.staging
$EDITOR secrets/.env.staging

# Push.
./scripts/provision-secrets.sh staging

# Deploy staging worker.
(cd infra/cloudflare/worker && bunx wrangler deploy --env staging)
```

---

## Initial provisioning — .NET services (DPAPI)

The DPAPI-wrapped values in `appsettings.{Environment}.json` must be generated
**on the same machine** that will run the Windows Service, because
`DataProtectionScope.LocalMachine` keys are tied to that machine's Windows
install. Running the CLI on a different box produces a blob the service cannot
decrypt.

### Wrap a single value

```powershell
# Example: wrapping a Cloudflare API key.
# NOTE: `echo -n` is a bash-ism — PowerShell has no `-n` switch on
# echo/Write-Output, so copy-pasting `echo -n "..."` here would send the
# literal `-n <secret>` bytes to the tool. Use the native pipe; the tool
# trims a single trailing \r\n automatically so the wrapped bytes are
# exactly the secret.
'YOUR_CF_API_KEY_HERE' | dotnet run `
  --project src/Tools/EncryptConfigValue `
  --configuration Release

# Output:
# DPAPI:AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA...
```

### Paste into the appsettings file

Edit `src/TradingSupervisorService/appsettings.Staging.json` (or `Production`):

```jsonc
{
  "Cloudflare": {
    "WorkerUrl": "https://ts-staging.padosoft.workers.dev",
    "ApiKey": "DPAPI:AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA..."
  }
}
```

At runtime, `AddEncryptedProvider()` (wired in `Program.cs`) unwraps the
`DPAPI:` prefix transparently — downstream code reads
`config["Cloudflare:ApiKey"]` and gets the cleartext value.

### Why LocalMachine (not CurrentUser)?

The Windows Service runs under a different identity (`LocalSystem`,
`NetworkService`, or a dedicated service account) than the operator who
edits the JSON file. A `CurrentUser` blob created by the operator would be
**undecryptable** by the service account. `LocalMachine` scope sidesteps
this by tying the key to the Windows install itself — any process on the
box can decrypt.

Trade-off: anyone with local admin on the box can also decrypt. That's
acceptable because local admin == full service control anyway; the attacker
could just read the process memory or replace the binary.

---

## Rotation — 90-day cadence

**Why 90 days?** Strikes a balance between:

- **Security** — bounds blast radius of a leaked secret to ~3 months.
- **Operator fatigue** — short rotations (30 days) devolve into autopilot
  "click-next" sessions where the operator stops noticing anomalies.
- **External provider tolerance** — Cloudflare, Telegram, Discord all
  tolerate manual rotation at this cadence without rate-limiting.

Calendar reminder: the GitHub issue template
`.github/ISSUE_TEMPLATE/secret-rotation.md` turns this into a recurring
quarterly ticket. Instantiate one every 90 days; close it only after every
box in the checklist is green.

### Procedure

Per secret (repeat for each one that is due):

1. **Generate new value at the source**. E.g. for Telegram: `/newtoken`
   with BotFather → revoke old token only AFTER the new one is deployed.
2. **Update the cleartext file**:
   ```bash
   $EDITOR secrets/.env.production
   ```
3. **Re-wrap for .NET** (if applicable — the `Smtp:Password`,
   `Cloudflare:ApiKey`, etc. live in appsettings):
   ```powershell
   # `echo -n` does NOT work in PowerShell — use the native pipe; the
   # tool strips a single trailing \r\n so the wrapped bytes match the
   # secret exactly. See the "Wrap a single value" section above.
   'NEW_VALUE' | dotnet run --project src/Tools/EncryptConfigValue
   # Paste the output into appsettings.Production.json.
   ```
4. **Push to Cloudflare**:
   ```bash
   ./scripts/provision-secrets.sh production
   ```
5. **Restart services**:
   ```powershell
   Restart-Service TradingSupervisorService
   Restart-Service OptionsExecutionService
   ```
6. **Monitor for 10 minutes**:
   - `Get-Content logs/supervisor-*.log -Tail 200 -Wait` — look for no 401 spikes.
   - Cloudflare Live tail — look for no AUTH_FAILED metric increment.
   - Dashboard widgets — numbers should refresh without "fallback-mock" header.
7. **Revoke the OLD secret at the source**.

### Rollback

If step 6 surfaces errors:

1. Copy the OLD value back from your password manager (you DID save it before
   rotating, right? — if not, see § Recovery).
2. Re-run steps 2-5 with the old value.
3. Re-verify.
4. Open a Sentry issue describing what went wrong; investigate before
   scheduling the next rotation.

---

## Recovery — lost key material

### Lost DPAPI blob (can't decrypt appsettings.{Env}.json)

Symptoms: service startup fails with

```
InvalidOperationException: Failed to decrypt DPAPI payload.
Likely causes: blob was created under a different DataProtectionScope,
on a different machine, or has been corrupted.
```

Root causes, in decreasing order of likelihood:

1. **Machine rebuilt / re-imaged**. `LocalMachine` DPAPI keys live in
   `C:\Windows\System32\Microsoft\Protect\S-1-5-18\User` and do NOT survive
   a reinstall. Fix: re-wrap each secret with the EncryptConfigValue CLI
   on the new box. (Plan ahead: document which secrets each service needs
   in this file — done above.)
2. **Blob hand-edited**. Base64 character dropped/inserted during a copy-paste.
   Fix: re-wrap and re-paste.
3. **Scope mismatch**. Someone wrapped with `--scope=CurrentUser` but the
   service runs under a different account. Fix: re-wrap without the flag
   (defaults to LocalMachine).

### Lost Cloudflare secret

Cloudflare secrets cannot be read back — only overwritten. If you don't
have the cleartext value stored elsewhere (password manager, secure vault):

1. Generate a brand-new value at the source provider.
2. Update `secrets/.env.<env>`, run `provision-secrets.sh <env>`.
3. Update the matching DPAPI appsettings entry for the .NET side (if
   applicable — `API_KEY` is shared).
4. Restart services.

**Prevention**: keep a password-manager entry (1Password, Bitwarden) with
BOTH the current value AND the previous value for every secret in the map
above. Update it during step 1 of every rotation, BEFORE touching any
config file.

---

## GitHub Actions secrets (CI/CD)

Phase 7.7 re-enabled the deploy workflows. They need a handful of
secrets stored at the GitHub repository level (or scoped to a
GitHub Environment) so `wrangler deploy`, `cloudflare/pages-action`,
and the Telegram alert step can authenticate. Without these, the CI
jobs fail with "in a non-interactive environment, it's necessary to
set a CLOUDFLARE_API_TOKEN environment variable" or equivalent.

### Required secrets

| Name                    | Used by                                      | Where to get it |
|-------------------------|----------------------------------------------|-----------------|
| `CLOUDFLARE_API_TOKEN`  | Every wrangler / Pages deploy job            | Cloudflare dashboard → **My Profile → API Tokens → Create Token**. Use the "Edit Cloudflare Workers" template and add these additional permissions: **Account → Workers Scripts:Edit**, **Account → Workers KV Storage:Edit**, **Account → D1:Edit**, **Account → Cloudflare Pages:Edit**, **Account → Account Analytics:Read**. Scope to the specific account. |
| `CLOUDFLARE_ACCOUNT_ID` | `cloudflare/pages-action@v1` (dashboard deploys) | Cloudflare dashboard → any site → right sidebar, "Account ID". |
| `TELEGRAM_BOT_TOKEN`    | `data-freshness.yml` failure alert           | BotFather `/mybots → <bot> → API Token`. |
| `TELEGRAM_CHAT_ID`      | `data-freshness.yml` failure alert           | Send a message to the bot, then `curl https://api.telegram.org/bot<TOKEN>/getUpdates` — the numeric `chat.id` from the latest update. |

`GITHUB_TOKEN` is auto-provided by Actions; no setup needed.

### Where to store them

**Repository secrets** (simplest, applies to every workflow):

1. GitHub repo → **Settings → Secrets and variables → Actions**.
2. Click **New repository secret**.
3. Enter `Name` (exactly matching the table above) and `Value`.
4. Repeat for each required secret.

**Environment secrets** (recommended for `CLOUDFLARE_API_TOKEN` if
you want a reviewer-required gate per Phase 7.7's `production` environment):

1. GitHub repo → **Settings → Environments**.
2. Select the `production` environment (create it if missing — see
   `docs/ops/BRANCH_PROTECTION.md § 4`).
3. Add the secret inside the environment. Repeat for `staging` if you
   want separate tokens per environment.

**Scope recommendation**: a single `CLOUDFLARE_API_TOKEN` at the
repository level is the simplest working setup. Per-environment tokens
buy you isolation (a leaked staging token cannot deploy prod) — worth
it if you want the rail but not required for MVP.

### First-time CI provisioning — step by step

```
1. Cloudflare → create API token per table above.
   Copy the token string (shown once — if you lose it, create a new one).

2. Cloudflare → note the Account ID from the right-hand sidebar of any
   resource page.

3. BotFather → /mybots → <your bot> → API Token. Copy.

4. Send any message to your bot from YOUR Telegram account, then:
     curl -s https://api.telegram.org/bot<BOT_TOKEN>/getUpdates | jq '.result[-1].message.chat.id'
   Copy the numeric ID.

5. GitHub repo → Settings → Secrets and variables → Actions.
   Create these four secrets:
     CLOUDFLARE_API_TOKEN  = <step 1>
     CLOUDFLARE_ACCOUNT_ID = <step 2>
     TELEGRAM_BOT_TOKEN    = <step 3>
     TELEGRAM_CHAT_ID      = <step 4>

6. Trigger a test deploy:
     git commit --allow-empty -m "ci: test deploy after secret provisioning"
     git push origin main
   → Watch the Actions tab. deploy-worker-staging should succeed.

7. If step 6 fails with "non-interactive environment" or 401:
   → token lacks a required permission. Delete it and re-create with
     the full permission list above.
```

### Rotating CI secrets

CI secrets follow the same 90-day cadence as runtime secrets. On rotation:

1. Cloudflare → create the NEW token. Leave the old one active for now.
2. GitHub → update `CLOUDFLARE_API_TOKEN` value (same secret name).
3. Trigger a smoke deploy (`workflow_dispatch` on `Cloudflare Worker &
   Dashboard Deploy` for staging) — confirm success.
4. Cloudflare → revoke the old token.
5. Record the rotation in the quarterly
   `.github/ISSUE_TEMPLATE/secret-rotation.md` checklist.

### Required scopes on the Cloudflare token

Verify the token has at minimum these permissions (paste into the
"Edit Cloudflare Workers" template + additions):

- **Account → Workers Scripts: Edit** — `wrangler deploy` + `wrangler rollback`
- **Account → Workers KV Storage: Edit** — KV bindings
- **Account → D1: Edit** — `wrangler d1 execute/export`, daily backup script
- **Account → Cloudflare Pages: Edit** — dashboard deploys
- **Account → Account Analytics: Read** — SLO measurement queries
- **Zone → Zone: Read** (if routes attach to custom domains)

Missing any of these → the deploy fails mid-run with a Cloudflare API
4xx, NOT with the "non-interactive" error. Read the actual failure
message — it names the scope that was refused.

---

## Never

- Commit `.env.staging` / `.env.production` / `appsettings.Staging.json` /
  `appsettings.Production.json` — all are gitignored; verify before every
  push with `git check-ignore <path>` if in doubt.
- Email / Slack / screenshot a cleartext secret.
- Wrap a secret with `--scope=CurrentUser` unless you are 100% certain the
  service runs under your account (it doesn't, by default).
- Rotate more than one secret per service at the same time — you won't be
  able to tell which one broke the service if verification fails.
- Use the same value for staging + production. Compromised staging →
  compromised production is a preventable escalation.

---

*Related: [`docs/ops/RUNBOOK.md`](./RUNBOOK.md) § Rotating secrets,
[`secrets/README.md`](../../secrets/README.md).*
