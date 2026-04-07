#!/usr/bin/env bash
# Deploy dashboard to static hosting
# Supports Cloudflare Pages or custom static hosting

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DASHBOARD_DIR="$(dirname "$SCRIPT_DIR")"
cd "$DASHBOARD_DIR"

echo ""
echo "=== Dashboard Deployment ==="
echo ""

# Parse command line arguments
TARGET="cloudflare-pages"
SKIP_BUILD=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --target)
            TARGET="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--target cloudflare-pages|custom] [--skip-build]"
            exit 1
            ;;
    esac
done

# Build if not skipped
if [ "$SKIP_BUILD" = false ]; then
    echo "Running production build..."
    ./scripts/build.sh
    echo ""
fi

# Verify dist/ exists
if [ ! -d "dist" ]; then
    echo "✗ ERROR: dist/ directory not found"
    echo "  Run: ./scripts/build.sh"
    exit 1
fi

# Deploy based on target
case $TARGET in
    cloudflare-pages)
        echo "Deploying to Cloudflare Pages..."
        echo ""

        # Check if wrangler is installed
        if ! command -v wrangler &> /dev/null; then
            echo "✗ ERROR: wrangler CLI not found"
            echo "  Install with: npm install -g wrangler"
            exit 1
        fi

        # Check authentication
        if ! wrangler whoami &> /dev/null; then
            echo "✗ ERROR: Not authenticated with Cloudflare"
            echo "  Run: wrangler login"
            exit 1
        fi

        # Deploy to Cloudflare Pages
        echo "Deploying to Cloudflare Pages..."

        # Get project name
        read -p "Enter Cloudflare Pages project name (or press Enter for 'trading-dashboard'): " PROJECT_NAME
        PROJECT_NAME=${PROJECT_NAME:-trading-dashboard}

        # Deploy
        wrangler pages deploy dist/ --project-name="$PROJECT_NAME"

        DEPLOY_EXIT_CODE=$?

        if [ $DEPLOY_EXIT_CODE -eq 0 ]; then
            echo ""
            echo "=== Deployment Successful ==="
            echo ""
            echo "Dashboard URL: https://$PROJECT_NAME.pages.dev"
            echo ""
            echo "Configure environment variables in Cloudflare Pages dashboard:"
            echo "  VITE_API_BASE_URL - API endpoint URL"
            echo "  VITE_API_KEY - API authentication key"
            echo ""
        else
            echo "✗ Deployment failed with exit code $DEPLOY_EXIT_CODE"
            exit $DEPLOY_EXIT_CODE
        fi
        ;;

    custom)
        echo "Deploying to custom static hosting..."
        echo ""
        echo "The built files are in the dist/ directory."
        echo ""
        echo "Upload the contents of dist/ to your static hosting provider:"
        echo "  - Netlify: drag-and-drop dist/ folder or use netlify-cli"
        echo "  - Vercel: vercel --prod"
        echo "  - AWS S3: aws s3 sync dist/ s3://your-bucket/ --delete"
        echo "  - GitHub Pages: copy dist/* to gh-pages branch"
        echo ""
        echo "Make sure to configure:"
        echo "  1. SPA routing (redirect all routes to index.html)"
        echo "  2. Environment variables (VITE_API_BASE_URL, VITE_API_KEY)"
        echo "  3. HTTPS enabled"
        echo ""
        ;;

    *)
        echo "✗ ERROR: Unknown target '$TARGET'"
        echo "  Supported targets: cloudflare-pages, custom"
        exit 1
        ;;
esac
