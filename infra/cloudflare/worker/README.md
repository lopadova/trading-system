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

Set via Wrangler CLI:

```bash
# API key for authentication
wrangler secret put API_KEY
# Enter your secret key when prompted
```

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

All protected endpoints require `X-Api-Key` header:

```bash
curl -H "X-Api-Key: your-secret-key" \
  https://your-worker.workers.dev/api/positions/active
```

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
