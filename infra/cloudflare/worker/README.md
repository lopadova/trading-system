---
title: "Trading System Cloudflare Worker"
tags: ["dev", "worker", "reference"]
aliases: ["Worker README"]
status: current
audience: ["developer"]
last-reviewed: "2026-04-21"
related:
  - "[[Trading System Architecture|ARCHITECTURE]]"
  - "[[Trading System - Deployment Guide|DEPLOYMENT_GUIDE]]"
---

# Trading System Cloudflare Worker

Hono-based API worker for querying trading system data from D1 database.

## Features

- **Hono Framework** - Fast, lightweight web framework
- **D1 Database** - Serverless SQLite database
- **Type-safe** - Full TypeScript with strict mode
- **API Key Auth** - Secure endpoint authentication
- **Rate Limiting** - Built-in request throttling
- **CORS** - Configured for dashboard access
- **Vitest Tests** - Integration tests with D1 mocking

## Prerequisites

- [Bun](https://bun.sh) v1.0+
- [Wrangler](https://developers.cloudflare.com/workers/wrangler/) v3.0+
- Cloudflare account with Workers and D1 enabled

## Installation

```bash
cd infra/cloudflare/worker
bun install
```

## Database Setup

### Create D1 Database

```bash
wrangler d1 create trading-db
```

Copy the `database_id` from the output and update `wrangler.toml`:

```toml
[[d1_databases]]
binding = "DB"
database_name = "trading-db"
database_id = "YOUR_DATABASE_ID_HERE"
```

### Run Migrations

```bash
# Local development database
bun run migrate:local

# Production database
bun run migrate:prod
```

## Configuration

### Environment Variables

Set in `wrangler.toml`:

```toml
[vars]
DASHBOARD_ORIGIN = "https://your-dashboard-domain.com"
```

### Secrets

Worker secrets are **optional** - features degrade gracefully if missing.

**Set via Wrangler CLI** (one-time setup):

```bash
cd infra/cloudflare/worker

# Telegram Bot (optional)
bunx wrangler secret put TELEGRAM_BOT_TOKEN
# Paste: 123456789:ABCdefGHIjklMNOpqrsTUVwxyz

# Discord Bot (optional)
bunx wrangler secret put DISCORD_PUBLIC_KEY  
# Paste: a1b2c3d4e5f6789abcdef0123456789...

# Anthropic API (optional)
bunx wrangler secret put CLAUDE_API_KEY
# Paste: sk-ant-api03-YOUR_KEY_HERE
```

**Local development** (`.dev.vars` file):

```bash
# Copy template
cp .dev.vars.example .dev.vars

# Edit with your values
# infra/cloudflare/worker/.dev.vars (NOT committed to git)
TELEGRAM_BOT_TOKEN=123456789:ABCdefGHIjklMNOpqrsTUVwxyz
DISCORD_PUBLIC_KEY=a1b2c3d4e5f6789abcdef0123456789...
CLAUDE_API_KEY=sk-ant-api03-YOUR_KEY_HERE
```

**⚠️ Security**: `.dev.vars` is in `.gitignore` - never commit secrets!

**Detailed setup guides**:
- **Telegram Bot**: Create via @BotFather → `/newbot`
- **Discord Bot**: Discord Developer Portal → General Information → PUBLIC KEY
- **Anthropic API**: https://console.anthropic.com/settings/keys

See [Main README](../../../README.md#api-keys-setup-optional---features-degrade-gracefully) for detailed instructions.

### Production URL Configuration (⭐ RECOMMENDED)

**⚠️ Privacy Consideration**: The `DASHBOARD_ORIGIN` variable in `wrangler.toml` is committed to git.

**For Production**, choose one option:

**Option A: Public URL in wrangler.toml**
```toml
[env.production]
vars = { DASHBOARD_ORIGIN = "https://trading.padosoft.com" }
```

**Option B: Secret (RECOMMENDED for privacy)**
```bash
# Hide production URL from git
bunx wrangler secret put DASHBOARD_ORIGIN
# Paste: https://trading.padosoft.com
```

**✅ Use secrets if**:
- Repository is public
- You want to hide your production domain
- URL contains sensitive info

See [DEPLOYMENT_GUIDE.md](../../../docs/DEPLOYMENT_GUIDE.md#43-configure-production-url--recommended) for details.

## Development

```bash
# Start local development server
bun run dev

# Type check
bun run typecheck

# Run tests
bun run test

# Run tests in watch mode
bun run test:watch
```

The worker will be available at `http://localhost:8787`.

## API Endpoints

### Public Endpoints

- `GET /api/health` - Health check (no auth required)
- `GET /` - API documentation

### Protected Endpoints (require `X-Api-Key` header)

#### Positions

- `GET /api/positions/active` - List active positions
  - Query params: `campaign_id`, `symbol`, `strategy_name`, `limit`
- `GET /api/positions/history` - List position history
  - Query params: `campaign_id`, `position_id`, `status`, `limit`
- `GET /api/positions/:position_id` - Get single position

#### Alerts

- `GET /api/alerts` - List all alerts
  - Query params: `severity`, `alert_type`, `source_service`, `unresolved_only`, `limit`
- `GET /api/alerts/unresolved` - List unresolved alerts only
- `GET /api/alerts/:alert_id` - Get single alert

#### Heartbeats

- `GET /api/heartbeats` - List all service heartbeats
- `GET /api/heartbeats/:service_name` - Get heartbeat for specific service
- `GET /api/heartbeats/stale/:threshold_seconds` - Get stale services

## Authentication

### API Key Authentication

All protected endpoints require `X-Api-Key` header with a valid token from D1 whitelist.

**How it works**:

1. **Generate token** (256-bit random):
   ```bash
   openssl rand -hex 32
   # Output: a3f5c8d9e2b1f4a6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0
   ```

2. **Apply migrations** (creates whitelist table):
   ```bash
   bunx wrangler d1 migrations apply trading-db --remote
   ```

3. **Add to D1 whitelist**:
   ```bash
   bunx wrangler d1 execute trading-db --remote --command="
   INSERT INTO whitelist (api_key, description) 
   VALUES ('YOUR_TOKEN', 'Production Dashboard');
   "
   
   # Verify
   bunx wrangler d1 execute trading-db --remote --command="
   SELECT api_key, description FROM whitelist;
   "
   ```

3. **Use in requests**:
   ```bash
   curl -H "X-Api-Key: YOUR_TOKEN" \
     https://trading-bot.padosoft.workers.dev/api/positions/active
   ```

**Clients configuration**:

**Dashboard** (`.env.local`):
```bash
VITE_API_KEY=YOUR_TOKEN
VITE_API_URL=https://trading-bot.padosoft.workers.dev
```

**Windows Services** (`appsettings.Local.json`):
```json
{
  "CloudflareWorker": {
    "BaseUrl": "https://trading-bot.padosoft.workers.dev",
    "ApiKey": "YOUR_TOKEN"
  }
}
```

**Verify authentication**:
```bash
# Valid token → 200 OK
curl -H "X-Api-Key: YOUR_TOKEN" https://your-worker.workers.dev/api/health
# {"status":"ok","timestamp":"..."}

# Invalid token → 401 Unauthorized
curl -H "X-Api-Key: wrong-token" https://your-worker.workers.dev/api/health
# {"error":"Unauthorized"}

# Missing header → 401 Unauthorized
curl https://your-worker.workers.dev/api/health
# {"error":"Unauthorized"}
```

**Security notes**:
- ✅ Tokens stored in D1 (encrypted at rest)
- ✅ Validated on every request
- ✅ Separate tokens per client (revoke individually)
- ❌ Never commit tokens to git (use `.env.local`, `appsettings.Local.json`)

## Rate Limiting

- **Limit**: 100 requests per minute per client
- **Identifier**: API key or client IP
- **Response**: 429 status with `Retry-After` header

## Deployment

```bash
# Deploy to Cloudflare Workers
bun run deploy
```

The worker will be deployed to `https://trading-system.<your-account>.workers.dev`.

### Custom Domain (Optional)

Configure a custom domain in the Cloudflare dashboard:

1. Go to Workers & Pages
2. Select your worker
3. Settings → Triggers → Custom Domains
4. Add your domain

## Testing

```bash
# Run all tests
bun run test

# Watch mode
bun run test:watch
```

Tests use `@cloudflare/vitest-pool-workers` for D1 integration testing.

## Architecture

```
src/
├── index.ts              - Main Hono app entry point
├── types/
│   ├── env.ts            - Environment bindings
│   └── database.ts       - D1 row types
├── middleware/
│   ├── auth.ts           - API key authentication
│   └── rate-limit.ts     - Rate limiting
└── routes/
    ├── positions.ts      - Positions endpoints
    ├── alerts.ts         - Alerts endpoints
    └── heartbeats.ts     - Heartbeats endpoints
```

## Security

- API key stored as Cloudflare secret (never in code)
- All database queries use prepared statements
- CORS configured to allow only dashboard origin
- Rate limiting prevents abuse
- Input validation on all endpoints

## Troubleshooting

### Database not found

Ensure you've created the D1 database and updated `wrangler.toml` with the correct `database_id`.

### Authentication failing

Check that the API key secret is set correctly:

```bash
wrangler secret list
```

If missing, set it:

```bash
wrangler secret put API_KEY
```

### CORS errors

Update `DASHBOARD_ORIGIN` in `wrangler.toml` to match your dashboard URL.

## License

Proprietary - Lorenzo Padovani Trading System
