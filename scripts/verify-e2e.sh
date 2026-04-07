#!/usr/bin/env bash
# verify-e2e.sh - E2E Readiness Verification Script
# Checks system readiness for E2E testing (without requiring IBKR connection)

set -euo pipefail

echo "=========================================="
echo "E2E Readiness Verification"
echo "Trading System - $(date '+%Y-%m-%d %H:%M:%S')"
echo "=========================================="
echo ""

REPORT_FILE="./logs/e2e-verification-report-$(date '+%Y%m%d-%H%M%S').md"
mkdir -p ./logs

# Initialize counters
PASSED=0
FAILED=0
WARNINGS=0

# Helper functions
pass() {
    echo "✅ PASS: $1"
    echo "- ✅ PASS: $1" >> "$REPORT_FILE"
    ((PASSED++))
}

fail() {
    echo "❌ FAIL: $1"
    echo "- ❌ FAIL: $1" >> "$REPORT_FILE"
    ((FAILED++))
}

warn() {
    echo "⚠️  WARN: $1"
    echo "- ⚠️  WARN: $1" >> "$REPORT_FILE"
    ((WARNINGS++))
}

# Start report
cat > "$REPORT_FILE" << 'EOF'
# E2E Verification Report

**Date**: $(date '+%Y-%m-%d %H:%M:%S')
**Script**: verify-e2e.sh

---

## Test Results

EOF

echo "## 1. Prerequisites Check" >> "$REPORT_FILE"
echo ""
echo "## 1. Prerequisites Check"

# Check .NET SDK
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    if [[ "$DOTNET_VERSION" == 10.* ]]; then
        pass ".NET SDK 10.0 installed (version: $DOTNET_VERSION)"
    else
        fail ".NET SDK 10.0 required (found: $DOTNET_VERSION)"
    fi
else
    fail ".NET SDK not found in PATH"
fi

# Check Git
if command -v git &> /dev/null; then
    pass "Git installed"
else
    warn "Git not found (optional for E2E tests)"
fi

# Check solution file
if [ -f "./TradingSystem.sln" ]; then
    pass "Solution file exists"
else
    fail "TradingSystem.sln not found"
fi

echo ""
echo "## 2. Build Verification" >> "$REPORT_FILE"
echo ""
echo "## 2. Build Verification"

# Build solution
echo "Building solution..."
if dotnet build -c Debug --no-restore > /tmp/build.log 2>&1; then
    pass "Solution builds successfully (Debug)"
else
    fail "Build failed (check logs/build.log for details)"
    cp /tmp/build.log ./logs/build-error.log
fi

# Run automated tests
echo "Running automated E2E tests..."
if [ -f "./tests/E2E/Automated/E2E.Automated.csproj" ]; then
    if dotnet test ./tests/E2E/Automated/E2E.Automated.csproj --no-build > /tmp/test.log 2>&1; then
        pass "Automated E2E tests passed"
    else
        fail "Automated E2E tests failed (check logs/test-error.log)"
        cp /tmp/test.log ./logs/test-error.log
    fi
else
    warn "Automated E2E tests not found (skipping)"
fi

echo ""
echo "## 3. Database Schema Verification" >> "$REPORT_FILE"
echo ""
echo "## 3. Database Schema Verification"

# Check for migrations
if [ -d "./src/TradingSupervisorService/Data/Migrations" ]; then
    SUPERVISOR_MIGRATIONS=$(find ./src/TradingSupervisorService/Data/Migrations -name "*.cs" 2>/dev/null | wc -l)
    if [ "$SUPERVISOR_MIGRATIONS" -gt 0 ]; then
        pass "Supervisor migrations exist ($SUPERVISOR_MIGRATIONS files)"
    else
        warn "No supervisor migration files found"
    fi
else
    warn "Supervisor migrations directory not found"
fi

if [ -d "./src/OptionsExecutionService/Data/Migrations" ]; then
    OPTIONS_MIGRATIONS=$(find ./src/OptionsExecutionService/Data/Migrations -name "*.cs" 2>/dev/null | wc -l)
    if [ "$OPTIONS_MIGRATIONS" -gt 0 ]; then
        pass "Options migrations exist ($OPTIONS_MIGRATIONS files)"
    else
        warn "No options migration files found"
    fi
else
    warn "Options migrations directory not found"
fi

echo ""
echo "## 4. Configuration Files" >> "$REPORT_FILE"
echo ""
echo "## 4. Configuration Files"

# Check for example config files
if [ -f "./config/supervisor.example.json" ] || [ -f "./src/TradingSupervisorService/appsettings.json" ]; then
    pass "Supervisor config template exists"
else
    warn "Supervisor config template not found (create config/supervisor.example.json)"
fi

if [ -f "./config/options.example.json" ] || [ -f "./src/OptionsExecutionService/appsettings.json" ]; then
    pass "Options config template exists"
else
    warn "Options config template not found (create config/options.example.json)"
fi

echo ""
echo "## 5. E2E Test Files" >> "$REPORT_FILE"
echo ""
echo "## 5. E2E Test Files"

# Check for E2E test markdown files
E2E_COUNT=0
for i in {01..10}; do
    if [ -f "./tests/E2E/E2E-$i-"*.md ]; then
        ((E2E_COUNT++))
    fi
done

if [ "$E2E_COUNT" -eq 10 ]; then
    pass "All 10 E2E test checklists present"
