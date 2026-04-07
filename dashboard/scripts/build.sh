#!/usr/bin/env bash
# Build dashboard for production deployment
# Runs type checking, linting, tests, and optimized build

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DASHBOARD_DIR="$(dirname "$SCRIPT_DIR")"
cd "$DASHBOARD_DIR"

echo ""
echo "=== Dashboard Production Build ==="
echo ""

# Parse command line arguments
SKIP_TESTS=false
SKIP_LINT=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        --skip-lint)
            SKIP_LINT=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--skip-tests] [--skip-lint]"
            exit 1
            ;;
    esac
done

# 1. Check dependencies
echo "Checking dependencies..."
if [ ! -d "node_modules" ]; then
    echo "Installing dependencies..."
    bun install
else
    echo "✓ Dependencies installed"
fi

# 2. Type checking
echo ""
echo "Running type check..."
if ! bun run typecheck; then
    echo "✗ Type check failed"
    exit 1
fi
echo "✓ Type check passed"

# 3. Linting (unless skipped)
if [ "$SKIP_LINT" = false ]; then
    echo ""
    echo "Running linter..."
    if ! bun run lint; then
        echo "✗ Lint failed"
        exit 1
    fi
    echo "✓ Lint passed"
fi

# 4. Tests (unless skipped)
if [ "$SKIP_TESTS" = false ]; then
    echo ""
    echo "Running tests..."
    if ! bun test; then
        echo "✗ Tests failed"
        exit 1
    fi
    echo "✓ Tests passed"
fi

# 5. Clean previous build
echo ""
echo "Cleaning previous build..."
rm -rf dist/
echo "✓ Cleaned"

# 6. Production build
echo ""
echo "Building for production..."
if ! bun run build 2>&1 | tee /tmp/dashboard-build.log; then
    echo "✗ Build failed"
    exit 1
fi
echo "✓ Build successful"

# 7. Verify build output
echo ""
echo "Verifying build output..."

if [ ! -d "dist" ]; then
    echo "✗ ERROR: dist/ directory not created"
    exit 1
fi

if [ ! -f "dist/index.html" ]; then
    echo "✗ ERROR: dist/index.html not found"
    exit 1
fi

# Count assets
JS_COUNT=$(find dist/assets -name "*.js" 2>/dev/null | wc -l || echo "0")
CSS_COUNT=$(find dist/assets -name "*.css" 2>/dev/null | wc -l || echo "0")

echo "✓ Build output verified"
echo "  - JavaScript files: $JS_COUNT"
echo "  - CSS files: $CSS_COUNT"

# 8. Calculate bundle size
echo ""
echo "Bundle size analysis:"
if [ -d "dist/assets" ]; then
    du -sh dist/assets | awk '{print "  Total assets size: " $1}'

    # Show largest files
    echo ""
    echo "  Largest files:"
    find dist/assets -type f -exec du -h {} + | sort -rh | head -5 | while read size file; do
        echo "    $size - $(basename "$file")"
    done
fi

# 9. Summary
echo ""
echo "=== Build Complete ==="
echo ""
echo "Output directory: dist/"
echo ""
echo "Next steps:"
echo "1. Test locally: bun run preview"
echo "2. Deploy: ./scripts/deploy.sh"
echo ""
