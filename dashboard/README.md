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

Create `.env.local` (gitignored):

```bash
VITE_API_URL=http://localhost:8787
# VITE_API_KEY=optional-for-local-dev
```

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