elif [ "$E2E_COUNT" -gt 0 ]; then
    warn "Only $E2E_COUNT/10 E2E test checklists found"
else
    fail "No E2E test checklists found in tests/E2E/"
fi

# Check README
if [ -f "./tests/E2E/README.md" ]; then
    pass "E2E README exists"
else
    warn "E2E README not found"
fi

echo ""
echo "## 6. Strategy Files" >> "$REPORT_FILE"
echo ""
echo "## 6. Strategy Files"

# Check strategies directory
if [ -d "./strategies" ]; then
    pass "Strategies directory exists"

    # Check for .gitkeep in private
    if [ -f "./strategies/private/.gitkeep" ]; then
        pass "strategies/private/.gitkeep exists"
    else
        warn "strategies/private/.gitkeep missing (create to preserve directory)"
    fi

    # Verify strategies/private is gitignored
    if git check-ignore -q strategies/private/test.json 2>/dev/null; then
        pass "strategies/private/ is gitignored"
    else
        fail "strategies/private/ is NOT gitignored (security risk!)"
    fi
else
    fail "Strategies directory not found"
fi

echo ""
echo "## 7. Scripts" >> "$REPORT_FILE"
echo ""
echo "## 7. Scripts"

# Check for essential scripts
SCRIPTS=(
    "check-knowledge.sh"
    "verify-e2e.sh"
    "pre-deployment-checklist.sh"
)

for script in "${SCRIPTS[@]}"; do
    if [ -f "./scripts/$script" ]; then
        if [ -x "./scripts/$script" ]; then
            pass "Script exists and is executable: $script"
        else
            warn "Script exists but not executable: $script"
        fi
    else
        warn "Script not found: $script"
    fi
done

echo ""
echo "## 8. Documentation" >> "$REPORT_FILE"
echo ""
echo "## 8. Documentation"

# Check for key documentation files
DOCS=(
    "docs/GETTING_STARTED.md"
    "docs/ARCHITECTURE.md"
    "docs/CONFIGURATION.md"
    "docs/TROUBLESHOOTING.md"
)

for doc in "${DOCS[@]}"; do
    if [ -f "./$doc" ]; then
        pass "Documentation exists: $doc"
    else
        warn "Documentation missing: $doc"
    fi
done

echo ""
echo "## 9. Knowledge Base" >> "$REPORT_FILE"
echo ""
echo "## 9. Knowledge Base"

# Check knowledge directory
if [ -d "./knowledge" ]; then
    pass "Knowledge directory exists"

    KB_FILES=(
        "errors-registry.md"
        "lessons-learned.md"
        "skill-changelog.md"
    )

    for kb in "${KB_FILES[@]}"; do
        if [ -f "./knowledge/$kb" ]; then
            pass "Knowledge file exists: $kb"
        else
            warn "Knowledge file missing: $kb"
        fi
    done
else
    warn "Knowledge directory not found"
fi

echo ""
echo "## 10. Service Readiness" >> "$REPORT_FILE"
echo ""
echo "## 10. Service Readiness (Dry Run)"

# Try to start services (without IBKR connection)
# This is a smoke test - services should fail gracefully if IBKR not available

echo "Testing TradingSupervisorService startup..."
# We won't actually start it, just check if the executable exists
if [ -f "./src/TradingSupervisorService/bin/Debug/net10.0/TradingSupervisorService.dll" ] || \
   dotnet build ./src/TradingSupervisorService/TradingSupervisorService.csproj --no-restore > /dev/null 2>&1; then
    pass "TradingSupervisorService builds and executable exists"
else
    fail "TradingSupervisorService build failed"
fi

echo "Testing OptionsExecutionService startup..."
if [ -f "./src/OptionsExecutionService/bin/Debug/net10.0/OptionsExecutionService.dll" ] || \
   dotnet build ./src/OptionsExecutionService/OptionsExecutionService.csproj --no-restore > /dev/null 2>&1; then
    pass "OptionsExecutionService builds and executable exists"
else
    fail "OptionsExecutionService build failed"
fi

# Summary
echo ""
echo "=========================================="
echo "SUMMARY"
echo "=========================================="
echo ""
cat >> "$REPORT_FILE" << EOF

---

## Summary

- ✅ Passed: $PASSED
- ❌ Failed: $FAILED
- ⚠️  Warnings: $WARNINGS

EOF

echo "✅ Passed:   $PASSED"
echo "❌ Failed:   $FAILED"
echo "⚠️  Warnings: $WARNINGS"
echo ""

# Overall assessment
if [ "$FAILED" -eq 0 ]; then
    if [ "$WARNINGS" -eq 0 ]; then
        echo "✅ RESULT: READY FOR E2E TESTING"
        echo "**RESULT**: ✅ **READY FOR E2E TESTING**" >> "$REPORT_FILE"
        exit 0
    else
        echo "⚠️  RESULT: READY WITH WARNINGS (review before testing)"
        echo "**RESULT**: ⚠️  **READY WITH WARNINGS** (review before testing)" >> "$REPORT_FILE"
        exit 0
    fi
else
    echo "❌ RESULT: NOT READY (fix failures before E2E testing)"
    echo "**RESULT**: ❌ **NOT READY** (fix failures before E2E testing)" >> "$REPORT_FILE"
    exit 1
fi

echo ""
echo "Report saved to: $REPORT_FILE"
