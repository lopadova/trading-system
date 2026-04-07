#!/usr/bin/env bash
# Pre-Deployment Checklist for Trading System
# Runs comprehensive checks before deployment to ensure system readiness

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
cd "$ROOT_DIR"

echo ""
echo "=== Trading System Pre-Deployment Checklist ==="
echo ""

FAILURES=0

check_pass() {
    echo "  [PASS] $1"
}

check_fail() {
    echo "  [FAIL] $1"
    if [ -n "${2:-}" ]; then
        echo "         $2"
    fi
    ((FAILURES++))
}

check_warn() {
    echo "  [WARN] $1"
    if [ -n "${2:-}" ]; then
        echo "         $2"
    fi
}

# 1. Git Status
echo "1. Checking Git status..."
if [ -d ".git" ]; then
    # Check for uncommitted changes
    if [ -n "$(git status --porcelain)" ]; then
        check_warn "Uncommitted changes found" "Consider committing before deployment"
    else
        check_pass "No uncommitted changes"
    fi

    # Check current branch
    CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
    if [ "$CURRENT_BRANCH" = "main" ] || [ "$CURRENT_BRANCH" = "master" ]; then
        check_pass "On main branch"
    else
        check_warn "Not on main branch" "Current: $CURRENT_BRANCH"
    fi
else
    check_warn "Not a git repository"
fi

# 2. .NET Build
echo ""
echo "2. Checking .NET projects..."

if command -v dotnet &> /dev/null; then
    # Build all projects
    if dotnet build TradingSystem.sln -c Release > /dev/null 2>&1; then
        check_pass ".NET solution builds successfully"
    else
        check_fail ".NET solution build failed" "Run: dotnet build TradingSystem.sln"
    fi

    # Check for published binaries
    SUPERVISOR_BIN="src/TradingSupervisorService/bin/Release/net10.0/win-x64/publish/TradingSupervisorService.exe"
    OPTIONS_BIN="src/OptionsExecutionService/bin/Release/net10.0/win-x64/publish/OptionsExecutionService.exe"

    if [ -f "$SUPERVISOR_BIN" ]; then
        check_pass "TradingSupervisorService published"
    else
        check_warn "TradingSupervisorService not published" "Run: dotnet publish"
    fi

    if [ -f "$OPTIONS_BIN" ]; then
        check_pass "OptionsExecutionService published"
    else
        check_warn "OptionsExecutionService not published" "Run: dotnet publish"
    fi
else
    check_fail ".NET SDK not found" "Install .NET 10 SDK"
fi

# 3. .NET Tests
echo ""
echo "3. Running .NET tests..."

if command -v dotnet &> /dev/null; then
    if dotnet test TradingSystem.sln --no-build -c Release --verbosity quiet > /dev/null 2>&1; then
        check_pass "All .NET tests passed"
    else
        check_fail ".NET tests failed" "Run: dotnet test TradingSystem.sln"
    fi
else
    check_fail "Cannot run tests - .NET SDK not found"
fi

# 4. Configuration Validation
echo ""
echo "4. Checking configuration files..."

# Check TradingMode in OptionsExecutionService
OPTIONS_CONFIG="src/OptionsExecutionService/appsettings.json"
if [ -f "$OPTIONS_CONFIG" ]; then
    TRADING_MODE=$(grep -o '"TradingMode"[[:space:]]*:[[:space:]]*"[^"]*"' "$OPTIONS_CONFIG" | sed 's/.*"\([^"]*\)".*/\1/')
    if [ "$TRADING_MODE" = "paper" ]; then
        check_pass "TradingMode = paper (safe)"
    else
        check_fail "TradingMode = $TRADING_MODE (DANGER!)" "Must be 'paper' for safety"
    fi
else
    check_warn "OptionsExecutionService appsettings.json not found"
fi

# Check gitignore for sensitive files
if [ -f ".gitignore" ]; then
    if grep -q "strategies/private/" ".gitignore"; then
        check_pass "strategies/private/ in .gitignore"
    else
        check_fail "strategies/private/ NOT in .gitignore" "Private strategies could be committed!"
    fi

    if grep -q "*.db" ".gitignore"; then
        check_pass "*.db in .gitignore"
    else
        check_warn "*.db NOT in .gitignore" "Database files could be committed"
    fi
else
    check_fail ".gitignore not found"
fi

# 5. Cloudflare Worker
echo ""
echo "5. Checking Cloudflare Worker..."

WORKER_DIR="infra/cloudflare/worker"
if [ -d "$WORKER_DIR" ]; then
    cd "$WORKER_DIR"

    # Check wrangler.toml
    if [ -f "wrangler.toml" ]; then
        if grep -q "REPLACE_WITH_YOUR_D1_ID" wrangler.toml; then
            check_fail "wrangler.toml has placeholder database_id" "Run: ./scripts/setup-d1.sh"
        else
            check_pass "wrangler.toml configured"
        fi
    else
        check_fail "wrangler.toml not found"
    fi

    # Check if worker builds
    if command -v bun &> /dev/null; then
        if [ -f "package.json" ]; then
            if [ ! -d "node_modules" ]; then
                check_warn "Worker dependencies not installed" "Run: bun install"
            else
                # Build worker
                if bun run build > /dev/null 2>&1; then
                    check_pass "Worker builds successfully"
                else
                    check_fail "Worker build failed" "Run: bun run build"
                fi

                # Run worker tests
                if bun test > /dev/null 2>&1; then
                    check_pass "Worker tests passed"
                else
                    check_fail "Worker tests failed" "Run: bun test"
                fi
            fi
        fi
    else
        check_warn "bun not found" "Cannot verify worker build"
    fi

    cd "$ROOT_DIR"
