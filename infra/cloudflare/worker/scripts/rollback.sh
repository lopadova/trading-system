#!/usr/bin/env bash
# Rollback Cloudflare Worker deployment
# Lists recent deployments and allows rolling back to a previous version

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKER_DIR="$(dirname "$SCRIPT_DIR")"
cd "$WORKER_DIR"

echo ""
echo "=== Cloudflare Worker Rollback ==="
echo ""

# Check wrangler CLI
if ! command -v wrangler &> /dev/null; then
    echo "ERROR: wrangler CLI not found"
    exit 1
fi

# Check authentication
if ! wrangler whoami &> /dev/null; then
    echo "ERROR: Not authenticated with Cloudflare"
    exit 1
fi

# Get worker name from wrangler.toml
WORKER_NAME=$(grep "^name" wrangler.toml | head -1 | sed 's/.*"\(.*\)".*/\1/')

echo "Worker: $WORKER_NAME"
echo ""

# List recent deployments
echo "Fetching recent deployments..."
echo ""

# Note: Cloudflare Workers versioning requires Workers Paid plan
# For free plan, rollback means redeploying a previous version from git

echo "Rollback options:"
echo ""
echo "1. Redeploy from git commit"
echo "2. Redeploy from local backup"
echo "3. Cancel"
echo ""

read -p "Choose option (1-3): " OPTION

case $OPTION in
    1)
        echo ""
        echo "Recent git commits:"
        git log --oneline --decorate -10 -- "$WORKER_DIR"
        echo ""
        read -p "Enter commit hash to rollback to: " COMMIT_HASH

        if [ -z "$COMMIT_HASH" ]; then
            echo "ERROR: Commit hash required"
            exit 1
        fi

        # Create temporary directory for checkout
        TEMP_DIR=$(mktemp -d)
        echo "Checking out commit $COMMIT_HASH to temporary directory..."

        git --work-tree="$TEMP_DIR" checkout "$COMMIT_HASH" -- "$WORKER_DIR"

        # Deploy from temporary directory
        cd "$TEMP_DIR/$(basename "$WORKER_DIR")"

        echo "Installing dependencies..."
        bun install

        echo "Running tests..."
        bun test

        echo ""
        read -p "Deploy this version? (type 'YES' to confirm): " CONFIRM

        if [ "$CONFIRM" = "YES" ]; then
            echo "Deploying rollback version..."
            wrangler deploy
            echo "✓ Rollback deployment successful"
        else
            echo "Rollback cancelled"
        fi

        # Cleanup
        cd "$WORKER_DIR"
        rm -rf "$TEMP_DIR"
        ;;

    2)
        echo ""
        BACKUP_DIR="$WORKER_DIR/backups"

        if [ ! -d "$BACKUP_DIR" ] || [ -z "$(ls -A "$BACKUP_DIR" 2>/dev/null)" ]; then
            echo "No backups found in $BACKUP_DIR"
            echo "Create backups before deploying by copying the worker directory."
            exit 1
        fi

        echo "Available backups:"
        ls -lt "$BACKUP_DIR" | tail -n +2 | nl
        echo ""

        read -p "Enter backup number to restore: " BACKUP_NUM
        BACKUP_PATH=$(ls -t "$BACKUP_DIR" | sed -n "${BACKUP_NUM}p")

        if [ -z "$BACKUP_PATH" ]; then
            echo "ERROR: Invalid backup number"
            exit 1
        fi

        echo "Restoring from backup: $BACKUP_PATH"

        # Copy backup to temporary location
        TEMP_DIR=$(mktemp -d)
        cp -r "$BACKUP_DIR/$BACKUP_PATH"/* "$TEMP_DIR/"

        cd "$TEMP_DIR"

        echo "Installing dependencies..."
        bun install

        echo "Running tests..."
        bun test

        echo ""
        read -p "Deploy this backup? (type 'YES' to confirm): " CONFIRM

        if [ "$CONFIRM" = "YES" ]; then
            echo "Deploying rollback version..."
            wrangler deploy
            echo "✓ Rollback deployment successful"
        else
            echo "Rollback cancelled"
        fi

        # Cleanup
        cd "$WORKER_DIR"
        rm -rf "$TEMP_DIR"
        ;;

    3)
        echo "Rollback cancelled"
        exit 0
        ;;

    *)
        echo "Invalid option"
        exit 1
        ;;
esac

echo ""
echo "=== Rollback Complete ==="
echo ""
echo "Verify the deployment:"
echo "  curl https://trading-system.<your-subdomain>.workers.dev/api/v1/health"
echo ""
