# Skill: Cloudflare Worker + D1 + Hono — Pattern e Best Practice
> Aggiornabile dagli agenti. Controlla knowledge/errors-registry.md per fix noti.

---

## wrangler.toml Definitivo

```toml
name = "trading-system"
main = "src/index.ts"
compatibility_date = "2025-01-01"
compatibility_flags = ["nodejs_compat"]

# Worker + Assets (NON Cloudflare Pages)
[assets]
directory = "./dist"                               # output build React (bun run build)
html_handling = "single-page-application"
not_found_handling = "single-page-application"

[[d1_databases]]
binding = "DB"
database_name = "trading-db"
database_id = "REPLACE_WITH_YOUR_D1_ID"

[vars]
DASHBOARD_ORIGIN = "https://trading.example.com"

# Secrets (mai in wrangler.toml — usa wrangler secret put)
# API_KEY → wrangler secret put API_KEY
```

## Hono App Entry Point

```typescript
// src/index.ts
import { Hono } from 'hono'
import { cors } from 'hono/cors'
import { ingestRouter } from './routes/ingest'
import { systemRouter } from './routes/system'

export interface Env {
  DB: D1Database
  API_KEY: string
  DASHBOARD_ORIGIN: string
}

const app = new Hono<{ Bindings: Env }>()

app.use('*', cors({
  origin: (origin, c) => c.env.DASHBOARD_ORIGIN,
  allowMethods: ['GET', 'POST', 'PATCH', 'DELETE'],
  allowHeaders: ['Content-Type', 'X-Api-Key'],
}))

app.route('/api/v1/ingest', ingestRouter)
app.route('/api/v1/system', systemRouter)

// Health check (no auth required)
app.get('/api/v1/health', (c) => c.json({ ok: true, ts: new Date().toISOString() }))

export default app
```

## Auth Middleware

```typescript
// src/middleware/auth.ts
import { createMiddleware } from 'hono/factory'

export const authMiddleware = createMiddleware<{ Bindings: Env }>(async (c, next) => {
  const key = c.req.header('X-Api-Key')
  if (!key || key !== c.env.API_KEY) {
    return c.json({ error: 'unauthorized' }, 401)
  }
  await next()
})
```

## D1 Query Pattern

```typescript
// Prepared statement — SEMPRE per evitare SQL injection
const result = await c.env.DB
  .prepare('SELECT * FROM events WHERE event_type = ? ORDER BY created_at DESC LIMIT ?')
  .bind(eventType, 50)
  .all<EventRow>()

// Insert
const insertResult = await c.env.DB
  .prepare('INSERT OR IGNORE INTO events (event_id, event_type, payload) VALUES (?, ?, ?)')
  .bind(eventId, eventType, JSON.stringify(payload))
  .run()

// insertResult.meta.changes === 0 → era duplicato (normale)
// insertResult.meta.changes === 1 → inserito

// Batch insert (transazionale — max 1000 statement per batch)
const stmts = events.map(e =>
  c.env.DB.prepare('INSERT OR IGNORE INTO events VALUES (?, ?, ?)')
          .bind(e.event_id, e.event_type, JSON.stringify(e.payload))
)
await c.env.DB.batch(stmts)
```

## D1 Migration

```bash
# Applica migration
wrangler d1 migrations apply trading-db --local    # locale
wrangler d1 migrations apply trading-db            # produzione

# File migration: migrations/0001_initial.sql
# Ogni file è idempotente (usa CREATE TABLE IF NOT EXISTS)

# Verifica
wrangler d1 execute trading-db --local --command "SELECT name FROM sqlite_master WHERE type='table'"
```

## Deploy

```bash
# Build frontend prima del deploy
cd dashboard && bun run build   # output in dist/

# Deploy Worker + Assets in un unico comando
wrangler deploy

# Verifica
curl https://WORKER.DOMAIN.workers.dev/api/v1/health
```

## Anti-pattern D1

```typescript
// ❌ Query senza prepared statement (SQL injection risk)
db.prepare(`SELECT * FROM events WHERE type = '${userInput}'`).all()

// ✅ CORRETTO — bind parameters
db.prepare('SELECT * FROM events WHERE type = ?').bind(userInput).all()

// ❌ Inserimento JSON non serializzato
db.prepare('INSERT INTO events (payload) VALUES (?)').bind(myObject).run()

// ✅ CORRETTO — serializza prima
db.prepare('INSERT INTO events (payload) VALUES (?)').bind(JSON.stringify(myObject)).run()

// ❌ .first() su query che può restituire null senza gestire null
const row = await db.prepare('SELECT * FROM events WHERE id = ?').bind(id).first<Row>()
row.event_type  // ← crash se row è null

// ✅ CORRETTO
const row = await db.prepare('...').bind(id).first<Row>()
if (!row) return c.json({ error: 'not_found' }, 404)
```

## Secrets Management

```bash
# MAI in wrangler.toml o nel codice
# Usa wrangler secret put
wrangler secret put API_KEY
# → prompt interattivo, inserisci il valore

# Lista secrets configurati
wrangler secret list
```