else
    check_warn "Cloudflare worker directory not found"
fi

# 6. Dashboard
echo ""
echo "6. Checking Dashboard..."

DASHBOARD_DIR="dashboard"
if [ -d "$DASHBOARD_DIR" ]; then
    cd "$DASHBOARD_DIR"

    # Check if dashboard builds
    if command -v bun &> /dev/null; then
        if [ -f "package.json" ]; then
            if [ ! -d "node_modules" ]; then
                check_warn "Dashboard dependencies not installed" "Run: bun install"
            else
                # Type check
                if bun run typecheck > /dev/null 2>&1; then
                    check_pass "Dashboard type check passed"
                else
                    check_fail "Dashboard type check failed" "Run: bun run typecheck"
                fi

                # Lint
                if bun run lint > /dev/null 2>&1; then
                    check_pass "Dashboard lint passed"
                else
                    check_warn "Dashboard lint failed" "Run: bun run lint"
                fi

                # Tests
                if bun test > /dev/null 2>&1; then
                    check_pass "Dashboard tests passed"
                else
                    check_fail "Dashboard tests failed" "Run: bun test"
                fi

                # Build
                if [ -d "dist" ]; then
                    check_pass "Dashboard build exists"
                else
                    check_warn "Dashboard not built" "Run: bun run build"
                fi
            fi
        fi
    else
        check_warn "bun not found" "Cannot verify dashboard build"
    fi

    cd "$ROOT_DIR"
else
    check_warn "Dashboard directory not found"
fi

# 7. Database Migrations
echo ""
echo "7. Checking database migrations..."

MIGRATIONS_DIRS=(
    "src/SharedKernel/Data/Migrations"
    "infra/cloudflare/worker/migrations"
)

for MIGRATIONS_DIR in "${MIGRATIONS_DIRS[@]}"; do
    if [ -d "$MIGRATIONS_DIR" ]; then
        MIGRATION_COUNT=$(find "$MIGRATIONS_DIR" -name "*.sql" -o -name "*Migration.cs" 2>/dev/null | wc -l)
        if [ "$MIGRATION_COUNT" -gt 0 ]; then
            check_pass "Migrations found in $MIGRATIONS_DIR ($MIGRATION_COUNT files)"
        else
            check_warn "No migrations in $MIGRATIONS_DIR"
        fi
    fi
done

# 8. Security Checks
echo ""
echo "8. Security checks..."

# Check for hardcoded secrets
POTENTIAL_SECRETS=$(grep -r "password\|secret\|api_key" --include="*.cs" --include="*.json" --include="*.ts" src/ infra/ dashboard/ 2>/dev/null | grep -v "appsettings.json" | grep -v "example" | wc -l || echo "0")

if [ "$POTENTIAL_SECRETS" -eq 0 ]; then
    check_pass "No hardcoded secrets found"
else
    check_warn "Found $POTENTIAL_SECRETS potential hardcoded secrets" "Review and move to configuration"
fi

# Check for TODO/FIXME
TODO_COUNT=$(grep -r "TODO\|FIXME" --include="*.cs" --include="*.ts" --include="*.tsx" src/ infra/ dashboard/ 2>/dev/null | wc -l || echo "0")

if [ "$TODO_COUNT" -eq 0 ]; then
    check_pass "No TODO/FIXME comments"
else
    check_warn "Found $TODO_COUNT TODO/FIXME comments" "Review before deployment"
fi

# 9. Documentation
echo ""
echo "9. Checking documentation..."

REQUIRED_DOCS=(
    "README.md"
    "CLAUDE.md"
    ".gitignore"
)

for DOC in "${REQUIRED_DOCS[@]}"; do
    if [ -f "$DOC" ]; then
        check_pass "$DOC exists"
    else
        check_warn "$DOC not found"
    fi
done

# Summary
echo ""
echo "=== Checklist Summary ==="
echo ""

if [ $FAILURES -eq 0 ]; then
    echo "All critical checks passed! ✓"
    echo ""
    echo "System is ready for deployment."
    echo ""
    echo "Next steps:"
    echo "  Windows Services: cd infra/windows && ./install-supervisor.ps1"
    echo "  Cloudflare Worker: cd infra/cloudflare/worker && ./scripts/deploy.sh"
    echo "  Dashboard: cd dashboard && ./scripts/deploy.sh"
    echo ""
    exit 0
else
    echo "$FAILURES critical check(s) failed! ✗"
    echo ""
    echo "Fix the failures above before deploying."
    echo ""
    exit 1
fi
