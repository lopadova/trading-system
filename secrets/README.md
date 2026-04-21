# secrets/ — Cloudflare Worker provisioning staging area

This directory is **gitignored** (except for this README and `.env.example`).
It holds `.env.<environment>` files consumed by
[`scripts/provision-secrets.sh`](../scripts/provision-secrets.sh) /
[`scripts/Provision-Secrets.ps1`](../scripts/Provision-Secrets.ps1), which
feed each key to `bunx wrangler secret put` for the Cloudflare Worker.

## Files

| File | Purpose | Committed? |
|------|---------|------------|
| `.env.example` | Template listing every required secret (placeholder values). | **YES** |
| `.env.staging` | Real staging secrets. | **NO** (gitignored) |
| `.env.production` | Real production secrets. | **NO** (gitignored) |

## Workflow

```bash
# 1. Copy the template
cp secrets/.env.example secrets/.env.staging

# 2. Fill in real values (use an editor that doesn't sync to cloud)
$EDITOR secrets/.env.staging

# 3. Push to Cloudflare
./scripts/provision-secrets.sh staging

# 4. Verify
bunx wrangler secret list --env staging --config infra/cloudflare/worker/wrangler.toml
```

## Rotation

Every 90 days (see [`docs/ops/SECRETS.md`](../docs/ops/SECRETS.md)): regenerate
values at the source (Cloudflare / Telegram / Discord / Anthropic dashboards),
update `secrets/.env.<env>`, re-run `provision-secrets.sh <env>`, restart
services, monitor 10 minutes.

## Never

- Commit `.env.staging` / `.env.production` (the `secrets/` glob catches them,
  but verify with `git check-ignore secrets/.env.staging` before pushing).
- Paste secret values into chat, issues, or PR descriptions.
- Share `.env` files via email or unencrypted cloud drives — use a password
  manager with file-attachment support (1Password, Bitwarden).
