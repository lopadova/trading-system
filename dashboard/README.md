---
title: "Trading System Dashboard"
tags: ["dev", "dashboard", "reference"]
aliases: ["Dashboard README"]
status: current
audience: ["developer"]
last-reviewed: "2026-04-21"
related:
  - "[[Trading System|Repository Overview]]"
  - "[[Trading System Architecture|ARCHITECTURE]]"
---

# Trading System Dashboard

React + TypeScript + Vite dashboard for the Trading System monitoring and control interface.

## Tech Stack

- **Runtime**: Bun 1.3+
- **Build Tool**: Vite 8
- **Framework**: React 19 with TypeScript (strict mode)
- **Styling**: Tailwind CSS v4 with custom theme system
- **State Management**:
  - **React Query**: Server state and data fetching
  - **Zustand**: UI state (theme, sidebar, filters)
- **HTTP Client**: ky (fetch wrapper)
- **Icons**: lucide-react
- **Animations**: Motion, React Spring
- **Charts**: ECharts, Lightweight Charts (planned)

## Project Structure

```
dashboard/
├── src/
│   ├── components/
│   │   ├── layout/          # Header, Sidebar, Layout
│   │   └── ui/              # Card, Badge, Button, etc.
│   ├── lib/                 # queryClient setup
│   ├── pages/               # HomePage, Health, Positions, etc.
│   ├── stores/              # Zustand stores (uiStore)
│   ├── utils/               # theme, cn (classname merger)
│   ├── App.tsx
│   ├── main.tsx
│   └── index.css            # Tailwind + theme variables
├── public/
├── index.html               # Includes anti-flash theme script
├── tailwind.config.js
├── postcss.config.js
├── tsconfig.json
└── vite.config.ts
```

## Development

```bash
# Install dependencies
bun install

# Start dev server (http://localhost:5173)
bun run dev

# Type check
bun run typecheck

# Lint
bun run lint

# Format code
bun run format

# Build for production
bun run build

# Preview production build
bun run preview
```

## TypeScript Configuration

Strict mode is enabled with additional safety checks:
- `strict: true`
- `noUncheckedIndexedAccess: true` (array access safety)
- `exactOptionalPropertyTypes: true` (no undefined for optional props)

## Theming

The dashboard supports **dark**, **light**, and **system** theme modes.

Theme state is persisted in localStorage and synced via Zustand.
An anti-flash script in `index.html` applies the theme before React renders to prevent flickering.

### CSS Variables

Defined in `src/index.css`:

```css
:root[data-theme='light'] {
  --color-background: #ffffff;
  --color-foreground: #0a0a0a;
  --color-primary: #2563eb;
  /* ... */
}

:root[data-theme='dark'] {
  --color-background: #0a0a0a;
  --color-foreground: #fafafa;
  /* ... */
}
```

Use via Tailwind classes: `bg-background`, `text-foreground`, `border-border`, etc.

## State Management Rules

Follow CLAUDE.md coding standards:

1. **Server data**: Use React Query (`useQuery`, `useMutation`)
   - No `useEffect` for data fetching
   - Automatic caching, refetching, and error handling

2. **UI state**: Use Zustand (`useUiStore`)
   - Theme preference
   - Sidebar open/closed
   - Active filters, sort order, etc.

3. **Component-local state**: Use `useState` for ephemeral UI state (modals, dropdowns)

## Environment Variables

### Setup

Create `.env.local` in the dashboard root (already in `.gitignore`):

```bash
# Required: Cloudflare Worker API URL
VITE_API_URL=https://trading-bot.padosoft.workers.dev

# Required: API authentication token
VITE_API_KEY=a3f5c8d9e2b1f4a6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0
```

**⚠️ Security**: `.env.local` is gitignored - never commit API keys!

### API Key Authentication

The dashboard authenticates requests to the Worker using `X-Api-Key` header.

**How to get the token**:

1. **Generate token** (256-bit random):
   ```bash
   openssl rand -hex 32
   # Output: a3f5c8d9e2b1f4a6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0
   ```

2. **Add to Worker D1 whitelist**:
   ```bash
   cd infra/cloudflare/worker
   bunx wrangler d1 execute trading-db --remote --command="
   INSERT INTO whitelist (api_key, description) 
   VALUES ('YOUR_TOKEN', 'Production Dashboard');
   "
   ```

3. **Add to dashboard `.env.local`**:
   ```bash
   echo "VITE_API_KEY=YOUR_TOKEN" > .env.local
   echo "VITE_API_URL=https://trading-bot.padosoft.workers.dev" >> .env.local
   ```

**How it works**:

```typescript
// utils/apiClient.ts
const response = await fetch(`${import.meta.env.VITE_API_URL}/api/positions`, {
  headers: { 
    'X-Api-Key': import.meta.env.VITE_API_KEY 
  }
})
```

**Verify authentication**:

```bash
# Start dev server
bun run dev

# Open browser console (F12)
# Check Network tab for API requests
# Should see successful 200 responses, NOT 401 Unauthorized
```

**Troubleshooting**:

| Error | Cause | Fix |
|-------|-------|-----|
| `401 Unauthorized` | Token not in whitelist | Add token to D1 with wrangler command |
| `CORS error` | Wrong API URL | Check `VITE_API_URL` matches Worker URL |
| `Network error` | Worker not deployed | Deploy Worker first: `cd infra/cloudflare/worker && bunx wrangler deploy` |
| `undefined API key` | `.env.local` missing | Create `.env.local` with `VITE_API_KEY` |

### Local Development

For local development with Worker running on `localhost:8787`:

```bash
# .env.local (local development)
VITE_API_URL=http://localhost:8787
VITE_API_KEY=test-key-local-dev
```

**Note**: Local Worker doesn't enforce whitelist validation (dev mode).

## Next Steps

- [ ] Add React Router for multi-page navigation
- [ ] Implement API hooks (useHeartbeat, usePositions, etc.)
- [ ] Add ECharts widgets for IVTS monitoring
- [ ] Implement real-time updates (WebSocket or polling)
- [ ] Add error boundary components
- [ ] Create Skeleton loaders for async content
- [ ] Deploy to Cloudflare Pages

## Standards

This project follows the coding standards defined in `/CLAUDE.md`:
- **No `any` types** — explicit typing everywhere
- **Strict TypeScript** — all strict flags enabled
- **React Query for fetching** — no `useEffect` data loading
- **Zustand for UI state** — persistent localStorage sync
- **Functional components** — no class components
- **Early returns** — no nested ternaries or complex conditionals
