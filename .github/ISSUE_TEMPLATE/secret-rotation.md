---
name: Secret Rotation (quarterly)
about: Track the 90-day rotation of trading-system secrets per docs/ops/SECRETS.md
title: "Secret rotation — YYYY-QN (<env>)"
labels: ops, security, rotation
assignees: ''
---

> Instantiate this template once every 90 days per environment
> (production + staging). The full procedure lives in
> [`docs/ops/SECRETS.md`](../../docs/ops/SECRETS.md) — this issue is the
> checklist view so rotation status is visible to the whole team.

## Scope

- **Environment**: _production_ OR _staging_
- **Date opened**: YYYY-MM-DD
- **Operator on call**: @handle
- **Previous rotation issue**: #NNN (link)

## Pre-flight

- [ ] Previous rotation closed with all boxes green.
- [ ] Password manager entries reviewed (current + previous value on hand).
- [ ] Staging rotation performed at least 24h ago (do staging first; prod
      follows after a cool-down so any breakage hits the non-trading box).
- [ ] Maintenance window announced (for production only).

## Cloudflare Worker secrets

For each key in `secrets/.env.<env>`:

- [ ] `API_KEY` — regenerated, stored in PM, `secrets/.env.<env>` updated.
- [ ] `ANTHROPIC_API_KEY` — ditto.
- [ ] `TELEGRAM_BOT_TOKEN` — regenerated via BotFather, stored in PM.
- [ ] `DISCORD_BOT_TOKEN` — regenerated via Developer Portal, stored in PM.
- [ ] `DISCORD_PUBLIC_KEY` — matches the newly-regenerated Discord app.
- [ ] `BOT_WHITELIST` — reviewed, no stale user IDs.
- [ ] `SENTRY_DSN` — kept unchanged unless Sentry project rotated (rare).
- [ ] `./scripts/provision-secrets.sh <env>` executed successfully.
- [ ] `bunx wrangler secret list --env <env>` shows all expected keys.

## .NET service secrets (DPAPI)

Run `dotnet run --project src/Tools/EncryptConfigValue` **on the host machine**
and update `appsettings.<Environment>.json` in the publish directory:

- [ ] `Cloudflare:ApiKey` re-wrapped and pasted.
- [ ] `Telegram:BotToken` re-wrapped and pasted.
- [ ] `Bots:DiscordBotToken` re-wrapped and pasted (if Discord enabled).
- [ ] `Bots:DiscordPublicKey` re-wrapped and pasted (if Discord enabled).
- [ ] `Smtp:Password` re-wrapped and pasted (if SMTP alerts enabled).
- [ ] JSON parses cleanly (`Get-Content ... | ConvertFrom-Json` in PowerShell).

## Deploy + verify

- [ ] `Restart-Service TradingSupervisorService`
- [ ] `Restart-Service OptionsExecutionService`
- [ ] `logs/supervisor-*.log` tail for 10 min — no new 401 / decrypt errors.
- [ ] Cloudflare Live tail 10 min — no AUTH_FAILED increments beyond baseline.
- [ ] Dashboard widgets fresh (no `X-Data-Source: fallback-mock` header).
- [ ] `/api/health` on Worker returns 200 for both envs.

## Finalize

- [ ] OLD Cloudflare secret revoked at source (Telegram, Discord, Anthropic
      all let you revoke from their respective dashboards).
- [ ] PM entry marked as "previous" (keep for rollback window = 30 days).
- [ ] Next rotation issue scheduled: target date = today + 90 days.
- [ ] This issue closed with a one-line note in `CHANGELOG` or
      `knowledge/lessons-learned.md` if anything unusual happened.

## Incidents during rotation

(Fill in if anything broke. Link to Sentry issues, logs, PRs.)

_No incidents._
