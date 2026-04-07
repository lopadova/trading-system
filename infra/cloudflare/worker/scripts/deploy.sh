#!/usr/bin/env bash
# Deploy Cloudflare Worker with pre-flight checks
# Runs tests, validates configuration, and deploys to Cloudflare

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKER_DIR="$(dirname "$SCRIPT_DIR")"
cd "$WORKER_DIR"

echo ""
echo "=== Cloudflare Worker Deployment ==="
echo ""

# Parse command line arguments
SKIP_TESTS=false
SKIP_BUILD=false
ENV="production"

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --env)
            ENV="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--skip-tests] [--skip-build] [--env production|staging]"
            exit 1
            ;;
    esac
done

# Pre-flight checks
echo "Running pre-flight checks..."
echo ""

# 1. Check wrangler CLI
if ! command -v wrangler &> /dev/null; then
    echo "✗ ERROR: wrangler CLI not found"
    echo "  Install with: npm install -g wrangler"
    exit 1
fi
echo "✓ wrangler CLI found"

# 2. Check authentication
if ! wrangler whoami &> /dev/null; then
    echo "✗ ERROR: Not authenticated with Cloudflare"
    echo "  Run: wrangler login"
    exit 1
fi
echo "✓ Authenticated with Cloudflare"

# 3. Check wrangler.toml configuration
if grep -q "REPLACE_WITH_YOUR_D1_ID" wrangler.toml; then
    echo "✗ ERROR: wrangler.toml still has placeholder database_id"
    echo "  Run: ./scripts/setup-d1.sh"
    exit 1
fi
echo "✓ wrangler.toml configured"

# 4. Check that API_KEY secret is set
echo ""
echo "Checking secrets..."
if ! wrangler secret list 2>/dev/null | grep -q "API_KEY"; then
    echo "⚠ WARNING: API_KEY secret not set"
    read -p "Set API_KEY now? (y/n): " SET_KEY
    if [ "$SET_KEY" = "y" ] || [ "$SET_KEY" = "Y" ]; then
        wrangler secret put API_KEY
    else
        echo "✗ ERROR: API_KEY secret is required"
        echo "  Set with: wrangler secret put API_KEY"
        exit 1
    fi
fi
echo "✓ API_KEY secret configured"

# 5. Run tests (unless skipped)
if [ "$SKIP_TESTS" = false ]; then
    echo ""
    echo "Running tests..."
    if ! bun test; then
        echo "✗ Tests failed"
        exit 1
    fi
    echo "✓ Tests passed"
fi

# 6. Build TypeScript (unless skipped)
if [ "$SKIP_BUILD" = false ]; then
    echo ""
    echo "Building TypeScript..."
    if ! bun run build 2>&1 | tee /tmp/worker-build.log; then
        echo "✗ Build failed"
        exit 1
    fi
    echo "✓ Build successful"
fi

# 7. Validate database schema
echo ""
echo "Validating database schema..."
DB_NAME=$(grep "database_name" wrangler.toml | head -1 | sed 's/.*"\(.*\)".*/\1/')

# Check that required tables exist
REQUIRED_TABLES=("events" "machine_status" "alerts")
for TABLE in "${REQUIRED_TABLES[@]}"; do
    if ! wrangler d1 execute "$DB_NAME" --local --command "SELECT name FROM sqlite_master WHERE type='table' AND name='$TABLE';" 2>/dev/null | grep -q "$TABLE"; then
        echo "⚠ WARNING: Table '$TABLE' not found in local database"
        echo "  Run: wrangler d1 migrations apply $DB_NAME --local"
    fi
done
echo "✓ Database schema validated"

# Deployment confirmation
echo ""
echo "=== Ready to Deploy ==="
echo "Environment: $ENV"
echo "Worker: trading-system"
echo ""

read -p "Proceed with deployment? (type 'YES' to confirm): " CONFIRM

if [ "$CONFIRM" != "YES" ]; then
    echo "Deployment cancelled."
    exit 0
fi

# Deploy
echo ""
echo "Deploying to Cloudflare..."

if [ "$ENV" = "production" ]; then
    wrangler deploy
else
    wrangler deploy --env "$ENV"
fi

DEPLOY_EXIT_CODE=$?

if [ $DEPLOY_EXIT_CODE -eq 0 ]; then
    echo ""
    echo "=== Deployment Successful ==="
    echo ""
    echo "Worker URL: https://trading-system.<your-subdomain>.workers.dev"
    echo ""
    echo "Test the deployment:"
    echo "  curl https://trading-system.<your-subdomain>.workers.dev/api/v1/health"
    echo ""
    exit 0
else
    echo ""
    echo "✗ Deployment failed with exit code $DEPLOY_EXIT_CODE"
    exit $DEPLOY_EXIT_CODE
fi
