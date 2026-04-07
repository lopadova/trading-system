#!/usr/bin/env bash
# Verify dashboard deployment health
# Checks that the dashboard is accessible and functional

set -euo pipefail

echo ""
echo "=== Dashboard Deployment Verification ==="
echo ""

# Parse command line arguments
DASHBOARD_URL=""

if [ $# -eq 0 ]; then
    read -p "Enter dashboard URL (e.g., https://trading-dashboard.pages.dev): " DASHBOARD_URL
else
    DASHBOARD_URL="$1"
fi

if [ -z "$DASHBOARD_URL" ]; then
    echo "✗ ERROR: Dashboard URL required"
    exit 1
fi

# Remove trailing slash
DASHBOARD_URL="${DASHBOARD_URL%/}"

echo "Testing dashboard at: $DASHBOARD_URL"
echo ""

# 1. Check if dashboard is accessible
echo "1. Checking if dashboard is accessible..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$DASHBOARD_URL" || echo "000")

if [ "$HTTP_CODE" = "200" ]; then
    echo "   ✓ Dashboard is accessible (HTTP $HTTP_CODE)"
else
    echo "   ✗ Dashboard returned HTTP $HTTP_CODE"
    exit 1
fi

# 2. Check if index.html is served
echo ""
echo "2. Checking if index.html is served..."
RESPONSE=$(curl -s "$DASHBOARD_URL")

if echo "$RESPONSE" | grep -q "<html"; then
    echo "   ✓ HTML content received"
else
    echo "   ✗ Invalid HTML response"
    exit 1
fi

# 3. Check if React root div exists
echo ""
echo "3. Checking React app structure..."
if echo "$RESPONSE" | grep -q 'id="root"'; then
    echo "   ✓ React root div found"
else
    echo "   ✗ React root div not found"
    exit 1
fi

# 4. Check if JavaScript assets are referenced
echo ""
echo "4. Checking JavaScript assets..."
if echo "$RESPONSE" | grep -q "\.js"; then
    echo "   ✓ JavaScript assets referenced"
else
    echo "   ✗ No JavaScript assets found"
    exit 1
fi

# 5. Check if CSS assets are referenced
echo ""
echo "5. Checking CSS assets..."
if echo "$RESPONSE" | grep -q "\.css"; then
    echo "   ✓ CSS assets referenced"
else
    echo "   ⚠ WARNING: No CSS assets found (might be inlined)"
fi

# 6. Check SPA routing (if path doesn't exist, should still return index.html)
echo ""
echo "6. Checking SPA routing..."
HTTP_CODE_404=$(curl -s -o /dev/null -w "%{http_code}" "$DASHBOARD_URL/nonexistent-path-test-12345" || echo "000")

if [ "$HTTP_CODE_404" = "200" ]; then
    echo "   ✓ SPA routing configured correctly (returns 200 for all paths)"
elif [ "$HTTP_CODE_404" = "404" ]; then
    echo "   ✗ SPA routing NOT configured (returns 404 for unknown paths)"
    echo "   Configure your hosting to redirect all routes to index.html"
    exit 1
else
    echo "   ⚠ WARNING: Unexpected HTTP code $HTTP_CODE_404 for unknown path"
fi

# 7. Check if API endpoint is configured (if .env file exists)
echo ""
echo "7. Checking API configuration..."

# Try to fetch a JavaScript bundle to check for API_BASE_URL
JS_FILE=$(echo "$RESPONSE" | grep -o 'src="[^"]*\.js"' | head -1 | sed 's/src="//;s/"//')

if [ -n "$JS_FILE" ]; then
    # Make URL absolute if relative
    if [[ "$JS_FILE" != http* ]]; then
        JS_FILE="$DASHBOARD_URL/$JS_FILE"
    fi

    JS_CONTENT=$(curl -s "$JS_FILE")

    if echo "$JS_CONTENT" | grep -q "VITE_API_BASE_URL"; then
        echo "   ⚠ WARNING: VITE_API_BASE_URL found in bundled JavaScript"
        echo "   This suggests environment variables might not be configured correctly"
    else
        echo "   ✓ API configuration appears correct"
    fi
fi

# 8. Test HTTPS (if URL is HTTPS)
if [[ "$DASHBOARD_URL" == https://* ]]; then
    echo ""
    echo "8. Checking HTTPS certificate..."
    SSL_INFO=$(curl -vI "$DASHBOARD_URL" 2>&1 | grep "SSL certificate verify" || echo "ok")
    if echo "$SSL_INFO" | grep -q "ok\|verify ok"; then
        echo "   ✓ HTTPS certificate valid"
    else
        echo "   ⚠ WARNING: HTTPS certificate issue"
    fi
fi

# Summary
echo ""
echo "=== Verification Complete ==="
echo ""
echo "Dashboard URL: $DASHBOARD_URL"
echo ""
echo "Manual checks to perform:"
echo "  1. Open dashboard in browser and verify UI loads"
echo "  2. Check browser console for errors"
echo "  3. Test API connectivity from dashboard"
echo "  4. Verify authentication works"
echo "  5. Test all major features (alerts, strategies, analytics)"
echo ""
