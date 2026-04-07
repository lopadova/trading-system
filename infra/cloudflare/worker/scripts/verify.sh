#!/usr/bin/env bash
# Verification script for Cloudflare Worker build
# Run this to verify all components are correctly configured

set -e

echo "=== Trading System Cloudflare Worker Verification ==="
echo ""

# Check if we're in the correct directory
if [ ! -f "package.json" ]; then
  echo "❌ Error: Run this script from infra/cloudflare/worker directory"
  exit 1
fi

echo "✓ Directory check passed"

# Check required files exist
echo ""
echo "Checking required files..."
files=(
  "src/index.ts"
  "src/types/env.ts"
  "src/types/database.ts"
  "src/middleware/auth.ts"
  "src/middleware/rate-limit.ts"
  "src/routes/positions.ts"
  "src/routes/alerts.ts"
  "src/routes/heartbeats.ts"
  "wrangler.toml"
  "tsconfig.json"
  "package.json"
  "migrations/0001_initial_schema.sql"
)

for file in "${files[@]}"; do
  if [ -f "$file" ]; then
    echo "  ✓ $file"
  else
    echo "  ❌ Missing: $file"
    exit 1
  fi
done

# Check dependencies installed
echo ""
echo "Checking dependencies..."
if [ ! -d "node_modules" ]; then
  echo "❌ node_modules not found. Run: bun install"
  exit 1
fi
echo "  ✓ node_modules exists"

# Check for required packages
packages=("hono" "wrangler" "typescript" "vitest")
for pkg in "${packages[@]}"; do
  if [ -d "node_modules/$pkg" ]; then
    echo "  ✓ $pkg installed"
  else
    echo "  ❌ Missing package: $pkg"
    exit 1
  fi
done

# Type check
echo ""
echo "Running TypeScript type check..."
if bun run typecheck > /dev/null 2>&1; then
  echo "  ✓ TypeScript compilation passed (0 errors)"
else
  echo "  ❌ TypeScript compilation failed"
  bun run typecheck
  exit 1
fi

# Build check
echo ""
echo "Running build..."
if bun run build > /dev/null 2>&1; then
  echo "  ✓ Build successful"
else
  echo "  ❌ Build failed"
  bun run build
  exit 1
fi

# Check build output
if [ -f "dist/index.js" ]; then
  echo "  ✓ Build output created: dist/index.js"
else
  echo "  ❌ Build output missing: dist/index.js"
  exit 1
fi

# Configuration checks
echo ""
echo "Checking configuration..."

# Check wrangler.toml has required fields
if grep -q "name = \"trading-system\"" wrangler.toml; then
  echo "  ✓ Worker name configured"
else
  echo "  ❌ Worker name not configured in wrangler.toml"
  exit 1
fi

if grep -q "binding = \"DB\"" wrangler.toml; then
  echo "  ✓ D1 binding configured"
else
  echo "  ❌ D1 binding not configured in wrangler.toml"
  exit 1
fi

if grep -q "DASHBOARD_ORIGIN" wrangler.toml; then
  echo "  ✓ DASHBOARD_ORIGIN variable configured"
else
  echo "  ❌ DASHBOARD_ORIGIN not configured in wrangler.toml"
  exit 1
fi

# Check for database_id placeholder
if grep -q "REPLACE_WITH_YOUR_D1_ID" wrangler.toml; then
  echo "  ⚠️  Warning: D1 database_id still has placeholder value"
  echo "     Run: wrangler d1 create trading-db"
  echo "     Then update database_id in wrangler.toml"
fi

echo ""
echo "=== Verification Summary ==="
echo "✓ All checks passed!"
echo ""
echo "Next steps:"
echo "1. Create D1 database: wrangler d1 create trading-db"
echo "2. Update database_id in wrangler.toml"
echo "3. Run migrations: bun run migrate:local"
echo "4. Set API key secret: wrangler secret put API_KEY"
echo "5. Start dev server: bun run dev"
echo ""
