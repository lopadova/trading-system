#!/usr/bin/env bash
# Setup Cloudflare D1 database for trading-system worker
# This script creates the database and runs migrations

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKER_DIR="$(dirname "$SCRIPT_DIR")"
cd "$WORKER_DIR"

echo ""
echo "=== Cloudflare D1 Database Setup ==="
echo ""

# Check if wrangler is installed
if ! command -v wrangler &> /dev/null; then
    echo "ERROR: wrangler CLI not found"
    echo "Install with: npm install -g wrangler"
    exit 1
fi

echo "✓ wrangler CLI found"

# Check if user is authenticated
if ! wrangler whoami &> /dev/null; then
    echo ""
    echo "Not authenticated with Cloudflare. Running login..."
    wrangler login
fi

echo "✓ Authenticated with Cloudflare"

# Parse wrangler.toml to get database name
DB_NAME=$(grep "database_name" wrangler.toml | head -1 | sed 's/.*"\(.*\)".*/\1/')
if [ -z "$DB_NAME" ]; then
    echo "ERROR: Could not parse database_name from wrangler.toml"
    exit 1
fi

echo ""
echo "Database name: $DB_NAME"

# Check if database already exists
echo ""
echo "Checking if database '$DB_NAME' already exists..."

if wrangler d1 list | grep -q "$DB_NAME"; then
    echo "✓ Database '$DB_NAME' already exists"
    echo ""
    read -p "Do you want to recreate it? (type 'YES' to confirm, anything else to skip): " CONFIRM
    if [ "$CONFIRM" = "YES" ]; then
        echo "Deleting existing database..."
        wrangler d1 delete "$DB_NAME" --skip-confirmation
        echo "Creating new database..."
        wrangler d1 create "$DB_NAME"
    else
        echo "Skipping database creation."
    fi
else
    echo "Database does not exist. Creating..."
    wrangler d1 create "$DB_NAME"
fi

# Get database ID
echo ""
echo "Getting database ID..."
DB_ID=$(wrangler d1 list | grep "$DB_NAME" | awk '{print $2}')

if [ -z "$DB_ID" ]; then
    echo "ERROR: Could not find database ID for '$DB_NAME'"
    exit 1
fi

echo "✓ Database ID: $DB_ID"

# Update wrangler.toml with database ID if needed
echo ""
echo "Checking wrangler.toml configuration..."

if grep -q "REPLACE_WITH_YOUR_D1_ID" wrangler.toml; then
    echo "Updating wrangler.toml with database ID..."
    sed -i.bak "s/REPLACE_WITH_YOUR_D1_ID/$DB_ID/" wrangler.toml
    rm wrangler.toml.bak 2>/dev/null || true
    echo "✓ wrangler.toml updated"
else
    echo "✓ wrangler.toml already configured"
fi

# Apply migrations locally first (for testing)
echo ""
echo "Applying migrations to local database..."
if [ -d "migrations" ] && [ "$(ls -A migrations/*.sql 2>/dev/null)" ]; then
    wrangler d1 migrations apply "$DB_NAME" --local
    echo "✓ Local migrations applied"
else
    echo "⚠ No migrations found in migrations/ directory"
fi

# Ask if user wants to apply migrations to production
echo ""
read -p "Apply migrations to PRODUCTION database? (type 'YES' to confirm): " CONFIRM_PROD

if [ "$CONFIRM_PROD" = "YES" ]; then
    echo "Applying migrations to production..."
    wrangler d1 migrations apply "$DB_NAME"
    echo "✓ Production migrations applied"
else
    echo "Skipping production migrations. Run manually with:"
    echo "  wrangler d1 migrations apply $DB_NAME"
fi

# Verify database schema
echo ""
echo "Verifying database schema..."
echo "Tables in database:"
wrangler d1 execute "$DB_NAME" --local --command "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;"

echo ""
echo "=== D1 Setup Complete ==="
echo ""
echo "Database Name: $DB_NAME"
echo "Database ID: $DB_ID"
echo ""
echo "Next steps:"
echo "1. Set API_KEY secret: wrangler secret put API_KEY"
echo "2. Update DASHBOARD_ORIGIN in wrangler.toml for production"
echo "3. Deploy worker: ./scripts/deploy.sh"
echo ""
